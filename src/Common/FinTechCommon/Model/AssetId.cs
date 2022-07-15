using System;
using System.Diagnostics;

namespace FinTechCommon;

// AssetType: uses the top 5 bits; 32 different values: 0..31
// ID (SubTableId): uses the bottom 27 bits. 134M different values.
// If smaller memory footprint is needed, we can use 16bit uint: 3bits for AssetType=8, 13 bits ID would give 8K values, which would be enough for the 5K USA stocks.
[DebuggerDisplay("AssetTypeID = {AssetTypeID}, SubTableID = {SubTableID} [{((int)AssetTypeID)}:{SubTableID}]")]
public struct AssetId32Bits  : IEquatable<AssetId32Bits>
{
    // signed int would use negative values, and it is represented as binary complement. It complicates things. Less error prone to use unsigned.
    // https://stackoverflow.com/questions/42548277/how-can-i-convert-a-signed-integer-into-an-unsigned-integer
    // In general SQL primary keys are better as unsigned int. Therefore uint is preferable over int.
    public uint m_value;

    public const AssetType AssetTypeMin = (AssetType)(0);
    public const AssetType AssetTypeMax = (AssetType)31;
    public const uint SubTableIdMin = 0;
    public const uint SubTableIdMax =  134217727;

    public const uint Invalid =  0; // invalid value is best to be 0, because it is easy to malloc an array of Invalid (0) objects

    public AssetId32Bits(uint p_value) { m_value = p_value; }
    public AssetId32Bits(IAssetID p_assetID)
    {
        m_value = (p_assetID == null) ? 0 : IntValue(p_assetID.AssetTypeID, p_assetID.ID);
    }
    public AssetId32Bits(AssetType p_type, uint p_subTableId)
    {
        m_value = IntValue(p_type, p_subTableId);
    }

    public AssetId32Bits(string p_typeSubTableStr)  // "2:6" is Type: 2 (stock), StockID: 6 (USO)
    {
        int iColon = p_typeSubTableStr.IndexOf(':');
        if (iColon == -1)
            throw new Exception($"AssetId32Bits cannot find : in '{p_typeSubTableStr}'");

        m_value = IntValue((AssetType)byte.Parse(p_typeSubTableStr[..iColon]), uint.Parse(p_typeSubTableStr.Substring(iColon + 1, p_typeSubTableStr.Length - iColon - 1)));
    }
    public AssetType AssetTypeID        { get { return (AssetType)(m_value >> 27); } }
    public uint SubTableID               { get { return (m_value << 5) >> 5; } }

    // public IAssetID AssetID
    // {
    //     get { return m_intValue == 0 ? null : DBUtils.MakeAssetID(AssetTypeID, SubTableID); }
    // }

    // public override string ToString() { return ToString(null); }
    // string DebugString()
    // {
    //     return ToString(TickerProvider.Singleton);
    // }

    // public string ToString(ITickerProvider p_tp)
    // {
    //     return m_intValue == 0 ? null : DBUtils.GetTicker(p_tp, AssetTypeID, SubTableID);
    // }
    public static implicit operator uint(AssetId32Bits p_this) { return p_this.m_value; }
    public static implicit operator AssetId32Bits(uint p_value) { return new AssetId32Bits(p_value); }
    
    public static uint IntValue(AssetType p_type, uint p_subTableId)
    {
        // checking that input is in the valid range or not. The code was valid for signed int, not for this uint implementation.
        // if (p_type < -16 || 15 < p_type || p_subTableId < -67108864 || 67108863 < p_subTableId)
        //    the following is the same but much faster (12 assembly instructions with 1 branch only):
        // if (0 <= unchecked(((uint)((int)p_type + 16) - (15 + 16 + 1L)) & ((uint)(p_subTableId + 67108864) - (67108863 + 67108864 + 1L)) ))
        //     throw new ArgumentOutOfRangeException();
        return ((uint)p_type << 27) | (p_subTableId & 0x07ffffff);
    }

    bool IEquatable<AssetId32Bits>.Equals(AssetId32Bits other)
    {
        return m_value == other.m_value;
    }

    public override bool Equals(object? obj)
    {
        return obj is AssetId32Bits bits && ((IEquatable<AssetId32Bits>)this).Equals(bits);
    }

    public static bool operator ==(AssetId32Bits left, AssetId32Bits right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AssetId32Bits left, AssetId32Bits right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        return m_value.GetHashCode();
    }

    public override string ToString()
    {
        return $"{((int)AssetTypeID)}:{SubTableID}";
    }
}


// Not used at the moment; but it might be a good idea to have different classes StockID/OptionID/FuturesID
/// <summary> IMPORTANT:
/// - GetHashCode() must be equivalent to DBUtils.GetHashCodeOfIAssetID(this);
/// - Equals(obj) must be equivalent to 0==CompareTo(this,obj as IAssetID);
/// - Do NOT use reference-equality (e.g. assetID1 == assetID2)!
/// Although IAssetIDs are pooled, other classes may also implement 
/// this interface (e.g. IAssetWeight, PortfolioItemSpec). </summary>
// in this way we can have different classes for Stock/Options/Indexes. All deriving from IAssetID. Or we can just use AssetId32Bits in code, without differentiating them.
public interface IAssetID : IComparable<IAssetID>
{
    AssetType AssetTypeID { get; }
    uint ID { get; }                     // AssetSubTableID
}

// internal interface IMinimalAssetID : IAssetID // used as marker for minimal IAssetID implementations
// {
// }

// struct StockID : IMinimalAssetID
// {
//     uint m_id;
//     public StockID(uint p_id)            { m_id = p_id; }
//     public uint ID                       { get { return m_id; } }
//     public AssetType AssetTypeID        { get { return AssetType.Stock; } }
//     public override int GetHashCode()   { return m_id.GetHashCode(); }    // equivalent to DBUtils.GetHashCodeOfIAssetID()
//     // public override string ToString()   { return DBUtils.DefaultAssetIDString(AssetType.Stock, m_id); }

//     public int CompareTo(IAssetID p_other)
//     {
//         throw new NotImplementedException();
//         //return DBUtils.NonBoxingCompare(this, p_other);
//     }
//     public override bool Equals(object obj)
//     {
//         IAssetID? other = obj as IAssetID;
//         return other != null && other.AssetTypeID == AssetType.Stock && other.ID == m_id;
//     }
// }