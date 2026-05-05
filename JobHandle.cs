using System;
using System.Threading.Tasks;

namespace IndustrialProcessing
{
    public class JobHandle
    {
        public Guid Id { get; set; }
        public Task<int> Result { get; set; }
    }
}