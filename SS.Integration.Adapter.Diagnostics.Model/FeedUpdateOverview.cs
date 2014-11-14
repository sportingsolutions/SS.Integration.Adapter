using System;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FeedUpdateOverview
    {
        public int Sequence { get; set; }
        public bool IsProcessed { get; set; }
        public bool IsSnapshot { get; set; }
        public DateTime Issued { get; set; }

        /// <summary>
        /// The time it took to process the update
        /// This property will be null if the update is being processed
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }
}
