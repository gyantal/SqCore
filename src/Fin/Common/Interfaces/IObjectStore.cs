using System;
using QuantConnect.Packets;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides object storage for data persistence.
    /// </summary>
    [InheritedExport(typeof(IObjectStore))]
    public interface IObjectStore : IDisposable, IEnumerable<KeyValuePair<string, byte[]>>
    {
        /// <summary>
        /// Event raised each time there's an error
        /// </summary>
        event EventHandler<ObjectStoreErrorRaisedEventArgs> ErrorRaised;

        /// <summary>
        /// Initializes the object store
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <param name="projectId">The project id</param>
        /// <param name="userToken">The user token</param>
        /// <param name="controls">The job controls instance</param>
        void Initialize(int userId, int projectId, string userToken, Controls controls);

        /// <summary>
        /// Determines whether the store contains data for the specified path
        /// </summary>
        /// <param name="path">The object path</param>
        /// <returns>True if the key was found</returns>
        bool ContainsKey(string path);

        /// <summary>
        /// Returns the object data for the specified key
        /// </summary>
        /// <param name="path">The object key</param>
        /// <returns>A byte array containing the data</returns>
        byte[] ReadBytes(string path);

        /// <summary>
        /// Saves the object data for the specified path
        /// </summary>
        /// <param name="path">The object path</param>
        /// <param name="contents">The object data</param>
        /// <returns>True if the save operation was successful</returns>
        bool SaveBytes(string path, byte[] contents);

        /// <summary>
        /// Deletes the object data for the specified path
        /// </summary>
        /// <param name="path">The object path</param>
        /// <returns>True if the delete operation was successful</returns>
        bool Delete(string path);

        /// <summary>
        /// Returns the file path for the specified path
        /// </summary>
        /// <param name="path">The object path</param>
        /// <returns>The path for the file</returns>
        string GetFilePath(string path);

        /// <summary>
        /// Returns the file paths present in the object store. This is specially useful not to load the object store into memory
        /// </summary>
        ICollection<string> Keys { get; }

        /// <summary>
        /// Will clear the object store state cache. This is useful when the object store is used concurrently by nodes which want to share information
        /// </summary>
        void Clear();
    }
}
