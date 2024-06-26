﻿using System;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Orders;
using RestSharp;
using RestSharp.Authenticators;
using SqCommon;

namespace QuantConnect.Api
{
    /// <summary>
    /// API Connection and Hash Manager
    /// </summary>
    public class ApiConnection
    {
        /// <summary>
        /// Authorized client to use for requests.
        /// </summary>
        public RestClient Client;

        // Authorization Credentials
        private readonly string _userId;
        private readonly string _token;

        /// <summary>
        /// Create a new Api Connection Class.
        /// </summary>
        /// <param name="userId">User Id number from QuantConnect.com account. Found at www.quantconnect.com/account </param>
        /// <param name="token">Access token for the QuantConnect account. Found at www.quantconnect.com/account </param>
        public ApiConnection(int userId, string token)
        {
            _token = token;
            _userId = userId.ToStringInvariant();
            Client = new RestClient("https://www.quantconnect.com/api/v2/");
        }

        /// <summary>
        /// Return true if connected successfully.
        /// </summary>
        public bool Connected
        {
            get
            {
                var request = new RestRequest("authenticate", Method.GET);
                AuthenticationResponse response;
                if (TryRequest(request, out response))
                {
                    return response.Success;
                }
                return false;
            }
        }

        /// <summary>
        /// Place a secure request and get back an object of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request"></param>
        /// <param name="result">Result object from the </param>
        /// <returns>T typed object response</returns>
        public bool TryRequest<T>(RestRequest request, out T result)
            where T : RestResponse
        {
            var responseContent = string.Empty;

            try
            {
                //Generate the hash each request
                // Add the UTC timestamp to the request header.
                // Timestamps older than 1800 seconds will not work.
                var timestamp = (int)Time.TimeStamp();
                var hash = Api.CreateSecureHash(timestamp, _token);
                request.AddHeader("Timestamp", timestamp.ToStringInvariant());

                Client.Authenticator = new HttpBasicAuthenticator(_userId, hash);

                // Execute the authenticated REST API Call
                var restsharpResponse = Client.Execute(request);

                // Use custom converter for deserializing live results data
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    Converters = { new LiveAlgorithmResultsJsonConverter(), new OrderJsonConverter() }
                };

                //Verify success
                if (restsharpResponse.ErrorException != null)
                {
                    Utils.Logger.Error($"ApiConnection.TryRequest({request.Resource}): Error: {restsharpResponse.ErrorException.Message}");
                    result = null;
                    return false;
                }

                if (!restsharpResponse.IsSuccessful)
                {
                    Utils.Logger.Error($"ApiConnect.TryRequest(): Content: {restsharpResponse.Content}");
                }

                responseContent = restsharpResponse.Content;
                result = JsonConvert.DeserializeObject<T>(responseContent);

                if (result == null || !result.Success)
                {
                    Utils.Logger.Debug($"ApiConnection.TryRequest(): Raw response: '{responseContent}'");
                    return false;
                }
            }
            catch (Exception err)
            {
                Utils.Logger.Error($"ApiConnection.TryRequest({request.Resource}): Error: {err.Message}, Response content: {responseContent}");
                result = null;
                return false;
            }
            return true;
        }
    }
}
