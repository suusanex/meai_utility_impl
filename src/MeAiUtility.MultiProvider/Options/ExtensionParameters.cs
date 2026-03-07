namespace MeAiUtility.MultiProvider.Options;

public sealed class ExtensionParameters
{
    private static readonly HashSet<string> AllowedPrefixes = ["openai", "azure", "copilot"];
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, object? value)
    {
        ValidateKey(key);
        _values[key] = value;
    }

    public T Get<T>(string key)
    {
        if (!TryGet<T>(key, out var value))
        {
            throw new KeyNotFoundException($"Extension parameter '{key}' was not found or type mismatch.");
        }

        return value!;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        if (!_values.TryGetValue(key, out var obj))
        {
            return false;
        }

        if (obj is T casted)
        {
            value = casted;
            return true;
        }

        return false;
    }

    public bool Has(string key) => _values.ContainsKey(key);

    public IReadOnlyDictionary<string, object?> GetAllForProvider(string providerName)
    {
        var prefix = providerName + ".";
        return _values.Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateKey(string key)
    {
        var parts = key.Split('.', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException("Extension key must be in '{provider}.{param}' format.", nameof(key));
        }

        if (!AllowedPrefixes.Contains(parts[0]))
        {
            throw new ArgumentException($"Unsupported provider prefix '{parts[0]}'.", nameof(key));
        }
    }
}
