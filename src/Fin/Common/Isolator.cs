using System;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Parameters;
using QuantConnect.Util;
using SqCommon;
using static QuantConnect.StringExtensions;

namespace QuantConnect
{
    /// <summary>
    /// Isolator class - create a new instance of the algorithm and ensure it doesn't
    /// exceed memory or time execution limits.
    /// </summary>
    public class Isolator
    {
        /// <summary>
        /// Algo cancellation controls - cancel source.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource
        {
            get; private set;
        }

        /// <summary>
        /// Algo cancellation controls - cancellation token for algorithm thread.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return CancellationTokenSource.Token; }
        }

        /// <summary>
        /// Check if this task isolator is cancelled, and exit the analysis
        /// </summary>
        public bool IsCancellationRequested
        {
            get { return CancellationTokenSource.IsCancellationRequested; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Isolator"/> class
        /// </summary>
        public Isolator()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Execute a code block with a maximum limit on time and memory.
        /// </summary>
        /// <param name="timeSpan">Timeout in timespan</param>
        /// <param name="withinCustomLimits">Function used to determine if the codeBlock is within custom limits, such as with algorithm manager
        /// timing individual time loops, return a non-null and non-empty string with a message indicating the error/reason for stoppage</param>
        /// <param name="codeBlock">Action codeblock to execute</param>
        /// <param name="memoryCap">Maximum memory allocation, default 1024Mb</param>
        /// <param name="sleepIntervalMillis">Sleep interval between each check in ms</param>
        /// <param name="workerThread">The worker thread instance that will execute the provided action, if null
        /// will use a <see cref="Task"/></param>
        /// <returns>True if algorithm exited successfully, false if cancelled because it exceeded limits.</returns>
        public bool ExecuteWithTimeLimit(TimeSpan timeSpan, Func<IsolatorLimitResult> withinCustomLimits, Action codeBlock, long memoryCap = 1024, int sleepIntervalMillis = 1000, WorkerThread workerThread = null)
        {
            // SqCore Change NEW:
            if (SqBacktestConfig.SqFastestExecution)
                throw new Exception("In SqCore running, for speed, we don't want to run any worker thread tasks. Just run them in the caller thread directly.");
            // SqCore Change END
            workerThread?.Add(codeBlock);

            // SqCore Change NEW:
            if (SqBacktestConfig.SqFastestExecution)
            {
                Task taskWt = Task.Factory.StartNew(() => workerThread.FinishedWorkItem.WaitOne(), CancellationTokenSource.Token);
                taskWt.Wait();
                return true;
            }
            // SqCore Change END

            var task = workerThread == null
                //Launch task
                ? Task.Factory.StartNew(codeBlock, CancellationTokenSource.Token)
                // wrapper task so we can reuse MonitorTask
                : Task.Factory.StartNew(() => workerThread.FinishedWorkItem.WaitOne(), CancellationTokenSource.Token);
            try
            {
                return MonitorTask(task, timeSpan, withinCustomLimits, memoryCap, sleepIntervalMillis);
            }
            catch (Exception)
            {
                if (!task.IsCompleted)
                {
                    // lets free the wrapper task even if the worker thread didn't finish
                    workerThread?.FinishedWorkItem.Set();
                }
                throw;
            }
        }

        private bool MonitorTask(Task task,
            TimeSpan timeSpan,
            Func<IsolatorLimitResult> withinCustomLimits,
            long memoryCap = 1024,
            int sleepIntervalMillis = 1000)
        {
            // default to always within custom limits
            withinCustomLimits = withinCustomLimits ?? (() => new IsolatorLimitResult(TimeSpan.Zero, string.Empty));

            var message = string.Empty;
            var emaPeriod = 60d;
            var memoryUsed = 0L;
            var utcNow = DateTime.UtcNow;
            var end = utcNow + timeSpan;
            var memoryLogger = utcNow + Time.OneMinute;
            var isolatorLimitResult = new IsolatorLimitResult(TimeSpan.Zero, string.Empty);

            //Convert to bytes
            memoryCap *= 1024 * 1024;
            var spikeLimit = memoryCap*2;

            while (!task.IsCompleted && utcNow < end)
            {
                // if over 80% allocation force GC then sample
                var sample = Convert.ToDouble(GC.GetTotalMemory(memoryUsed > memoryCap * 0.8));

                // find the EMA of the memory used to prevent spikes killing stategy
                memoryUsed = Convert.ToInt64((emaPeriod-1)/emaPeriod * memoryUsed + (1/emaPeriod)*sample);

                // if the rolling EMA > cap; or the spike is more than 2x the allocation.
                if (memoryUsed > memoryCap || sample > spikeLimit)
                {
                    message = $"Execution Security Error: Memory Usage Maxed Out - {PrettyFormatRam(memoryCap)}MB max, " +
                              $"with last sample of {PrettyFormatRam((long) sample)}MB.";
                    break;
                }

                if (utcNow > memoryLogger)
                {
                    if (memoryUsed > memoryCap * 0.8)
                    {
                        Utils.Logger.Error(Invariant($"Execution Security Error: Memory usage over 80% capacity. Sampled at {sample}"));
                    }

                    Utils.Logger.Trace("Isolator.ExecuteWithTimeLimit(): " +
                              $"Used: {PrettyFormatRam(memoryUsed)}, " +
                              $"Sample: {PrettyFormatRam((long)sample)}, " +
                              $"App: {PrettyFormatRam(OS.ApplicationMemoryUsed * 1024 * 1024)}, " +
                              Invariant($"CurrentTimeStepElapsed: {isolatorLimitResult.CurrentTimeStepElapsed:mm':'ss'.'fff}. ") +
                              $"CPU: {(int)Math.Ceiling(OS.CpuUsage)}%");

                    memoryLogger = utcNow.AddMinutes(1);
                }

                // check to see if we're within other custom limits defined by the caller
                isolatorLimitResult = withinCustomLimits();
                if (!isolatorLimitResult.IsWithinCustomLimits)
                {
                    message = isolatorLimitResult.ErrorMessage;
                    break;
                }

                if (task.Wait(utcNow.GetSecondUnevenWait(sleepIntervalMillis)))
                {
                    break;
                }

                utcNow = DateTime.UtcNow;
            }

            if (task.IsCompleted == false && string.IsNullOrEmpty(message))
            {
                message = $"Execution Security Error: Operation timed out - {timeSpan.TotalMinutes.ToStringInvariant()} minutes max. Check for recursive loops.";
                Utils.Logger.Trace($"Isolator.ExecuteWithTimeLimit(): {message}");
            }

            if (!string.IsNullOrEmpty(message))
            {
                CancellationTokenSource.Cancel();
                Utils.Logger.Error($"Security.ExecuteWithTimeLimit(): {message}");
                throw new TimeoutException(message);
            }
            return task.IsCompleted;
        }

        /// <summary>
        /// Execute a code block with a maximum limit on time and memory.
        /// </summary>
        /// <param name="timeSpan">Timeout in timespan</param>
        /// <param name="codeBlock">Action codeblock to execute</param>
        /// <param name="memoryCap">Maximum memory allocation, default 1024Mb</param>
        /// <param name="sleepIntervalMillis">Sleep interval between each check in ms</param>
        /// <param name="workerThread">The worker thread instance that will execute the provided action, if null
        /// will use a <see cref="Task"/></param>
        /// <returns>True if algorithm exited successfully, false if cancelled because it exceeded limits.</returns>
        public bool ExecuteWithTimeLimit(TimeSpan timeSpan, Action codeBlock, long memoryCap, int sleepIntervalMillis = 1000, WorkerThread workerThread = null)
        {
            return ExecuteWithTimeLimit(timeSpan, null, codeBlock, memoryCap, sleepIntervalMillis, workerThread);
        }

        /// <summary>
        /// Convert the bytes to a MB in double format for string display
        /// </summary>
        /// <param name="ramInBytes"></param>
        /// <returns></returns>
        private static string PrettyFormatRam(long ramInBytes)
        {
            return Math.Round(Convert.ToDouble(ramInBytes/(1024*1024))).ToStringInvariant();
        }
    }
}
