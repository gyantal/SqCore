using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SqCommon
{
    public static partial class Utils
    {
        // https://stackoverflow.com/questions/22629951/suppressing-warning-cs4014-because-this-call-is-not-awaited-execution-of-the
        public static void FireParallelAndForgetAndLogErrorTask(this Task task)
        {
            // task is called without await, so it doesn't wait; it will run parallel. "await task.ContinueWith()" would wait the task
            task.ContinueWith(
                t => { Utils.Logger.Error(t.Exception.ToString()); },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void TurnAsyncToSyncTask(this Task task)
        {   // RunSynchronously may not be called on a task not bound to a delegate, such as the task returned from an asynchronous method.
            // So for asynch Methods, use Wait(), or use ConfigureAwait() + GetResult() which is Explicit wait too.
            // https://stackoverflow.com/questions/14485115/synchronously-waiting-for-an-async-operation-and-why-does-wait-freeze-the-pro
            task.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

        // "How do I cancel or timeout non-cancelable async operations?" https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        // https://stackoverflow.com/questions/25683980/timeout-pattern-on-task-based-asynchronous-method-in-c-sharp/25684549#25684549
        // if cts.Cancel() is called either by manually or because the delay timeout of cancellationToken expired, then tcs.Task completes,
        // that will terminate (throw OperationCanceledException) the aggregate task in the thread of tcs.Task, not in the thread of the original Task
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            return await task;
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
                else 
                    await task;     // this is a double await, lika await await  Task.WhenAny(), but that is fine.
        }

    }
}