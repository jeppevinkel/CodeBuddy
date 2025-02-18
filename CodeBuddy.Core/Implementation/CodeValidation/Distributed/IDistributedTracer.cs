using System;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public interface IDistributedTracer
{
    ITrace StartTrace(string operationName);
    void RecordSpan(string traceId, string operationName, DateTime start, DateTime end, params (string key, string value)[] tags);
}

public interface ITrace : IDisposable
{
    string TraceId { get; }
    void SetTag(string key, object value);
    void AddEvent(string name, params (string key, object value)[] attributes);
}