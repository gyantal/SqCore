﻿using Newtonsoft.Json;
using System.Collections.Generic;
using QuantConnect.Notifications;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Live job task packet: container for any live specific job variables
    /// </summary>
    public class LiveNodePacket : AlgorithmNodePacket
    {
        /// <summary>
        /// Deploy Id for this live algorithm.
        /// </summary>
        [JsonProperty(PropertyName = "sDeployID")]
        public string DeployId = "";

        /// <summary>
        /// String name of the brokerage we're trading with
        /// </summary>
        [JsonProperty(PropertyName = "sBrokerage")]
        public string Brokerage = "";

        /// <summary>
        /// String-String Dictionary of Brokerage Data for this Live Job
        /// </summary>
        [JsonProperty(PropertyName = "aBrokerageData")]
        public Dictionary<string, string> BrokerageData = new Dictionary<string, string>();

        /// <summary>
        /// String name of the DataQueueHandler or LiveDataProvider we're running with
        /// </summary>
        [JsonProperty(PropertyName = "sDataQueueHandler")]
        public string DataQueueHandler = "";

        /// <summary>
        /// String name of the DataChannelProvider we're running with
        /// </summary>
        [JsonProperty(PropertyName = "sDataChannelProvider")]
        public string DataChannelProvider = "";

        /// <summary>
        /// Gets flag indicating whether or not the message should be acknowledged and removed from the queue
        /// </summary>
        [JsonProperty(PropertyName = "DisableAcknowledgement")]
        public bool DisableAcknowledgement;

        /// <summary>
        /// A list of event types to generate notifications for, which will use <see cref="NotificationTargets"/>
        /// </summary>
        [JsonProperty(PropertyName = "aNotificationEvents")]
        public HashSet<string> NotificationEvents;

        /// <summary>
        /// A list of notification targets to use
        /// </summary>
        [JsonProperty(PropertyName = "aNotificationTargets")]
        public List<Notification> NotificationTargets;

        /// <summary>
        /// List of real time data types available in the live trading environment
        /// </summary>
        [JsonProperty(PropertyName = "aLiveDataTypes")]
        public HashSet<string> LiveDataTypes;

        /// <summary>
        /// Default constructor for JSON of the Live Task Packet
        /// </summary>
        public LiveNodePacket()
            : base(PacketType.LiveNode)
        {
            Controls = new Controls
            {
                MinuteLimit = 100,
                SecondLimit = 50,
                TickLimit = 25,
                RamAllocation = 512
            };
        }
    }
}
