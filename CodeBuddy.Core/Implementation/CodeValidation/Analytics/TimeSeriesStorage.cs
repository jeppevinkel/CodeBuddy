using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO.Compression;
using System.Text.Json;
using System.IO;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public interface ITimeSeriesStorageOptions
    {
        TimeSpan RetentionPeriod { get; set; }
        TimeSpan SamplingRate { get; set; }
        bool EnableCompression { get; set; }
        int MaxDataPointsInMemory { get; set; }
        string StoragePath { get; set; }
        CompressionLevel CompressionLevel { get; set; }
    }

    public class TimeSeriesStorageOptions : ITimeSeriesStorageOptions
    {
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
        public TimeSpan SamplingRate { get; set; } = TimeSpan.FromSeconds(1);
        public bool EnableCompression { get; set; } = true;
        public int MaxDataPointsInMemory { get; set; } = 1000000;
        public string StoragePath { get; set; } = "data/timeseries";
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    }

    public class StorageHealthStatus
    {
        public bool IsHealthy { get; set; }
        public long TotalStorageSize { get; set; }
        public int DataPointCount { get; set; }
        public DateTime OldestDataPoint { get; set; }
        public DateTime NewestDataPoint { get; set; }
        public TimeSpan AverageWriteLatency { get; set; }
        public TimeSpan AverageReadLatency { get; set; }
        public string LastError { get; set; }
    }

    public class AggregatedDataPoint
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int SampleCount { get; set; }
    }

    public enum AggregationType
    {
        Average,
        Sum,
        Min,
        Max,
        Count
    }

    public interface ITimeSeriesStorage
    {
        Task StoreDataPointAsync(TimeSeriesDataPoint dataPoint);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime, Dictionary<string, string> tags);
        Task PruneDataAsync(DateTime olderThan);
        Task<IEnumerable<AggregatedDataPoint>> GetAggregatedDataAsync(
            DateTime startTime,
            DateTime endTime,
            TimeSpan aggregationInterval,
            Dictionary<string, AggregationType> metricAggregations,
            Dictionary<string, string> tags = null);
        Task<StorageHealthStatus> GetHealthStatusAsync();
    }

    public class TimeSeriesStorage : ITimeSeriesStorage, IDisposable
    {
        private readonly IList<TimeSeriesDataPoint> _inMemoryDataPoints;
        private readonly object _lock = new object();
        private readonly ITimeSeriesStorageOptions _options;
        private readonly Timer _cleanupTimer;
        private readonly Timer _flushToDiskTimer;
        private bool _disposed;

        public TimeSeriesStorage(ITimeSeriesStorageOptions options)
        {
            _options = options;
            _inMemoryDataPoints = new List<TimeSeriesDataPoint>();
            
            // Create storage directory if it doesn't exist
            Directory.CreateDirectory(_options.StoragePath);

            // Initialize cleanup timer
            _cleanupTimer = new Timer(CleanupOldData, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            
            // Initialize flush to disk timer
            _flushToDiskTimer = new Timer(FlushToDisk, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private string GetStorageFileName(DateTime timestamp)
        {
            return Path.Combine(_options.StoragePath, $"timeseries_{timestamp:yyyyMMdd_HH}.dat");
        }

        private async void FlushToDisk(object state)
        {
            try
            {
                List<TimeSeriesDataPoint> pointsToFlush;
                lock (_lock)
                {
                    pointsToFlush = _inMemoryDataPoints.ToList();
                    _inMemoryDataPoints.Clear();
                }

                if (!pointsToFlush.Any())
                    return;

                var groupedPoints = pointsToFlush.GroupBy(p => p.Timestamp.Date.AddHours(p.Timestamp.Hour));

                foreach (var group in groupedPoints)
                {
                    var fileName = GetStorageFileName(group.Key);
                    var json = JsonSerializer.Serialize(group.ToList());
                    
                    if (_options.EnableCompression)
                    {
                        using var fileStream = File.Create(fileName);
                        using var compressionStream = new GZipStream(fileStream, _options.CompressionLevel);
                        using var writer = new StreamWriter(compressionStream);
                        await writer.WriteAsync(json);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(fileName, json);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error flushing data to disk: {ex}");
            }
        }

        private async void CleanupOldData(object state)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow - _options.RetentionPeriod;
                var files = Directory.GetFiles(_options.StoragePath, "timeseries_*.dat");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(fileName.Substring(11), "yyyyMMdd_HH",
                        null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                }

                await PruneDataAsync(cutoffDate);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error cleaning up old data: {ex}");
            }
        }

        public async Task StoreDataPointAsync(TimeSeriesDataPoint dataPoint)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            var startOperation = DateTime.UtcNow;
            try
            {
                // Sample data based on sampling rate
            var lastPoint = _inMemoryDataPoints.LastOrDefault();
            if (lastPoint != null && 
                (dataPoint.Timestamp - lastPoint.Timestamp) < _options.SamplingRate)
            {
                return;
            }

            lock (_lock)
            {
                _inMemoryDataPoints.Add(dataPoint);

                // Ensure we don't exceed max data points by removing oldest entries
                while (_inMemoryDataPoints.Count > _options.MaxDataPointsInMemory)
                {
                    _inMemoryDataPoints.RemoveAt(0);
                }
            }

            // If we've accumulated enough points, trigger a flush
            if (_inMemoryDataPoints.Count >= _options.MaxDataPointsInMemory / 2)
            {
                await Task.Run(() => FlushToDisk(null));
            }
        }

        public async Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            var results = new List<TimeSeriesDataPoint>();

            // Get in-memory points
            lock (_lock)
            {
                results.AddRange(_inMemoryDataPoints.Where(dp => 
                    dp.Timestamp >= startTime && dp.Timestamp <= endTime));
            }

            // Get points from disk
            var startDate = startTime.Date;
            var endDate = endTime.Date;
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    var fileDate = currentDate.AddHours(hour);
                    if (fileDate < startTime || fileDate > endTime)
                        continue;

                    var fileName = GetStorageFileName(fileDate);
                    if (!File.Exists(fileName))
                        continue;

                    try
                    {
                        string json;
                        if (_options.EnableCompression)
                        {
                            using var fileStream = File.OpenRead(fileName);
                            using var decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress);
                            using var reader = new StreamReader(decompressionStream);
                            json = await reader.ReadToEndAsync();
                        }
                        else
                        {
                            json = await File.ReadAllTextAsync(fileName);
                        }

                        var points = JsonSerializer.Deserialize<List<TimeSeriesDataPoint>>(json);
                        results.AddRange(points.Where(dp => 
                            dp.Timestamp >= startTime && dp.Timestamp <= endTime));
                    }
                    catch (Exception ex)
                    {
                        // Log error
                        Console.WriteLine($"Error reading data file {fileName}: {ex}");
                    }
                }
                currentDate = currentDate.AddDays(1);
            }

            return results.OrderBy(dp => dp.Timestamp);
        }

        public async Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(
            DateTime startTime,
            DateTime endTime,
            Dictionary<string, string> tags)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            var points = await GetDataPointsAsync(startTime, endTime);
            return points.Where(dp => tags.All(t => 
                dp.Tags.ContainsKey(t.Key) && dp.Tags[t.Key] == t.Value));
        }

        public async Task PruneDataAsync(DateTime olderThan)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            // Remove old in-memory points
            lock (_lock)
            {
                _inMemoryDataPoints.RemoveAll(dp => dp.Timestamp < olderThan);
            }

            // Remove old files
            var files = Directory.GetFiles(_options.StoragePath, "timeseries_*.dat");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParseExact(fileName.Substring(11), "yyyyMMdd_HH",
                    null, System.Globalization.DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate < olderThan)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            // Log error
                            Console.WriteLine($"Error deleting old data file {file}: {ex}");
                        }
                    }
                }
            }
        }

        private readonly Dictionary<string, TimeSpan> _operationLatencies = new Dictionary<string, TimeSpan>();
        private string _lastError;
        private readonly object _healthLock = new object();

        private void TrackOperationLatency(string operation, TimeSpan latency)
        {
            lock (_healthLock)
            {
                _operationLatencies[operation] = latency;
            }
        }

        private void TrackError(string error)
        {
            lock (_healthLock)
            {
                _lastError = error;
            }
        }

        public async Task<StorageHealthStatus> GetHealthStatusAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            var status = new StorageHealthStatus();
            
            try
            {
                // Calculate storage size
                status.TotalStorageSize = Directory.GetFiles(_options.StoragePath, "timeseries_*.dat")
                    .Sum(f => new FileInfo(f).Length);

                // Get data point statistics
                var allPoints = await GetDataPointsAsync(DateTime.MinValue, DateTime.MaxValue);
                var points = allPoints.ToList();
                
                status.DataPointCount = points.Count;
                if (points.Any())
                {
                    status.OldestDataPoint = points.Min(p => p.Timestamp);
                    status.NewestDataPoint = points.Max(p => p.Timestamp);
                }

                // Get operation latencies
                lock (_healthLock)
                {
                    if (_operationLatencies.ContainsKey("write"))
                        status.AverageWriteLatency = _operationLatencies["write"];
                    if (_operationLatencies.ContainsKey("read"))
                        status.AverageReadLatency = _operationLatencies["read"];
                    status.LastError = _lastError;
                }

                status.IsHealthy = true;
            }
            catch (Exception ex)
            {
                status.IsHealthy = false;
                status.LastError = ex.Message;
                TrackError(ex.Message);
            }

            return status;
        }

        public async Task<IEnumerable<AggregatedDataPoint>> GetAggregatedDataAsync(
            DateTime startTime,
            DateTime endTime,
            TimeSpan aggregationInterval,
            Dictionary<string, AggregationType> metricAggregations,
            Dictionary<string, string> tags = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimeSeriesStorage));

            var startOperation = DateTime.UtcNow;
            try
            {
                var rawData = await GetDataPointsAsync(startTime, endTime, tags);
                var groupedData = rawData
                    .GroupBy(p => new DateTime(
                        p.Timestamp.Ticks / aggregationInterval.Ticks * aggregationInterval.Ticks,
                        DateTimeKind.Utc));

                var aggregatedPoints = new List<AggregatedDataPoint>();

                foreach (var group in groupedData)
                {
                    var points = group.ToList();
                    var aggregatedMetrics = new Dictionary<string, double>();

                    foreach (var metricAgg in metricAggregations)
                    {
                        var metricName = metricAgg.Key;
                        var aggregationType = metricAgg.Value;

                        var metricValues = points
                            .Where(p => p.Metrics.ContainsKey(metricName))
                            .Select(p => p.Metrics[metricName])
                            .ToList();

                        if (!metricValues.Any())
                            continue;

                        double aggregatedValue = 0;
                        switch (aggregationType)
                        {
                            case AggregationType.Average:
                                aggregatedValue = metricValues.Average();
                                break;
                            case AggregationType.Sum:
                                aggregatedValue = metricValues.Sum();
                                break;
                            case AggregationType.Min:
                                aggregatedValue = metricValues.Min();
                                break;
                            case AggregationType.Max:
                                aggregatedValue = metricValues.Max();
                                break;
                            case AggregationType.Count:
                                aggregatedValue = metricValues.Count;
                                break;
                        }

                        aggregatedMetrics[metricName] = aggregatedValue;
                    }

                    var commonTags = points
                        .SelectMany(p => p.Tags)
                        .GroupBy(t => t.Key)
                        .Where(g => g.Select(t => t.Value).Distinct().Count() == 1)
                        .ToDictionary(g => g.Key, g => g.First().Value);

                    aggregatedPoints.Add(new AggregatedDataPoint
                    {
                        Timestamp = group.Key,
                        Metrics = aggregatedMetrics,
                        Tags = commonTags,
                        SampleCount = points.Count
                    });
                }

                TrackOperationLatency("read", DateTime.UtcNow - startOperation);
                return aggregatedPoints.OrderBy(p => p.Timestamp);
            }
            catch (Exception ex)
            {
                TrackError(ex.Message);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer?.Dispose();
            _flushToDiskTimer?.Dispose();

            // Final flush to disk
            FlushToDisk(null);
        }
    }
}