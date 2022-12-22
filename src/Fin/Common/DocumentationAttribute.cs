using System;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace QuantConnect
{
    /// <summary>
    /// Custom attribute used for documentation
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    [DocumentationAttribute("Reference")]
    public sealed class DocumentationAttribute : Attribute
    {
        private static readonly DocumentationAttribute Attribute =
            typeof(DocumentationAttribute).GetCustomAttributes<DocumentationAttribute>().Single();
        private static readonly string BasePath =
            Attribute.FileName.Substring(0, Attribute.FileName.LastIndexOf("Common", StringComparison.Ordinal));

        /// <summary>
        /// The documentation tag
        /// </summary>
        [JsonProperty(PropertyName = "tag")]
        public string Tag { get; }

        /// <summary>
        ///  The associated weight of this attribute and tag
        /// </summary>
        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; }

        /// <summary>
        ///  The associated line of this attribute
        /// </summary>
        [JsonProperty(PropertyName = "line")]
        public int Line { get; }

        /// <summary>
        ///  The associated file name of this attribute
        /// </summary>
        [JsonProperty(PropertyName = "fileName")]
        public string FileName { get; }

        /// <summary>
        /// The attributes type id, we override it to ignore it when serializing
        /// </summary>
        [JsonIgnore]
        public override object TypeId => base.TypeId;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public DocumentationAttribute(string tag, int weight = 0,
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0,
            [System.Runtime.CompilerServices.CallerFilePath] string fileName = "")
        {
            Tag = tag;
            Line = line;
            Weight = weight;
            // will be null for the attribute of DocumentationAttribute itself
            if (BasePath != null)
            {
                FileName = fileName.Replace(BasePath, string.Empty, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                FileName = fileName;
            }
        }
    }
}
