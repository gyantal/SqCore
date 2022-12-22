using QuantConnect.Packets;

namespace QuantConnect.Commands
{
    /// <summary>
    /// Contains data held as the result of executing a command
    /// </summary>
    public class CommandResultPacket : Packet
    {
        /// <summary>
        /// Gets or sets the command that produced this packet
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Gets or sets whether or not the
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResultPacket"/> class
        /// </summary>
        public CommandResultPacket(ICommand command, bool success)
            : base(PacketType.CommandResult)
        {
            Success = success;
            CommandName = command.GetType().Name;
        }
    }
}
