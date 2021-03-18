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

        public virtual void Run()   // try/catch is not necessary, because sqExecution.Run() is wrapped around a try/catch with HealthMonitor notification in SqTrigger.cs
        {
            
        }
    }
}