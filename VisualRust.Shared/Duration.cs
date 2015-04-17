using System;
using System.Diagnostics;

namespace VisualRust.Shared
{
    public static class Duration
    {
        public static TimeSpan MeasureAndPrint(Action action, string taskname, int numberOfTimes = 1, Action after = null)
        {
            return MeasureAndPrint(_ => action(), taskname, numberOfTimes, after);
        }

        public static TimeSpan MeasureAndPrint(Action<int> action, string taskname, int numberOfTimes = 1, Action after = null)
        {
            TimeSpan duration = Measure(action, numberOfTimes, after);
            Debug.WriteLine("##############################################");
            Debug.WriteLine(taskname + " took " + duration.Seconds + " s, " + duration.Milliseconds + " ms or " + duration.Ticks + " ticks");
            return duration;
        }

        public static TimeSpan Measure(Action action, int numberOfTimes = 1, Action after = null)
        {
            return Measure(_ => action(), numberOfTimes, after);
        }

        public static TimeSpan Measure(Action<int> action, int numberOfTimes = 1, Action after = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < numberOfTimes; i++)
                action(i);
            if (after != null)
                after();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }
}
