using System;

namespace CodeBuddy.Core.Models
{
    public class PerformanceReport
    {
        public byte[] Content { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}