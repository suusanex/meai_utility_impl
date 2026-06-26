#:property TargetFramework=net10.0
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

var options = ParseArguments(args);

if (options.ShowHelp)
{
    ShowUsage();
    return 0;
}

try
{
    ValidateOptions(options);

    var outputDirectory = Path.GetFullPath(options.OutputDirectory);
    Directory.CreateDirectory(outputDirectory);

    var prView = await RunGhAsync(
        "pr",
        "view",
        options.PullRequestNumber.ToString(),
        "--repo",
        options.Repository,
        "--json",
        "number,title,state,author,body,url,headRefName,baseRefName,mergeable,isDraft,reviewDecision,latestReviews,comments,commits,files");

    var reviewComments = await RunGhAsync(
        "api",
        $"repos/{options.Owner}/{options.Name}/pulls/{options.PullRequestNumber}/comments",
        "--paginate",
        "--slurp");

    string? checks = null;
    if (options.IncludeChecks)
    {
        checks = await RunGhAsync(
            "pr",
            "checks",
            options.PullRequestNumber.ToString(),
            "--repo",
            options.Repository,
            "--json",
            "name,state,conclusion,startedAt,completedAt,link,bucket");
    }

    using var prViewJson = JsonDocument.Parse(prView);
    var normalizedReviewComments = NormalizePaginatedJsonArray(reviewComments);
    using var reviewCommentsJson = JsonDocument.Parse(normalizedReviewComments);
    using var checksJson = checks is null ? null : JsonDocument.Parse(checks);

    var context = new Dictionary<string, object?>
    {
        ["generatedAt"] = DateTimeOffset.UtcNow,
        ["repository"] = options.Repository,
        ["pullRequest"] = options.PullRequestNumber,
        ["includeChecks"] = options.IncludeChecks,
        ["sources"] = new Dictionary<string, object?>
        {
            ["prView"] = prViewJson.RootElement.Clone(),
            ["reviewComments"] = reviewCommentsJson.RootElement.Clone(),
            ["checks"] = checksJson?.RootElement.Clone()
        }
    };

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    var jsonPath = Path.Combine(outputDirectory, "review-context.json");
    var markdownPath = Path.Combine(outputDirectory, "review-context.md");

    File.WriteAllText(jsonPath, JsonSerializer.Serialize(context, jsonOptions), Encoding.UTF8);
    File.WriteAllText(markdownPath, BuildMarkdown(options, prViewJson.RootElement, reviewCommentsJson.RootElement, checksJson?.RootElement), Encoding.UTF8);

    Console.WriteLine("Review context collected.");
    Console.WriteLine($"Markdown: {markdownPath}");
    Console.WriteLine($"JSON: {jsonPath}");
    return 0;
}
catch (Exception ex)
{
    Trace.WriteLine(ex.ToString());
    Console.Error.WriteLine("Error: failed to collect PR review context.");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

static Options ParseArguments(string[] args)
{
    var options = new Options();

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--help":
            case "-h":
                options.ShowHelp = true;
                break;
            case "--repo":
                options.Repository = ReadValue(args, ref i, "--repo");
                break;
            case "--pr":
                var value = ReadValue(args, ref i, "--pr");
                if (!int.TryParse(value, out var number) || number <= 0)
                {
                    throw new ArgumentException("--pr must be a positive integer.");
                }

                options.PullRequestNumber = number;
                break;
            case "--out":
                options.OutputDirectory = ReadValue(args, ref i, "--out");
                break;
            case "--include-checks":
                options.IncludeChecks = true;
                break;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    return options;
}

static string ReadValue(string[] args, ref int index, string optionName)
{
    if (index + 1 >= args.Length)
    {
        throw new ArgumentException($"{optionName} requires a value.");
    }

    index++;
    return args[index];
}

static void ValidateOptions(Options options)
{
    if (string.IsNullOrWhiteSpace(options.Repository))
    {
        throw new ArgumentException("--repo is required.");
    }

    var parts = options.Repository.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 2)
    {
        throw new ArgumentException("--repo must be in owner/name format.");
    }

    if (options.PullRequestNumber <= 0)
    {
        throw new ArgumentException("--pr is required.");
    }

    if (string.IsNullOrWhiteSpace(options.OutputDirectory))
    {
        throw new ArgumentException("--out is required.");
    }

    options.Owner = parts[0];
    options.Name = parts[1];
}

static async Task<string> RunGhAsync(params string[] arguments)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "gh",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start gh.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var output = await outputTask;
    var error = await errorTask;

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"gh {FormatArguments(arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{error}");
    }

    return output;
}

static string NormalizePaginatedJsonArray(string json)
{
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    if (root.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidOperationException("Expected gh api --paginate --slurp to return a JSON array.");
    }

    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartArray();
        foreach (var page in root.EnumerateArray())
        {
            if (page.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in page.EnumerateArray())
                {
                    item.WriteTo(writer);
                }

                continue;
            }

            if (page.ValueKind == JsonValueKind.Object)
            {
                page.WriteTo(writer);
                continue;
            }

            throw new InvalidOperationException("Expected each paginated gh api page to be a JSON array or object.");
        }

        writer.WriteEndArray();
    }

    return Encoding.UTF8.GetString(stream.ToArray());
}

static string BuildMarkdown(Options options, JsonElement prView, JsonElement reviewComments, JsonElement? checks)
{
    var builder = new StringBuilder();
    var title = GetString(prView, "title");
    var url = GetString(prView, "url");

    builder.AppendLine("# PR Review Context");
    builder.AppendLine();
    builder.AppendLine("## Target");
    builder.AppendLine();
    builder.AppendLine($"- Repository: {options.Repository}");
    builder.AppendLine($"- PR: #{options.PullRequestNumber}");
    builder.AppendLine($"- Title: {title}");
    builder.AppendLine($"- URL: {url}");
    builder.AppendLine($"- State: {GetString(prView, "state")}");
    builder.AppendLine($"- Draft: {GetBooleanText(prView, "isDraft")}");
    builder.AppendLine($"- Review decision: {GetString(prView, "reviewDecision")}");
    builder.AppendLine($"- Base: {GetString(prView, "baseRefName")}");
    builder.AppendLine($"- Head: {GetString(prView, "headRefName")}");
    builder.AppendLine();

    AppendBody(builder, prView);
    AppendFiles(builder, prView);
    AppendLatestReviews(builder, prView);
    AppendIssueComments(builder, prView);
    AppendReviewComments(builder, reviewComments);
    AppendChecks(builder, checks);
    AppendCopilotNote(builder, prView, reviewComments);

    return builder.ToString();
}

static void AppendBody(StringBuilder builder, JsonElement prView)
{
    builder.AppendLine("## PR Body");
    builder.AppendLine();
    builder.AppendLine(GetString(prView, "body"));
    builder.AppendLine();
}

static void AppendFiles(StringBuilder builder, JsonElement prView)
{
    builder.AppendLine("## Changed Files");
    builder.AppendLine();

    if (TryGetArray(prView, "files", out var files))
    {
        foreach (var file in files.EnumerateArray())
        {
            builder.AppendLine($"- {GetString(file, "path")}");
        }
    }

    builder.AppendLine();
}

static void AppendLatestReviews(StringBuilder builder, JsonElement prView)
{
    builder.AppendLine("## Latest Reviews");
    builder.AppendLine();

    if (TryGetArray(prView, "latestReviews", out var reviews))
    {
        foreach (var review in reviews.EnumerateArray())
        {
            var author = GetNestedString(review, "author", "login");
            builder.AppendLine($"### {author} / {GetString(review, "state")}");
            builder.AppendLine();
            builder.AppendLine($"- Submitted at: {GetString(review, "submittedAt")}");
            builder.AppendLine();
            builder.AppendLine(GetString(review, "body"));
            builder.AppendLine();
        }
    }

    builder.AppendLine();
}

static void AppendIssueComments(StringBuilder builder, JsonElement prView)
{
    builder.AppendLine("## PR Comments");
    builder.AppendLine();

    if (TryGetArray(prView, "comments", out var comments))
    {
        foreach (var comment in comments.EnumerateArray())
        {
            var author = GetNestedString(comment, "author", "login");
            builder.AppendLine($"### {author} / {GetString(comment, "createdAt")}");
            builder.AppendLine();
            builder.AppendLine(GetString(comment, "body"));
            builder.AppendLine();
        }
    }

    builder.AppendLine();
}

static void AppendReviewComments(StringBuilder builder, JsonElement reviewComments)
{
    builder.AppendLine("## Review Comments");
    builder.AppendLine();

    if (reviewComments.ValueKind == JsonValueKind.Array)
    {
        foreach (var comment in reviewComments.EnumerateArray())
        {
            var user = GetNestedString(comment, "user", "login");
            builder.AppendLine($"### {user} / {GetString(comment, "path")}:{GetNumberText(comment, "line")}");
            builder.AppendLine();
            builder.AppendLine($"- URL: {GetString(comment, "html_url")}");
            builder.AppendLine($"- Created at: {GetString(comment, "created_at")}");
            builder.AppendLine();
            builder.AppendLine(GetString(comment, "body"));
            builder.AppendLine();
        }
    }

    builder.AppendLine();
}

static void AppendChecks(StringBuilder builder, JsonElement? checks)
{
    builder.AppendLine("## Checks");
    builder.AppendLine();

    if (checks is null)
    {
        builder.AppendLine("Checks were not requested.");
        builder.AppendLine();
        return;
    }

    if (checks.Value.ValueKind == JsonValueKind.Array)
    {
        foreach (var check in checks.Value.EnumerateArray())
        {
            builder.AppendLine($"- {GetString(check, "name")}: state={GetString(check, "state")}, conclusion={GetString(check, "conclusion")}, bucket={GetString(check, "bucket")}");
        }
    }

    builder.AppendLine();
}

static void AppendCopilotNote(StringBuilder builder, JsonElement prView, JsonElement reviewComments)
{
    builder.AppendLine("## GitHub Copilot Review Note");
    builder.AppendLine();

    var found = ContainsCopilot(prView) || ContainsCopilot(reviewComments);
    if (found)
    {
        builder.AppendLine("GitHub Copilot related review data was found in the collected PR reviews or comments.");
    }
    else
    {
        builder.AppendLine("GitHub Copilot review data was not identified in the collected PR reviews or comments. Treat this as not collected, not as evidence that no Copilot review exists.");
    }

    builder.AppendLine();
}

static bool ContainsCopilot(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(property => ContainsCopilot(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(ContainsCopilot),
        JsonValueKind.String => (element.GetString() ?? string.Empty).Contains("copilot", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}

static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
{
    if (element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Array)
    {
        array = property;
        return true;
    }

    array = default;
    return false;
}

static string GetString(JsonElement element, string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
        return string.Empty;
    }

    return property.ValueKind switch
    {
        JsonValueKind.String => property.GetString() ?? string.Empty,
        JsonValueKind.Number => property.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => property.ToString()
    };
}

static string GetNestedString(JsonElement element, string objectName, string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object
        || !element.TryGetProperty(objectName, out var nested)
        || nested.ValueKind != JsonValueKind.Object)
    {
        return string.Empty;
    }

    return GetString(nested, propertyName);
}

static string GetBooleanText(JsonElement element, string propertyName)
{
    if (element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
    {
        return property.GetBoolean().ToString();
    }

    return string.Empty;
}

static string GetNumberText(JsonElement element, string propertyName)
{
    if (element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number)
    {
        return property.ToString();
    }

    return string.Empty;
}

static string FormatArguments(IEnumerable<string> arguments)
{
    return string.Join(" ", arguments.Select(QuoteArgument));
}

static string QuoteArgument(string argument)
{
    return argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"")}\"" : argument;
}

static void ShowUsage()
{
    Console.WriteLine("""
Usage:
  dotnet run --file scripts/collect-pr-review-context.cs -- --repo owner/name --pr 123 --out .review/pr-123 [--include-checks]

Options:
  --repo owner/name     GitHub repository.
  --pr number           Pull request number.
  --out directory       Output directory for review-context.md and review-context.json.
  --include-checks      Include gh pr checks output.
  --help                Show this help.
""");
}

sealed class Options
{
    public string Repository { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int PullRequestNumber { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    public bool IncludeChecks { get; set; }

    public bool ShowHelp { get; set; }
}
