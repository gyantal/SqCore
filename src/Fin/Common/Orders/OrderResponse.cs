using static QuantConnect.StringExtensions;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Represents a response to an <see cref="OrderRequest"/>. See <see cref="OrderRequest.Response"/> property for
    /// a specific request's response value
    /// </summary>
    public class OrderResponse
    {
        /// <summary>
        /// Gets the order id
        /// </summary>
        public int OrderId
        {
            get; private set;
        }

        /// <summary>
        /// Gets the error message if the <see cref="ErrorCode"/> does not equal <see cref="OrderResponseErrorCode.None"/>, otherwise
        /// gets <see cref="string.Empty"/>
        /// </summary>
        public string ErrorMessage
        {
            get; private set;
        }

        /// <summary>
        /// Gets the error code for this response.
        /// </summary>
        public OrderResponseErrorCode ErrorCode
        {
            get; private set;
        }

        /// <summary>
        /// Gets true if this response represents a successful request, false otherwise
        /// If this is an unprocessed response, IsSuccess will return false.
        /// </summary>
        public bool IsSuccess
        {
            get { return IsProcessed && !IsError; }
        }

        /// <summary>
        /// Gets true if this response represents an error, false otherwise
        /// </summary>
        public bool IsError
        {
            get { return IsProcessed && ErrorCode != OrderResponseErrorCode.None; }
        }

        /// <summary>
        /// Gets true if this response has been processed, false otherwise
        /// </summary>
        public bool IsProcessed
        {
            get { return this != Unprocessed; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderResponse"/> class
        /// </summary>
        /// <param name="orderId">The order id</param>
        /// <param name="errorCode">The error code of the response, specify <see cref="OrderResponseErrorCode.None"/> for no error</param>
        /// <param name="errorMessage">The error message, applies only if the <paramref name="errorCode"/> does not equal <see cref="OrderResponseErrorCode.None"/></param>
        private OrderResponse(int orderId, OrderResponseErrorCode errorCode, string errorMessage)
        {
            OrderId = orderId;
            ErrorCode = errorCode;
            if (errorCode != OrderResponseErrorCode.None)
            {
                ErrorMessage = errorMessage ?? "An unexpected error occurred.";
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (this == Unprocessed)
            {
                return "Unprocessed";
            }

            if (IsError)
            {
                return Invariant($"Error: {ErrorCode} - {ErrorMessage}");
            }

            return "Success";
        }

        #region Statics - implicit(int), Unprocessed constant, reponse factory methods

        /// <summary>
        /// Gets an <see cref="OrderResponse"/> for a request that has not yet been processed
        /// </summary>
        public static readonly OrderResponse Unprocessed = new OrderResponse(int.MinValue, OrderResponseErrorCode.None, "The request has not yet been processed.");

        /// <summary>
        /// Helper method to create a successful response from a request
        /// </summary>
        public static OrderResponse Success(OrderRequest request)
        {
            return new OrderResponse(request.OrderId, OrderResponseErrorCode.None, null);
        }

        /// <summary>
        /// Helper method to create an error response from a request
        /// </summary>
        public static OrderResponse Error(OrderRequest request, OrderResponseErrorCode errorCode, string errorMessage)
        {
            return new OrderResponse(request.OrderId, errorCode, errorMessage);
        }

        /// <summary>
        /// Helper method to create an error response due to an invalid order status
        /// </summary>
        public static OrderResponse InvalidStatus(OrderRequest request, Order order)
        {
            return Error(request, OrderResponseErrorCode.InvalidOrderStatus,
                Invariant($"Unable to update order with id {request.OrderId} because it already has {order.Status} status.")
            );
        }

        /// <summary>
        /// Helper method to create an error response due to the "New" order status
        /// </summary>
        public static OrderResponse InvalidNewStatus(OrderRequest request, Order order)
        {
            return Error(request, OrderResponseErrorCode.InvalidNewOrderStatus,
                Invariant($"Unable to update or cancel order with id {request.OrderId} and status {order.Status} because the submit confirmation has not been received yet.")
            );
        }

        /// <summary>
        /// Helper method to create an error response due to a bad order id
        /// </summary>
        public static OrderResponse UnableToFindOrder(OrderRequest request)
        {
            return Error(request, OrderResponseErrorCode.UnableToFindOrder,
                Invariant($"Unable to locate order with id {request.OrderId}.")
            );
        }

        /// <summary>
        /// Helper method to create an error response due to a zero order quantity
        /// </summary>
        public static OrderResponse ZeroQuantity(OrderRequest request)
        {
            return Error(request, OrderResponseErrorCode.OrderQuantityZero,
                Invariant($"Unable to {request.OrderRequestType.ToLower()} order with id {request.OrderId} that has zero quantity.")
            );
        }

        /// <summary>
        /// Helper method to create an error response due to algorithm still in warmup mode
        /// </summary>
        public static OrderResponse WarmingUp(OrderRequest request)
        {
            return Error(request, OrderResponseErrorCode.AlgorithmWarmingUp,
                Invariant($"This operation is not allowed in Initialize or during warm up: OrderRequest.{request.OrderRequestType}. ") +
                "Please move this code to the OnWarmupFinished() method."
            );
        }

        #endregion
    }
}
