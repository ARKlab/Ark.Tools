using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System;
using System.Text;


namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Extension methods for registering <see cref="ArkLegacyEnvironmentVariablesConfigurationProvider"/> with <see cref="IConfigurationBuilder"/>.
    /// </summary>
    public static class ArkLegacyEnvironmentVariablesExtentions
    {
        /// <summary>
        /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables.
        /// </summary>
        /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddArkLegacyEnvironmentVariables(this IConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Add(new ArkLegacyEnvironmentVariablesConfigurationSource());
            return configurationBuilder;
        }

        /// <summary>
        /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables
        /// with a specified prefix.
        /// </summary>
        /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="prefix">The prefix that environment variable names must start with. The prefix will be removed from the environment variable names.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddArkLegacyEnvironmentVariables(
            this IConfigurationBuilder configurationBuilder,
            string prefix)
        {
            configurationBuilder.Add(new ArkLegacyEnvironmentVariablesConfigurationSource { Prefix = prefix });
            return configurationBuilder;
        }

        /// <summary>
        /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from environment variables.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddArkLegacyEnvironmentVariables(this IConfigurationBuilder builder, Action<ArkLegacyEnvironmentVariablesConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }
}
