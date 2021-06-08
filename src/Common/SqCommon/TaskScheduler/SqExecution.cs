using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon
{
    public enum ExecutionState : byte { NeverStarted, Working, FinishedOk, FinishedError, Unknown };
    public class SqExecution
    {
        public SqTask? SqTask { get; set; }
        public SqTrigger? Trigger { get; set; }
        public ExecutionState BrokerTaskState { get; set; } = ExecutionState.NeverStarted; 

        // try/catch is not necessary, because sqExecution.Run() is wrapped around a try/catch with HealthMonitor notification in SqTrigger.cs
        // but that catch is only triggered if there is no non-awaited async function in it.
        // if there is a non-awaited async method, it returns very quickly, but later it continues in a different threadpool bck thread,
        // but then it loses stacktrace, and exception is not caught in try/catch, but it becomes an AppDomain_BckgThrds_UnhandledException()
        // So, if there is a rare non-awaited async, then try/catch is necessary; otherwise ignore try/catch for simpler code.
        // The surest thing if we want to avoid AppDomain_BckgThrds_UnhandledException() is that there should be separate a try/catch after every async method.
        // see MultithreadTips.txt
        public virtual void Run() // try/catch is only necessary Only if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
        {
            
        }
    }
}