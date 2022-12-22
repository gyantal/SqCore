namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Parameters required to initialize a <see cref="IMessagingHandler"/> instance
    /// </summary>
    public class MessagingHandlerInitializeParameters
    {
        /// <summary>
        /// The api instance to use
        /// </summary>
        public IApi Api { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="api">The api instance to use</param>
        public MessagingHandlerInitializeParameters(IApi api)
        {
            Api = api;
        }
    }
}
