using System;

namespace QuantConnect.Parameters
{
    public class SqBacktestConfig
    {
        public bool DoUseIbFeeModelForEquities { get; set; } = false;
        public bool DoUseBenchmark { get; set; } = false;
        public bool DoGenerateDrawdownChart { get; set; } = false;
        public bool DoGenerateLog { get; set; } = false;

        public float SqInitialDeposit { get; set; }
        public DateTime SqStartDate { get; set; } = DateTime.MinValue;
        public DateTime SqEndDate { get; set; } = DateTime.MinValue;

        public string SqStrategyParams { get; set; } = string.Empty;
    }
}