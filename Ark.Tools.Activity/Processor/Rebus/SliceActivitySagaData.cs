using Ark.Tools.Activity.Messages;
using NodaTime;
using Rebus.Sagas;
using System;
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

		public DateTimeOffset? CoolDownTill { get; set; }

		public bool IsScheduled { get; set; }
		public bool IsCoolDown => CoolDownTill > DateTimeOffset.UtcNow;
	}
}
