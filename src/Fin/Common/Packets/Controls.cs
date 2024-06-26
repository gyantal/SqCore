using System.IO;
using Newtonsoft.Json;
using QuantConnect.Interfaces;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Specifies values used to control algorithm limits
    /// </summary>
    public class Controls
    {
        /// <summary>
        /// The maximum runtime in minutes
        /// </summary>
        [JsonProperty(PropertyName = "iMaximumRuntimeMinutes")]
        public int MaximumRuntimeMinutes;

        /// <summary>
        /// The maximum number of minute symbols
        /// </summary>
        [JsonProperty(PropertyName = "iMinuteLimit")]
        public int MinuteLimit;

        /// <summary>
        /// The maximum number of second symbols
        /// </summary>
        [JsonProperty(PropertyName = "iSecondLimit")]
        public int SecondLimit;

        /// <summary>
        /// The maximum number of tick symbol
        /// </summary>
        [JsonProperty(PropertyName = "iTickLimit")]
        public int TickLimit;

        /// <summary>
        /// Ram allocation for this algorithm in MB
        /// </summary>
        [JsonProperty(PropertyName = "iMaxRamAllocation")]
        public int RamAllocation;

        /// <summary>
        /// CPU allocation for this algorithm
        /// </summary>
        [JsonProperty(PropertyName = "dMaxCpuAllocation")]
        public decimal CpuAllocation;

        /// <summary>
        /// The user backtesting log limit
        /// </summary>
        [JsonProperty(PropertyName = "iBacktestLogLimit")]
        public int BacktestLogLimit;

        /// <summary>
        /// The daily log limit of a user
        /// </summary>
        [JsonProperty(PropertyName = "iDailyLogLimit")]
        public int DailyLogLimit;

        /// <summary>
        /// The remaining log allowance for a user
        /// </summary>
        [JsonProperty(PropertyName = "iRemainingLogAllowance")]
        public int RemainingLogAllowance;

        /// <summary>
        /// Maximimum number of insights we'll store and score in a single backtest
        /// </summary>
        [JsonProperty(PropertyName = "iBacktestingMaxInsights")]
        public int BacktestingMaxInsights;

        /// <summary>
        /// Maximimum number of orders we'll allow in a backtest.
        /// </summary>
        [JsonProperty(PropertyName = "iBacktestingMaxOrders")]
        public int BacktestingMaxOrders { get; set; }

        /// <summary>
        /// Limits the amount of data points per chart series. Applies only for backtesting
        /// </summary>
        [JsonProperty(PropertyName = "iMaximumDataPointsPerChartSeries")]
        public int MaximumDataPointsPerChartSeries;

        /// <summary>
        /// The amount seconds used for timeout limits
        /// </summary>
        [JsonProperty(PropertyName = "iSecondTimeOut")]
        public int SecondTimeOut;

        /// <summary>
        /// Sets parameters used for determining the behavior of the leaky bucket algorithm that
        /// controls how much time is available for an algorithm to use the training feature.
        /// </summary>
        [JsonProperty(PropertyName = "oTrainingLimits")]
        public LeakyBucketControlParameters TrainingLimits;

        /// <summary>
        /// Limits the total size of storage used by <see cref="IObjectStore"/>
        /// </summary>
        [JsonProperty(PropertyName = "storageLimit")]
        public long StorageLimit;

        /// <summary>
        /// Limits the number of files to be held under the <see cref="IObjectStore"/>
        /// </summary>
        [JsonProperty(PropertyName = "storageFileCount")]
        public int StorageFileCount;

        /// <summary>
        /// Holds the permissions for the object store
        /// </summary>
        [JsonProperty(PropertyName = "storagePermissions")]
        public FileAccess StoragePermissions;

        /// <summary>
        /// The interval over which the <see cref="IObjectStore"/> will persistence the contents of
        /// the object store
        /// </summary>
        [JsonProperty(PropertyName = "persistenceIntervalSeconds")]
        public int PersistenceIntervalSeconds;

        /// <summary>
        /// The cost associated with running this job
        /// </summary>
        [JsonProperty(PropertyName = "dCreditCost")]
        public decimal CreditCost;

        /// <summary>
        /// Initializes a new default instance of the <see cref="Controls"/> class
        /// </summary>
        public Controls()
        {
            MinuteLimit = 500;
            SecondLimit = 100;
            TickLimit = 30;
            RamAllocation = 1024;
            BacktestLogLimit = 10000;
            BacktestingMaxOrders = int.MaxValue;
            DailyLogLimit = 3000000;
            RemainingLogAllowance = 10000;
            MaximumRuntimeMinutes = 60 * 24 * 100; // 100 days default
            BacktestingMaxInsights = 10000;
            MaximumDataPointsPerChartSeries = 4000;
            SecondTimeOut = 300;
            StorageLimit = 10737418240;
            StorageFileCount = 10000;
            PersistenceIntervalSeconds = 5;
            StoragePermissions = FileAccess.ReadWrite;

            // initialize to default leaky bucket values in case they're not specified
            TrainingLimits = new LeakyBucketControlParameters();
        }
    }
}
