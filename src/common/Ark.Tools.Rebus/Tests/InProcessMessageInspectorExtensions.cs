using Rebus.Config;
using Rebus.Pipeline;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
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
                return new PipelineStepConcatenator(pipeline)
                    .OnReceive(step, PipelineAbsolutePosition.Front);
            });
        }
=======
namespace Ark.Tools.Rebus.Tests;

public static class InProcessMessageInspectorExtensions
{
    public static void AddInProcessMessageInspector(this OptionsConfigurer configurer)
    {
        configurer.Decorate<IPipeline>(c =>
        {
            var pipeline = c.Get<IPipeline>();
            var step = new InProcessMessageInspectorStep();
            return new PipelineStepConcatenator(pipeline)
                .OnReceive(step, PipelineAbsolutePosition.Front);
        });
>>>>>>> After


namespace Ark.Tools.Rebus.Tests;

    public static class InProcessMessageInspectorExtensions
    {
        public static void AddInProcessMessageInspector(this OptionsConfigurer configurer)
        {
            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = new InProcessMessageInspectorStep();
                return new PipelineStepConcatenator(pipeline)
                    .OnReceive(step, PipelineAbsolutePosition.Front);
            });
        }
    }