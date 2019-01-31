using Ark.Tools.Activity.Messages;
using Rebus.Sagas;
using System.Collections.Generic;

namespace Ark.Tools.Activity.Processor
{
    public sealed class SliceActivitySagaData : SagaData
    {
        public Slice ActivitySlice { get; set; }

        public string FormattedSliceStart { get
            {
                return this.ActivitySlice.ToString();
            }
        }

        public List<SliceReady> MissingSlices { get; set; }
    }
}
