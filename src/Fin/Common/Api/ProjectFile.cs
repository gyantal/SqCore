using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// File for a project
    /// </summary>
    public class ProjectFile
    {
        /// <summary>
        /// Name of a project file
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Contents of the project file
        /// </summary>
        [JsonProperty(PropertyName = "content")]
        public string Code { get; set; }

        /// <summary>
        /// DateTime project file was modified
        /// </summary>
        [JsonProperty(PropertyName = "modified")]
        public DateTime DateModified{ get; set; }
    }

    /// <summary>
    /// Response received when reading all files of a project
    /// </summary>
    public class ProjectFilesResponse : RestResponse
    {
        /// <summary>
        /// List of project file information
        /// </summary>
        [JsonProperty(PropertyName = "files")]
        public List<ProjectFile> Files { get; set; }
    }
}