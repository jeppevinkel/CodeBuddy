using System;

namespace CodeBuddy.Core.Models.Exceptions
{
    public class ResourceManagementException : Exception
    {
        public ResourceManagementException(string message) : base(message) { }
        public ResourceManagementException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ResourceAllocationException : ResourceManagementException
    {
        public ResourceAllocationException(string message) : base(message) { }
        public ResourceAllocationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ResourceMonitoringException : ResourceManagementException
    {
        public ResourceMonitoringException(string message) : base(message) { }
        public ResourceMonitoringException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ResourceCleanupException : ResourceManagementException
    {
        public ResourceCleanupException(string message) : base(message) { }
        public ResourceCleanupException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ResourceThresholdException : ResourceManagementException
    {
        public string ResourceName { get; }
        public double CurrentValue { get; }
        public double ThresholdValue { get; }

        public ResourceThresholdException(string resourceName, double currentValue, double thresholdValue, string message)
            : base(message)
        {
            ResourceName = resourceName;
            CurrentValue = currentValue;
            ThresholdValue = thresholdValue;
        }
    }
}