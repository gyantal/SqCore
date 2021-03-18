using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon
{
    // If a SqTask specifies a Task that runs every 5 minutes, many parallel SqExecution of the same Type/Schema can exist at the same time
    public class SqTask
    {
        public string Name { get; set; } = string.Empty;
        string m_nameForTextToSpeech = string.Empty;
        public string NameForTextToSpeech  // "UberVXX" is said as 'bsxxx' by Twilio on the phonecall, which is unrecognisable
        {
            get { return (String.IsNullOrEmpty(m_nameForTextToSpeech)) ? Name : m_nameForTextToSpeech; }
            set { m_nameForTextToSpeech = value; }
        }
        public List<SqTrigger> Triggers { get; set; } = new List<SqTrigger>();

        public Dictionary<object, object> Settings { get; set; } = new Dictionary<object, object>();

        public List<SqExecution> BrokerTasks { get; set; } = new List<SqExecution>();      // if we Execute the task every 5 minutes than these 2 executions can live in parallel

        public Func<SqExecution> ExecutionFactory { get; set; } = () => new SqExecution();

        public SqTask()
        {
        }
    }
}