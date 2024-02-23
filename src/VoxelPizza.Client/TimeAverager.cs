using System;
using System.Diagnostics;

namespace VoxelPizza.Client
{
    public enum TimeAveragerEntry
    {
        Update,
        Draw
    }

    public class TimeAverager
    {
        public static readonly double MillisPerTick = 1 / (double)Stopwatch.Frequency * 1000;
        public static readonly double SecondsPerTick = 1 / (double)Stopwatch.Frequency;

        private int[] _tickCounts;
        private int _tickIndex = 0;
        private double _measureDelay;
        private double _tickStamp;
        private double _elapsedTime;
        private int _tickCount = 0;

        private double[] _updateTimes;
        private double _updateTime;
        private double _updateStamp;

        private double[] _drawTimes;
        private double _drawTime;
        private double _drawStamp;

        private double[] _presentTimes;
        private double _presentTime;
        private double _presentStamp;

        public double AverageUpdateSeconds
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _updateTimes.Length; i++)
                    sum += _updateTimes[i];
                return sum / _updateTimes.Length;
            }
        }

        public double AverageDrawSeconds
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _drawTimes.Length; i++)
                    sum += _drawTimes[i];
                return sum / _drawTimes.Length;
            }
        }

        public double AveragePresentSeconds
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _presentTimes.Length; i++)
                    sum += _presentTimes[i];
                return sum / _presentTimes.Length;
            }
        }

        public double AverageTicksPerSecond
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _tickCounts.Length; i++)
                    sum += _tickCounts[i];
                return sum / _tickCounts.Length / _measureDelay;
            }
        }

        public TimeAverager(int sampleCount, TimeSpan measureDelay)
        {
            if (sampleCount < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (measureDelay.Ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(measureDelay));

            _measureDelay = measureDelay.TotalSeconds;
            _updateTimes = new double[sampleCount];
            _drawTimes = new double[sampleCount];
            _presentTimes = new double[sampleCount];
            _tickCounts = new int[sampleCount];
        }

        public void BeginUpdate()
        {
            _updateStamp = GetTimestampSeconds();
        }

        public void BeginDraw()
        {
            _drawStamp = GetTimestampSeconds();
        }

        public void BeginPresent()
        {
            _presentStamp = GetTimestampSeconds();
        }

        public void EndUpdate()
        {
            double diff = GetTimestampSeconds() - _updateStamp;
            _updateTime += diff;
        }

        public void EndDraw()
        {
            double diff = GetTimestampSeconds() - _drawStamp;
            _drawTime += diff;
        }

        public void EndPresent()
        {
            double diff = GetTimestampSeconds() - _presentStamp;
            _presentTime += diff;
        }

        public void Tick()
        {
            double current = GetTimestampSeconds();
            double diff = current - _tickStamp;
            _tickStamp = current;

            _elapsedTime += diff;
            _tickCount++;

            if (_elapsedTime >= _measureDelay)
            {
                _updateTimes[_tickIndex] = _updateTime / _tickCount;
                _drawTimes[_tickIndex] = _drawTime / _tickCount;
                _presentTimes[_tickIndex] = _presentTime / _tickCount;
                _tickCounts[_tickIndex] = _tickCount;

                _tickIndex = (_tickIndex + 1) % _tickCounts.Length;
                _updateTime = 0;
                _drawTime = 0;
                _presentTime = 0;
                _tickCount = 0;
                _elapsedTime = 0;
            }
        }

        public static double GetTimestampSeconds()
        {
            return Stopwatch.GetTimestamp() * SecondsPerTick;
        }
    }
}
