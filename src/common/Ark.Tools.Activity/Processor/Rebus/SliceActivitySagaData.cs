using Ark.Tools.Activity.Messages;

using Rebus.Sagas;

using System;
using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Processor
{
    public sealed class SliceActivitySagaData : SagaData
    {
        public Slice ActivitySlice { get; set; }

        public string FormattedSliceStart
        {
            get
            {
                return this.ActivitySlice.ToString();
            }
        }

        public List<SliceReady> MissingSlices { get; set; } = new List<SliceReady> { };

        public DateTimeOffset? CoolDownTill { get; set; }

        public bool IsScheduled { get; set; }
        public bool IsCoolDown => CoolDownTill > DateTimeOffset.UtcNow;
    }


=======
namespace Ark.Tools.Activity.Processor;

public sealed class SliceActivitySagaData : SagaData
{
    public Slice ActivitySlice { get; set; }

    public string FormattedSliceStart
    {
        get
        {
            return this.ActivitySlice.ToString();
        }
    }

    public List<SliceReady> MissingSlices { get; set; } = new List<SliceReady> { };

    public DateTimeOffset? CoolDownTill { get; set; }

    public bool IsScheduled { get; set; }
    public bool IsCoolDown => CoolDownTill > DateTimeOffset.UtcNow;
>>>>>>> After
    namespace Ark.Tools.Activity.Processor;

    public sealed class SliceActivitySagaData : SagaData
    {
        public Slice ActivitySlice { get; set; }

        public string FormattedSliceStart
        {
            get
            {
                return this.ActivitySlice.ToString();
            }
        }

        public List<SliceReady> MissingSlices { get; set; } = new List<SliceReady> { };

        public DateTimeOffset? CoolDownTill { get; set; }

        public bool IsScheduled { get; set; }
        public bool IsCoolDown => CoolDownTill > DateTimeOffset.UtcNow;
    }