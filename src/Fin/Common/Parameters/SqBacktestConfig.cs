using System;

namespace QuantConnect.Parameters
{
    public enum SqResult { QcOriginal, SqPvOnly, SqDetailed }

    public class SqBacktestConfig
    {
        public bool DoUseIbFeeModelForEquities { get; set; } = false;
        public SqResult SqResult { get; set; } = SqResult.QcOriginal; // Lightweight result calculation, only what SqCore needs, and additional stat numbers that QC doesn't calculate

        public bool DoGenerateLog { get; set; } = false;

        public float SqInitialDeposit { get; set; }
        public DateTime SqStartDate { get; set; } = DateTime.MinValue;
        public DateTime SqEndDate { get; set; } = DateTime.MinValue;

        public string SqStrategyParams { get; set; } = string.Empty;
    }
}