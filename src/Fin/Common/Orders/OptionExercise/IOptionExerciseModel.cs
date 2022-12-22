using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using System.Collections.Generic;

namespace QuantConnect.Orders.OptionExercise
{
    /// <summary>
    /// Represents a model that simulates option exercise and lapse events
    /// </summary>
    public interface IOptionExerciseModel
    {

        /// <summary>
        /// Model the option exercise 
        /// </summary>
        /// <param name="option">Option we're trading this order</param>
        /// <param name="order">Order to update</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        IEnumerable<OrderEvent> OptionExercise(Option option, OptionExerciseOrder order);

    }
}
