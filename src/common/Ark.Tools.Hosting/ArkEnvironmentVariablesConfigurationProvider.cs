using System.Collections;

namespace Microsoft.Extensions.Configuration.EnvironmentVariables;

/// <summary>
/// An environment variable based <see cref="ConfigurationProvider"/>.
/// </summary>
public class ArkEnvironmentVariablesConfigurationProvider : ConfigurationProvider
{
    private const string _mySqlServerPrefix = "MYSQLCONNSTR_";
    private const string _sqlAzureServerPrefix = "SQLAZURECONNSTR_";
    private const string _sqlServerPrefix = "SQLCONNSTR_";
    private const string _customPrefix = "CUSTOMCONNSTR_";

    private const string _connStrKeyFormat = _connStrKey + "{0}";
    private const string _connStrKey = "ConnectionStrings:";
    private const string _providerKeyFormat = "ConnectionStrings:{0}_ProviderName";

    private static readonly CompositeFormat _connStrKeyCompositeFormat = System.Text.CompositeFormat.Parse(_connStrKeyFormat);
    private static readonly CompositeFormat _providerKeyCompositeFormat = System.Text.CompositeFormat.Parse(_providerKeyFormat);

    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ArkEnvironmentVariablesConfigurationProvider() : this(string.Empty)
    { }

    /// <summary>
    /// Initializes a new instance with the specified prefix.
    /// </summary>
    /// <param name="prefix">A prefix used to filter the environment variables.</param>
    public ArkEnvironmentVariablesConfigurationProvider(string prefix)
    {
        _prefix = prefix ?? string.Empty;
    }

    /// <summary>
    /// Loads the environment variables.
    /// </summary>
    public override void Load()
    {
        Load(Environment.GetEnvironmentVariables());
    }

    internal void Load(IDictionary envVariables)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var filteredEnvVariables = envVariables
            .Cast<DictionaryEntry>()
            .SelectMany(_azureEnvToAppEnv);

        filteredEnvVariables = ArkEnvironmentVariablesConfigurationProvider._normalizeConnectionString(filteredEnvVariables)
            .Where(entry => ((string)entry.Key).StartsWith(_prefix, StringComparison.OrdinalIgnoreCase));

        foreach (var envVariable in filteredEnvVariables)
        {
            var key = ((string)envVariable.Key)[_prefix.Length..];
            data[key] = (string?)envVariable.Value;
        }

        Data = data;
    }

    private static string _normalizeConnectionStringKey(string key)
    {
        return key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
    }

    private static string _normalizeAppSettingsKey(string key)
    {
        return _normalizeConnectionStringKey(key).Replace(".", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
    }

    private static IEnumerable<DictionaryEntry> _normalizeConnectionString(IEnumerable<DictionaryEntry> filteredEnvVariables)
    {
        List<DictionaryEntry> newFilteredEnvVariables = new();

        foreach (var entry in filteredEnvVariables)
        {
            var key = (string)entry.Key;

            if (key.StartsWith(_connStrKey, StringComparison.OrdinalIgnoreCase))
            {
                var newEntry = new DictionaryEntry(key.Replace("_", ".", StringComparison.Ordinal), entry.Value);
                newFilteredEnvVariables.Add(newEntry);
            }
            else
                newFilteredEnvVariables.Add(entry);
        }

        return newFilteredEnvVariables;
    }

    private static IEnumerable<DictionaryEntry> _azureEnvToAppEnv(DictionaryEntry entry)
    {
        var key = (string)entry.Key;
        var prefix = string.Empty;
        var provider = string.Empty;

        if (key.StartsWith(_mySqlServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = _mySqlServerPrefix;
            provider = "MySql.Data.MySqlClient";
        }
        else if (key.StartsWith(_sqlAzureServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = _sqlAzureServerPrefix;
            provider = "Microsoft.Data.SqlClient";
        }
        else if (key.StartsWith(_sqlServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = _sqlServerPrefix;
            provider = "Microsoft.Data.SqlClient";
        }
        else if (key.StartsWith(_customPrefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = _customPrefix;
        }
        else
        {
            entry.Key = _normalizeAppSettingsKey(key);
            yield return entry;
            yield break;
        }

        // Return the key-value pair for connection string
        yield return new DictionaryEntry(
            string.Format(CultureInfo.InvariantCulture, _connStrKeyCompositeFormat, _normalizeConnectionStringKey(key[prefix.Length..])),
            entry.Value);

        if (!string.IsNullOrEmpty(provider))
        {
            // Return the key-value pair for provider name
            yield return new DictionaryEntry(
                string.Format(CultureInfo.InvariantCulture, _providerKeyCompositeFormat, _normalizeConnectionStringKey(key[prefix.Length..])),
                provider);
        }
    }
}