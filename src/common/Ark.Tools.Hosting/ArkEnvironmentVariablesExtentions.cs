using Microsoft.Extensions.Configuration.EnvironmentVariables;



namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods for registering <see cref="ArkEnvironmentVariablesConfigurationProvider"/> with <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class ArkEnvironmentVariablesExtentions
{

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddArkEnvironmentVariables(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new ArkEnvironmentVariablesConfigurationSource());
        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables
    /// with a specified prefix.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="prefix">The prefix that environment variable names must start with. The prefix will be removed from the environment variable names.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddArkEnvironmentVariables(
        this IConfigurationBuilder configurationBuilder,
        string prefix)
    {
        configurationBuilder.Add(new ArkEnvironmentVariablesConfigurationSource { Prefix = prefix });
        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="configureSource">Configures the source.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddArkEnvironmentVariables(this IConfigurationBuilder builder, Action<ArkEnvironmentVariablesConfigurationSource> configureSource)
        => builder.Add(configureSource);

}

public static class ConfigurationExtensions
{
    public static T GetRequiredValue<T>(this IConfiguration configuration, string key)
    {
        return configuration.GetValue<T>(key) ?? throw new ArgumentException("Parameter not found in configuration", key);
    }
}