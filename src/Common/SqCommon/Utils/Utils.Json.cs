using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqCommon;

public class DoubleJsonConverterToStr : JsonConverter<double> // the number is written as a string, with quotes: "previousClose":"272.48"
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Double.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, double p_value, JsonSerializerOptions options) =>
        writer.WriteStringValue(p_value.ToString("0.####")); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
}

public class DoubleJsonConverterToNumber4D : JsonConverter<double> // the number is written as a number, without quotes: ("previousClose":"272.4800109863281") should be ("previousClose":272.48)
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Double.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, double p_value, JsonSerializerOptions options)
    {
        // https://github.com/dotnet/runtime/issues/31024
        // ArgumentException' in System.Text.Json.dll: '.NET number values such as positive and negative infinity cannot be written as valid JSON.'
        // This is a more complicated scenario as some floating-point values (+infinity, -infinity, and NaN) can't be represented as JSON "numbers".
        // The only way these can be successfully serialized/deserialized is by representing them by using an alternative format (such as strings).
        // Writes ["Infinity","NaN",0.1,1.0002,3.141592653589793]
        // And the following succeeds: JsonConvert.DeserializeObject<double[]>("[\"Infinity\",\"NaN\",0.1,1.0002,3.141592653589793]");
        if (double.IsFinite(p_value))
            writer.WriteNumberValue(Convert.ToDecimal(Math.Round(p_value, 4))); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
        else
            writer.WriteStringValue(p_value.ToString());
    }
}

public class FloatJsonConverterToNumber4D : JsonConverter<float> // the number is written as a number, without quotes: ("previousClose":"272.4800109863281") => ("previousClose":272.48)
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Single.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, float p_value, JsonSerializerOptions options)
    {
        // https://github.com/dotnet/runtime/issues/31024
        // ArgumentException' in System.Text.Json.dll: '.NET number values such as positive and negative infinity cannot be written as valid JSON.'
        // This is a more complicated scenario as some floating-point values (+infinity, -infinity, and NaN) can't be represented as JSON "numbers".
        // The only way these can be successfully serialized/deserialized is by representing them by using an alternative format (such as strings).
        // Writes ["Infinity","NaN",0.1,1.0002,3.141592653589793]
        // And the following succeeds: JsonConvert.DeserializeObject<double[]>("[\"Infinity\",\"NaN\",0.1,1.0002,3.141592653589793]");
        if (float.IsFinite(p_value))
            writer.WriteNumberValue(Convert.ToDecimal(Math.Round(p_value, 4))); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
        else
            writer.WriteStringValue(p_value.ToString());
    }
}

public class FloatListJsonConverterToNumber4D : JsonConverter<List<float>> // the number is written as a number, without quotes: ("previousClose":"272.4800109863281") => ("previousClose":272.48)
{
    public override List<float> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization of List<float> is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, List<float> p_values, JsonSerializerOptions options)
    {
        if (p_values != null)
        {
            writer.WriteStartArray();

            foreach (var value in p_values)
            {
                if (float.IsFinite(value))
                    writer.WriteNumberValue(Convert.ToDecimal(Math.Round(value, 4)));
                else
                    writer.WriteStringValue(value.ToString());
            }

            writer.WriteEndArray();
        }
    }
}
public class DateTimeJsonConverterToUnixEpochSeconds : JsonConverter<DateTime> // the DateTime is written as a number, without quotes: ("lastUtc":"2022-09-08T07:15:08.2191122Z")  => ("lastUtc":1662635321)
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.UnixEpoch.AddSeconds(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, DateTime p_value, JsonSerializerOptions options)
    {
        // https://stackoverflow.com/questions/7966559/how-to-convert-javascript-date-object-to-ticks
        // "The JavaScript Date type's origin is the Unix epoch: midnight on 1 January 1970.
        // The .NET DateTime type's origin is midnight on 1 January 0001."

        // .Net Ticks: number of 100-nanosecond intervals that have elapsed since 12:00:00 midnight, January 1, 0001. It does not include the number of ticks that are attributable to leap seconds.
        // Ticks is very long (e.g. 633896886277130000). Not needed.
        // Furthermore, convert it to Unix epoct (from 1970) for having even a smaller number to send.
        long nSecondsSinceUnixEpoch = (long)(p_value - DateTime.UnixEpoch).TotalSeconds;
        writer.WriteNumberValue(nSecondsSinceUnixEpoch);
    }
}

// public class DateTimeJsonConverterToYYYYMMDD : JsonConverter<DateTime> // the DateTime is written as a number, without quotes: ("lastUtc":"2022-09-08T07:15:08.2191122Z")  => ("lastUtc":1662635321)
public static partial class Utils
{
    public static JsonSerializerOptions g_camelJsonSerializeOpt = new() // use this if JSON string is sent to a JavaScript frontend
    {
        WriteIndented = false, // exclude line breaks and indentations for smaller byte size
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // default Null means that property names in the output JSON are the same as the property names in the source .NET objects. But in Javascript, class field members should use CamelCase: https://www.robinwieruch.de/javascript-naming-conventions  "AnyParam" (PascalCase) should be turned to "anyParam" (CamelCase)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // without this "S&P" is serialized as "S\u0026P", which is ugly. Our results will not be URL data, or HTML data. We send our data in Websocket. We don't want these Escapings e.g. <, >, &, +
    };

    public static JsonSerializerOptions g_noEscapesJsonSerializeOpt = new() // without indentation and escaping
    {
        WriteIndented = false, // exclude line breaks and indentations for smaller byte size
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // without this "S&P" is serialized as "S\u0026P", which is ugly. Our results will not be URL data, or HTML data. We send our data in Websocket. We don't want these Escapings e.g. <, >, &, +
    };

    public static string CamelCaseSerialize<TValue>(TValue obj)
    {
        return JsonSerializer.Serialize(obj, g_camelJsonSerializeOpt);
    }

    public static T? LoadFromJSON<T>(string p_str)
    {
        try
        {
            // don't use FileStream directly to serializer
            // using (FileStream stream = File.OpenRead(filePath))
            // Encountered unexpected character 'ï', because
            // "Please note that DataContractJsonSerializer only supports the following encodings: UTF-8"
            // see http://blogs.msdn.com/b/cie/archive/2014/03/19/encountered-unexpected-character-239-error-serializing-json.aspx

            // string p_str = System.IO.File.ReadAllText(p_filePath);
            MemoryStream ms = new(Encoding.UTF8.GetBytes(p_str));
            DataContractJsonSerializerSettings settings = new() { DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ssZ"), UseSimpleDictionaryFormat = true };    // the 'T' is used by Javascript in HealthMonitor website. 'Z' shows that it is UTC (Zero TimeZone).  That is the reason of the format.
            DataContractJsonSerializer serializer = new(typeof(T), settings);
            T? contract = (T?)serializer.ReadObject(ms);
            return contract;
        }
        catch
        {
            Utils.Logger.Info("LoadFromJSON(): Cannot deserialize json " + p_str);      // Not even a warning. It is quite expected that sometimes, Json serialization fails. The caller will handle rethrown exception.
            throw;
        }
    }

    public static string SaveToJSON<T>(T p_obj)
    {
        try
        {
            DataContractJsonSerializerSettings settings = new() { DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ssZ"), UseSimpleDictionaryFormat = true };
            DataContractJsonSerializer serializer = new(typeof(T), settings);

            using MemoryStream ms = new();
            serializer.WriteObject(ms, p_obj);
            ms.Position = 0;
            StreamReader sr = new(ms);
            return sr.ReadToEnd();
            // return Encoding.Unicode.GetString(ms.ToArray());  //UTF-16 is used for in-memory strings because it is faster per character to parse and maps directly to unicode character class and other tables. All string functions in Windows use UTF-16 and have for years.
        }
        catch (System.Exception ex)
        {
            Utils.Logger.Info(ex, "Cannot serialize object " + p_obj!.ToString());
            throw;
        }
    }
}