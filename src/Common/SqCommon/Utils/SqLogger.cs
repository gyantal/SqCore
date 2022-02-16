using System;
using System.Diagnostics;

namespace SqCommon
{
    // with 'this' extension methods on NLog.Logger,
    // we can avoid the inheritance (impossible, because Base class is made by a factor), and
    // we can avoid the Wrapper structure (in which a lot of pipeline code has to be written unnecessarily)
    // https://stackoverflow.com/questions/45311619/proper-way-to-extend-class-method-functionality-using-nlog-as-example
    //
    // Another performance profiling idea: simple: https://pietschsoft.com/post/2015/12/18/code-tip-simpler-performance-timer-logging-in-c
    // Another performance profiling idea: accurate: https://stackoverflow.com/questions/969290/exact-time-measurement-for-performance-testing
    //
    // PotentialToDo: Extending Nlog layout renderer. http://www.carlosjanderson.com/log-the-elapsed-time-between-log-entries-using-nlog/
    // In that way, All logging would show ElapsedTime. But in that simple form it is probably not good, because it would mess it up,
    // because of all multi-threads use the same lastTime. But it can be solved by looking at the CurrentThreadId, and having a Dictionary<threadId, lastTime>.
    public static class SqLogger
    {
        // this works in multithreaded environment, but a local instance of watch is always needed. It profiles inside one function, not  between different callstack levels
        public static void ProfiledInfo(this NLog.Logger logger, string message, Stopwatch watch, bool forceConsoleTarget = false) // Profiled info can go silently to log files
        {
            watch.Stop();
            var msgEx = $"{message} takes {watch.Elapsed.TotalMilliseconds:f3}ms";

            logger.Info(msgEx);
            if (forceConsoleTarget)
                Console.WriteLine(msgEx);

            watch.Restart();
        }

        private static DateTime? g_lastTimeStamp;

        // GProfiling means Global Profiling
        // This can fail in multithread, but passing a local instance of a stopwatch is not required. So, it can profile between different Callstack levels.
        public static void BeginGProfiling(this NLog.Logger logger, bool forceConsoleTarget = false)
        {
            g_lastTimeStamp = DateTime.Now;
            var msgEx = $"BeginGProfiling starts";

            logger.Info(msgEx);
            if (forceConsoleTarget)
                Console.WriteLine(msgEx);
        }

        public static void GProfiledInfo(this NLog.Logger logger, string message, bool forceConsoleTarget = false) // Profiled info can go silently to log files
        {
            var lastTimeStamp = g_lastTimeStamp ?? DateTime.Now;
            var elapsedTime = DateTime.Now - lastTimeStamp;
            var msgEx = $"{message} takes {elapsedTime.TotalMilliseconds:f3}ms";

            logger.Info(msgEx);
            if (forceConsoleTarget)
                Console.WriteLine(msgEx);

            g_lastTimeStamp = DateTime.Now;
        }
    }
}