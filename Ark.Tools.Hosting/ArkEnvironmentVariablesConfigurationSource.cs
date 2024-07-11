namespace Microsoft.Extensions.Configuration.EnvironmentVariables
{
    /// <summary>
    /// Represents environment variables as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class ArkEnvironmentVariablesConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// A prefix used to filter environment variables.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>
        /// Builds the <see cref="ArkEnvironmentVariablesConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="ArkEnvironmentVariablesConfigurationProvider"/></returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new ArkEnvironmentVariablesConfigurationProvider(Prefix);
        }
    }
}

