using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

namespace Ark.Tools.Rebus.Tests
{
    public static class InProcessMessageInspectorExtensions
    {
        public static void AddInProcessMessageInspector(this OptionsConfigurer configurer)
        {
            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = new InProcessMessageInspectorStep();
                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
            });
        }
    }
}
