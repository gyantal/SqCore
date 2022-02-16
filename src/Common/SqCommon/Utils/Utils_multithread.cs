using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SqCommon
{
    // We should use async-await (TAP: Task-based Asynchronous Pattern), instead of TPL's '.Result/Wait'
    // In the future, base classes (e.g. Microsoft.AspNet.Identity) will only have async API methods, not old-style sync. So use async extensively.
    public static partial class Utils
    {
        // How to run async method synchronously  (see MultithreadTips.txt)
        // Use it rarely. In general it is a bad practice: you should implement both async and sync version of a library and call the sync one without a wrapper.
        // https://stackoverflow.com/questions/5095183/how-would-i-run-an-async-taskt-method-synchronously
        // This confirms that GetResult() will not create AggregateExceptions
        // "int result = BlahAsync().GetAwaiter().GetResult();"
        // >it doesn't swallow exceptions (like Wait)
        // >it won't wrap any exceptions thrown in an AggregateException (like Result)
        // >works for both Task and Task<T>
        //
        // https://stackoverflow.com/questions/9343594/how-to-call-asynchronous-method-from-synchronous-method-in-c
        // If in the future we need improvements to this code: "Microsoft built an AsyncHelper (internal) class to run Async as Sync. The source looks like:"
        // Task.RunSynchronously() is not a solution, because it throws "InvalidOperationException: RunSynchronously may not be called on a task not bound to a delegate, such as the task returned from an asynchronous method."
        // so, RunSynchronously() cannot be called on a Task that is the return of a function.
        public static void TurnAsyncToSyncTask(this Task task)
        {
            task.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

        public static T TurnAsyncToSyncTask<T>(this Task<T> task)
        {
            return task.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

        // run async method in a separate thread and return immediately (in FireAndForget way)
        // Execution in calling thread will continue immedietaly. Even the first instruction of FuncAsync() will be in a separate ThreadPool thread.
        // QueueUserWorkItem is preferred. QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]. see "Task.Run vs ThreadPool.QueueUserWorkItem.txt"
        // However, use this RunInNewThread() wrapper, in case  in the future Task.Run() becomes better or faster, we might switch to that implementation.
        // In rare rare case we have to Wait for completion (Not FireAndForget way), we should use Task.Run instead of ThreadPool.QueueUserWorkItem
        public static void RunInNewThread(WaitCallback function)
        {
            ThreadPool.QueueUserWorkItem(function); // FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        }

        public static void RunInNewThread(WaitCallback function, object? state)
        {
            ThreadPool.QueueUserWorkItem(function, state); // FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        }

        // https://stackoverflow.com/questions/22629951/suppressing-warning-cs4014-because-this-call-is-not-awaited-execution-of-the
        // usage: don't await it. Call "FuncAsync().ContinueInSameThreadButDontWait();"
        // don't do: "await FuncAsync().ContinueInSameThreadButDontWait();"
        // Execution in calling thread will continue until the first inner 'await'. Then FuncAsync() continues where it was called.
        public static void RunInSameThreadButReturnAtFirstAwaitAndLogError(this Task task)
        {
            // task is called without await, so it doesn't wait; it will run parallel. "await task.ContinueWith()" would wait the task

            // Also, without a continuation task After an async func, we get the warning 'Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.'
            // https://stackoverflow.com/questions/14903887/warning-this-call-is-not-awaited-execution-of-the-current-method-continues
            task.ContinueWith(
                t => { Utils.Logger.Error(t.Exception?.ToString() ?? string.Empty); },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        // A Task's exception(s) were not observed either by Waiting on the Task or accessing its Exception property.
        // http://stackoverflow.com/questions/7883052/a-tasks-exceptions-were-not-observed-either-by-waiting-on-the-task-or-accessi
        // Used for long-runnig Tasks with Task.Factory.StartNew() to create non-ThreadPool threads.
        public static Task LogUnobservedTaskExceptions(this Task p_task, string p_msg)
        {
            Utils.Logger.Info("LogUnobservedTaskExceptions().Registering for long running task" + p_msg);
            p_task.ContinueWith(
                t =>
                {
                    AggregateException? aggException = t?.Exception?.Flatten();
                    if (aggException != null)
                    {
                        foreach (var exception in aggException.InnerExceptions)
                            Utils.Logger.Error(exception, "LogUnobservedTaskExceptions().ContinueWithTask(): " + p_msg);
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
            return p_task;
        }

        // "How do I cancel or timeout non-cancelable async operations?" https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        // https://stackoverflow.com/questions/25683980/timeout-pattern-on-task-based-asynchronous-method-in-c-sharp/25684549#25684549
        // if cts.Cancel() is called either by manually or because the delay timeout of cancellationToken expired, then tcs.Task completes,
        // that will terminate (throw OperationCanceledException) the aggregate task in the thread of tcs.Task, not in the thread of the original Task
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>?)s)?.TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            }

            return await task;
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>?)s)?.TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
                else
                    await task;     // this is a double await, lika await await  Task.WhenAny(), but that is fine.
            }
        }
    }
}