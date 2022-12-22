using System;
using System.Collections.Concurrent;
using System.Threading;
using SqCommon;

namespace QuantConnect.Util
{
    /// <summary>
    /// This worker tread is required to guarantee all python operations are
    /// executed by the same thread, to enable complete debugging functionality.
    /// We don't use the main thread, to avoid any chance of blocking the process
    /// </summary>
    public class WorkerThread : IDisposable
    {
        private readonly BlockingCollection<Action> _blockingCollection;
        private readonly CancellationTokenSource _threadCancellationTokenSource;
        private readonly Thread _workerThread;

        /// <summary>
        /// The worker thread instance
        /// </summary>
        // public static WorkerThread Instance = new WorkerThread();

        /// <summary>
        /// Will be set when the worker thread finishes a work item
        /// </summary>
        public AutoResetEvent FinishedWorkItem { get; }

        /// <summary>
        /// Creates a new instance, which internally launches a new worker thread
        /// </summary>
        /// <remarks><see cref="Dispose"/></remarks>
        public WorkerThread()
        {
            _threadCancellationTokenSource = new CancellationTokenSource();
            FinishedWorkItem = new AutoResetEvent(false);
            _blockingCollection = new BlockingCollection<Action>();
            _workerThread = new Thread(() =>
            {
                try
                {
                    foreach (var action in _blockingCollection.GetConsumingEnumerable(_threadCancellationTokenSource.Token))
                    {
                        FinishedWorkItem.Reset();
                        try
                        {
                            action();
                        }
                        catch (Exception exception)
                        {
                            Utils.Logger.Error(exception, "WorkerThread(): exception thrown when running task");
                        }
                        FinishedWorkItem.Set();
                    }
                }
                catch (OperationCanceledException)
                {
                    // pass, when the token gets cancelled. It is normal, when Main thread calls workerThread?.Dispose(); after Engine.Run()
                }
            })
            {
                IsBackground = true,
                Name = "Isolator Thread",
                Priority = ThreadPriority.Highest
            };
            _workerThread.Start();
        }

        /// <summary>
        /// Adds a new item of work
        /// </summary>
        /// <param name="action">The work item to add</param>
        public void Add(Action action)
        {
            _blockingCollection.Add(action);
        }

        /// <summary>
        /// Disposes the worker thread.
        /// </summary>
        /// <remarks>Note that the worker thread is a background thread,
        /// so it won't block the process from terminating even if not disposed</remarks>
        public void Dispose()
        {
            try
            {
                _blockingCollection.CompleteAdding();
                _workerThread.StopSafely(TimeSpan.FromMilliseconds(50), _threadCancellationTokenSource);
                _threadCancellationTokenSource.DisposeSafely();
            }
            catch (Exception exception)
            {
                Utils.Logger.Error(exception);
            }
        }
    }
}
