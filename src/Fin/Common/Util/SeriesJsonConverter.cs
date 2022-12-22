using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Util
{
    /// <summary>
    /// Json Converter for Series which handles special Pie Series serialization case
    /// </summary>
    public class SeriesJsonConverter : JsonConverter
    {
        /// <summary>
        /// Write Series to Json
        /// </summary>
        /// <param name="writer">The Json Writer to use</param>
        /// <param name="value">The value to written to Json</param>
        /// <param name="serializer">The Json Serializer to use</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var series = value as Series;
            if (series == null)
            {
                return;
            }

            writer.WriteStartObject();

            List<ChartPoint> values;
            if (series.SeriesType == SeriesType.Pie)
            {
                values = new List<ChartPoint>();
                var dataPoint = series.ConsolidateChartPoints();
                if (dataPoint != null)
                {
                    values.Add(dataPoint);
                }
            }
            else
            {
                values = series.Values;
            }

            // have to add the converter we want to use, else will use default
            serializer.Converters.Add(new ColorJsonConverter());

            writer.WritePropertyName("Name");
            writer.WriteValue(series.Name);
            writer.WritePropertyName("Unit");
            writer.WriteValue(series.Unit);
            writer.WritePropertyName("Index");
            writer.WriteValue(series.Index);
            writer.WritePropertyName("Values");
            serializer.Serialize(writer, values);
            writer.WritePropertyName("SeriesType");
            writer.WriteValue(series.SeriesType);
            writer.WritePropertyName("Color");
            serializer.Serialize(writer, series.Color);
            writer.WritePropertyName("ScatterMarkerSymbol");
            serializer.Serialize(writer, series.ScatterMarkerSymbol);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determine if this Converter can convert this type
        /// </summary>
        /// <param name="objectType">Type that we would like to convert</param>
        /// <returns>True if <see cref="Series"/></returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Series);
        }

        /// <summary>
        /// This converter wont be used to read JSON. Will throw exception if manually called.
        /// </summary>
        public override bool CanRead => false;
    }
}
