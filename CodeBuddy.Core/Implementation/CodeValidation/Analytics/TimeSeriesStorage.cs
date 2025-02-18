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

    public interface ITimeSeriesStorage
    {
        Task StoreDataPointAsync(TimeSeriesDataPoint dataPoint);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime, Dictionary<string, string> tags);
        Task PruneDataAsync(DateTime olderThan);
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