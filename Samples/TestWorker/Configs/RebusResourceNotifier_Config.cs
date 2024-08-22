using Ark.Tools.Activity.Provider;

using System;

using TestWorker.Constants;

namespace TestWorker.Configs
{
	public class RebusResourceNotifier_Config : IRebusResourceNotifier_Config
	{
        public RebusResourceNotifier_Config(string? asbConnectionString)
        {
            AsbConnectionString = asbConnectionString ?? throw new ArgumentNullException(nameof(asbConnectionString));
        }

        public string AsbConnectionString { get; set; }
		public string ProviderName { get; set; } = Test_Constants.ProviderName;
		public bool StartAtCreation { get; set; } = Test_Constants.StartAtCreationDefault;

		string IRebusResourceNotifier_Config.AsbConnectionString { get { return this.AsbConnectionString; } }
		string IRebusResourceNotifier_Config.ProviderName { get { return this.ProviderName; } }
		bool IRebusResourceNotifier_Config.StartAtCreation { get { return this.StartAtCreation; } }
	}
}
