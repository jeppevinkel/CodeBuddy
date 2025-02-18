using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public interface ITimeSeriesStorage
    {
        Task StoreDataPointAsync(TimeSeriesDataPoint dataPoint);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime);
        Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime, Dictionary<string, string> tags);
        Task PruneDataAsync(DateTime olderThan);
    }

    public class TimeSeriesStorage : ITimeSeriesStorage
    {
        private readonly IList<TimeSeriesDataPoint> _dataPoints;
        private readonly object _lock = new object();
        private readonly int _maxDataPoints;

        public TimeSeriesStorage(int maxDataPoints = 1000000)
        {
            _dataPoints = new List<TimeSeriesDataPoint>();
            _maxDataPoints = maxDataPoints;
        }

        public Task StoreDataPointAsync(TimeSeriesDataPoint dataPoint)
        {
            lock (_lock)
            {
                _dataPoints.Add(dataPoint);

                // Ensure we don't exceed max data points by removing oldest entries
                while (_dataPoints.Count > _maxDataPoints)
                {
                    _dataPoints.RemoveAt(0);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(DateTime startTime, DateTime endTime)
        {
            IEnumerable<TimeSeriesDataPoint> result;

            lock (_lock)
            {
                result = _dataPoints
                    .Where(dp => dp.Timestamp >= startTime && dp.Timestamp <= endTime)
                    .OrderBy(dp => dp.Timestamp)
                    .ToList();
            }

            return Task.FromResult(result);
        }

        public Task<IEnumerable<TimeSeriesDataPoint>> GetDataPointsAsync(
            DateTime startTime,
            DateTime endTime,
            Dictionary<string, string> tags)
        {
            IEnumerable<TimeSeriesDataPoint> result;

            lock (_lock)
            {
                result = _dataPoints
                    .Where(dp => dp.Timestamp >= startTime &&
                                dp.Timestamp <= endTime &&
                                tags.All(t => dp.Tags.ContainsKey(t.Key) && dp.Tags[t.Key] == t.Value))
                    .OrderBy(dp => dp.Timestamp)
                    .ToList();
            }

            return Task.FromResult(result);
        }

        public Task PruneDataAsync(DateTime olderThan)
        {
            lock (_lock)
            {
                var indexesToRemove = _dataPoints
                    .Select((dp, index) => new { DataPoint = dp, Index = index })
                    .Where(x => x.DataPoint.Timestamp < olderThan)
                    .Select(x => x.Index)
                    .OrderByDescending(i => i)
                    .ToList();

                foreach (var index in indexesToRemove)
                {
                    _dataPoints.RemoveAt(index);
                }
            }

            return Task.CompletedTask;
        }
    }
}