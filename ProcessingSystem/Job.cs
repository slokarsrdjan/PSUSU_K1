using System;

namespace IndustrialProcessing
{
    public class Job
    {
        public Guid Id { get; set; }
        public JobType Type { get; set; }
        public string Payload { get; set; }
        public int Priority { get; set; } // Manji broj = veći prioritet

        public Job()
        {
            Id = Guid.NewGuid();
        }
    }
}