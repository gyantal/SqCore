namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Represents a type that is both a bar and base data
    /// </summary>
    public interface IBaseDataBar : IBaseData, IBar
    {
    }
}