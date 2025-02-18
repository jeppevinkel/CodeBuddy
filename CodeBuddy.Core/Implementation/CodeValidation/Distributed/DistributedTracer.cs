using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public class DistributedTracer : IDistributedTracer
{
    private readonly ILogger<DistributedTracer> _logger;
    private readonly ConcurrentDictionary<string, TraceContext> _activeTraces;
    private readonly ITraceExporter _traceExporter;

    public DistributedTracer(
        ILogger<DistributedTracer> logger,
        ITraceExporter traceExporter)
    {
        _logger = logger;
        _traceExporter = traceExporter;
        _activeTraces = new ConcurrentDictionary<string, TraceContext>();
    }

    public ITrace StartTrace(string operationName)
    {
        var traceId = GenerateTraceId();
        var trace = new Trace(traceId, operationName, this);
        _activeTraces[traceId] = new TraceContext
        {
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Tags = new ConcurrentDictionary<string, object>()
        };
        return trace;
    }

    public void RecordSpan(string traceId, string operationName, DateTime start, DateTime end, params (string key, string value)[] tags)
    {
        if (_activeTraces.TryGetValue(traceId, out var context))
        {
            var span = new TraceSpan
            {
                TraceId = traceId,
                OperationName = operationName,
                StartTime = start,
                EndTime = end,
                Tags = tags.ToDictionary(t => t.key, t => t.value)
            };

            _traceExporter.ExportSpan(span);
        }
    }

    internal void CompleteTrace(string traceId)
    {
        if (_activeTraces.TryRemove(traceId, out var context))
        {
            context.EndTime = DateTime.UtcNow;
            _traceExporter.ExportTrace(new TraceData
            {
                TraceId = traceId,
                OperationName = context.OperationName,
                StartTime = context.StartTime,
                EndTime = context.EndTime,
                Tags = context.Tags.ToDictionary(t => t.Key, t => t.Value.ToString())
            });
        }
    }

    internal void AddTagToTrace(string traceId, string key, object value)
    {
        if (_activeTraces.TryGetValue(traceId, out var context))
        {
            context.Tags[key] = value;
        }
    }

    internal void AddEventToTrace(string traceId, string name, params (string key, object value)[] attributes)
    {
        if (_activeTraces.TryGetValue(traceId, out var context))
        {
            var eventData = new TraceEvent
            {
                Name = name,
                Timestamp = DateTime.UtcNow,
                Attributes = attributes.ToDictionary(a => a.key, a => a.value.ToString())
            };
            _traceExporter.ExportEvent(traceId, eventData);
        }
    }

    private string GenerateTraceId()
    {
        return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private class Trace : ITrace
    {
        private readonly string _traceId;
        private readonly string _operationName;
        private readonly DistributedTracer _tracer;
        private bool _disposed;

        public string TraceId => _traceId;

        public Trace(string traceId, string operationName, DistributedTracer tracer)
        {
            _traceId = traceId;
            _operationName = operationName;
            _tracer = tracer;
        }

        public void SetTag(string key, object value)
        {
            _tracer.AddTagToTrace(_traceId, key, value);
        }

        public void AddEvent(string name, params (string key, object value)[] attributes)
        {
            _tracer.AddEventToTrace(_traceId, name, attributes);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _tracer.CompleteTrace(_traceId);
                _disposed = true;
            }
        }
    }

    private class TraceContext
    {
        public string OperationName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ConcurrentDictionary<string, object> Tags { get; set; }
    }
}

public interface ITraceExporter
{
    void ExportSpan(TraceSpan span);
    void ExportTrace(TraceData trace);
    void ExportEvent(string traceId, TraceEvent eventData);
}

public class TraceSpan
{
    public string TraceId { get; set; }
    public string OperationName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Dictionary<string, string> Tags { get; set; }
}

public class TraceData
{
    public string TraceId { get; set; }
    public string OperationName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Dictionary<string, string> Tags { get; set; }
}

public class TraceEvent
{
    public string Name { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}