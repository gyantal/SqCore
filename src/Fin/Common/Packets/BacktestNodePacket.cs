using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using QuantConnect.Securities;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Algorithm backtest task information packet.
    /// </summary>
    public class BacktestNodePacket : AlgorithmNodePacket
    {
        // default random id, static so its one per process
        private static readonly string DefaultId
            = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        /// <summary>
        /// Name of the backtest as randomly defined in the IDE.
        /// </summary>
        [JsonProperty(PropertyName = "sName")]
        public string Name = "";

        /// <summary>
        /// BacktestId / Algorithm Id for this task
        /// </summary>
        [JsonProperty(PropertyName = "sBacktestID")]
        public string BacktestId = DefaultId;

        /// <summary>
        /// Optimization Id for this task
        /// </summary>
        [JsonProperty(PropertyName = "sOptimizationID")]
        public string OptimizationId;

        /// <summary>
        /// Backtest start-date as defined in the Initialize() method.
        /// </summary>
        [JsonProperty(PropertyName = "dtPeriodStart")]
        public DateTime? PeriodStart;

        /// <summary>
        /// Backtest end date as defined in the Initialize() method.
        /// </summary>
        [JsonProperty(PropertyName = "dtPeriodFinish")]
        public DateTime? PeriodFinish;

        /// <summary>
        /// Estimated number of trading days in this backtest task based on the start-end dates.
        /// </summary>
        [JsonProperty(PropertyName = "iTradeableDates")]
        public int TradeableDates = 0;

        /// <summary>
        /// True, if this is a debugging backtest
        /// </summary>
        [JsonProperty(PropertyName = "bDebugging")]
        public bool IsDebugging;

        /// <summary>
        /// Optional initial cash amount if set
        /// </summary>
        public CashAmount? CashAmount;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public BacktestNodePacket()
            : base(PacketType.BacktestNode)
        {
            Controls = new Controls
            {
                MinuteLimit = 500,
                SecondLimit = 100,
                TickLimit = 30
            };
        }

        /// <summary>
        /// Initialize the backtest task packet.
        /// </summary>
        public BacktestNodePacket(int userId, int projectId, string sessionId, byte[] algorithmData, decimal startingCapital, string name)
            : this (userId, projectId, sessionId, algorithmData, name, new CashAmount(startingCapital, Currencies.USD))
        {
        }

        /// <summary>
        /// Initialize the backtest task packet.
        /// </summary>
        public BacktestNodePacket(int userId, int projectId, string sessionId, byte[] algorithmData, string name, CashAmount? startingCapital = null)
            : base(PacketType.BacktestNode)
        {
            UserId = userId;
            Algorithm = algorithmData;
            SessionId = sessionId;
            ProjectId = projectId;
            Name = name;
            CashAmount = startingCapital;
            Language = Language.CSharp;
            Controls = new Controls
            {
                MinuteLimit = 500,
                SecondLimit = 100,
                TickLimit = 30
            };
        }
    }
}
