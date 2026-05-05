using System;

namespace IndustrialProcessing
{
    public class JobStats
    {
        public Guid JobId { get; set; }
        public JobType Type { get; set; }
        public bool IsSuccessful { get; set; }
        public double ExecutionTimeMs { get; set; }
    }
}