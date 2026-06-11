param(
    [switch]$ShowAllowed
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scanRoots = @(
    '.github/workflows',
    'README.md',
    'src',
    'tests',
    'plans',
    'specs'
)

$excludedSegments = @('bin', 'obj', '.git', '.vs', '.vscode', 'apm_modules', '.codex')
$removedConceptPhrase = [string]::Concat([char]0x3042, [char]0x308A, [char]0x307E, [char]0x305B, [char]0x3093)
$rules = @(
    [pscustomobject]@{
        Name = 'OldSolutionTarget'
        Pattern = '\bMeAiUtility\.slnx?\b'
    },
    [pscustomobject]@{
        Name = 'OldProjectPath'
        Pattern = '(^|[\\/])(src|tests)[\\/]MeAiUtility\.MultiProvider'
    },
    [pscustomobject]@{
        Name = 'OldPackageArtifact'
        Pattern = '\bMeAiUtility-(\$\{|\*)|\bMeAiUtility-\*|\bMeAiUtility\.\*'
    },
    [pscustomobject]@{
        Name = 'OldNamespace'
        Pattern = '\bMeAiUtility\.MultiProvider(\.[A-Za-z0-9_.]+)?\b'
    },
    [pscustomobject]@{
        Name = 'OldMultiProviderApi'
        Pattern = '\b(AddMultiProviderChat|IChatClient|ProviderFactory|ProviderRegistry|MultiProviderOptions|ConversationExecutionOptions|ExtensionParameters|ProviderOverride)\b'
    },
    [pscustomobject]@{
        Name = 'OldMicrosoftExtensionsAI'
        Pattern = '\bMicrosoft\.Extensions\.AI\b'
    },
    [pscustomobject]@{
        Name = 'OldProviderSwitchingGuidance'
        Pattern = '(?i)\bprovider switching\b|\bOpenAICompatible\b|\bOpenAIProvider\b|\bAzureOpenAIProvider\b|\bAzureOpenAI\b|\bOpenAI\s*/\s*Azure OpenAI provider switching\b|\bOpenAI.*provider switching\b|\bprovider switching.*OpenAI\b'
    }
)

function Convert-ToRelativePath {
    param([string]$Path)

    $rootPath = $repoRoot
    if (-not $rootPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootPath = $rootPath + [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri($Path)
    $relative = [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
    return $relative.Replace('\', '/')
}

function Test-ExcludedPath {
    param([string]$RelativePath)

    $segments = $RelativePath -split '/'
    foreach ($segment in $segments) {
        if ($excludedSegments -contains $segment) {
            return $true
        }
    }

    return $false
}

function Get-ReadmeMigrationRange {
    param([string[]]$Lines)

    $start = -1
    $end = $Lines.Length
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($Lines[$i] -eq '## Migration note') {
            $start = $i + 1
            continue
        }

        if ($start -gt 0 -and $Lines[$i].StartsWith('## ')) {
            $end = $i
            break
        }
    }

    return [pscustomobject]@{
        Start = $start
        End = $end
    }
}

function Get-AllowReason {
    param(
        [string]$RelativePath,
        [int]$LineNumber,
        [string]$Line,
        [object]$ReadmeMigrationRange
    )

    if ($Line -match 'ProviderOverride' -and $Line.Contains($removedConceptPhrase)) {
        return 'README removed concept note'
    }

    if ($RelativePath.StartsWith('plans/')) {
        return 'planning artifact context'
    }

    if ($RelativePath.StartsWith('specs/')) {
        return 'historical spec context'
    }

    if ($RelativePath -eq 'README.md' -or $RelativePath.EndsWith('/README.md')) {
        if ($ReadmeMigrationRange.Start -gt 0 -and $LineNumber -ge $ReadmeMigrationRange.Start -and $LineNumber -le $ReadmeMigrationRange.End) {
            return 'README migration note'
        }

        if ($Line -match 'provider switching' -and $Line -match 'OpenAI|Azure OpenAI|OpenAI compatible endpoint') {
            return 'README non-provided provider switching note'
        }

    }

    return $null
}

$candidateFiles = New-Object System.Collections.Generic.List[string]
foreach ($root in $scanRoots) {
    $path = Join-Path $repoRoot $root
    if (-not (Test-Path $path)) {
        continue
    }

    $item = Get-Item $path
    if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $item.FullName -Recurse -File |
            ForEach-Object {
                $relative = Convert-ToRelativePath $_.FullName
                if (-not (Test-ExcludedPath $relative)) {
                    $candidateFiles.Add($_.FullName)
                }
            }
    } else {
        $relative = Convert-ToRelativePath $item.FullName
        if (-not (Test-ExcludedPath $relative)) {
            $candidateFiles.Add($item.FullName)
        }
    }
}

$readmePath = Join-Path $repoRoot 'README.md'
$readmeMigrationRange = [pscustomobject]@{ Start = -1; End = -1 }
if (Test-Path $readmePath) {
    $readmeLines = [System.IO.File]::ReadAllLines($readmePath, [System.Text.Encoding]::UTF8)
    $readmeMigrationRange = Get-ReadmeMigrationRange $readmeLines
}

$violations = New-Object System.Collections.Generic.List[object]
$allowed = New-Object System.Collections.Generic.List[object]

foreach ($file in $candidateFiles) {
    $relative = Convert-ToRelativePath $file
    $lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8)

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        foreach ($rule in $rules) {
            if ($line -match $rule.Pattern) {
                $reason = Get-AllowReason $relative ($i + 1) $line $readmeMigrationRange
                $entry = [pscustomobject]@{
                    Path = $relative
                    Line = $i + 1
                    Rule = $rule.Name
                    Text = $line.Trim()
                    Reason = $reason
                }

                if ([string]::IsNullOrWhiteSpace($reason)) {
                    $violations.Add($entry)
                } else {
                    $allowed.Add($entry)
                }
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Old-name audit failed. Violations: $($violations.Count)"
    foreach ($violation in $violations) {
        Write-Host ("{0}:{1}: {2}: {3}" -f $violation.Path, $violation.Line, $violation.Rule, $violation.Text)
    }

    exit 1
}

Write-Host "Old-name audit passed. Scanned files: $($candidateFiles.Count). Allowed historical/planning hits: $($allowed.Count)."

if ($ShowAllowed -and $allowed.Count -gt 0) {
    $allowed |
        Group-Object Path, Reason |
        Sort-Object Name |
        ForEach-Object {
            Write-Host ("Allowed: {0} ({1} hits)" -f $_.Name, $_.Count)
        }
}
