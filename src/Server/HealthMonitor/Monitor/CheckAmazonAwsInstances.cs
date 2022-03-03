using SqCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace HealthMonitor
{
    public partial class HealthMonitor
    {
        int m_nFailedAmazonAwsInstancesCheck = 0;
        bool m_isCheckAmazonAwsInstancesEmailWasSent = false;  // to avoid sending the same warning email many times; send only once

        //# Key derivation functions. See: 
        //# http://docs.aws.amazon.com/general/latest/gr/signature-v4-examples.html#signature-v4-examples-python
        internal byte[] Sign(byte[] key, string msg)
        {
            HMACSHA256 hmac = new(key);
            var computedDigest = hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
            return computedDigest;
            //return hmac.new(key, msg.encode('utf-8'), hashlib.sha256).digest();
        }

        internal byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            byte[] kDate = Sign(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
            byte[] kRegion = Sign(kDate, regionName);
            byte[] kService = Sign(kRegion, serviceName);
            byte[] kSigning = Sign(kService, "aws4_request");
            return kSigning;
        }

        internal string? GetAmazonApiResponse(string p_actionWithParams)    // converted from Python code
        {
            string method = "GET";
            string service = "ec2";
            string host = "ec2.amazonaws.com";
            string region = "us-east-1";
            string endpoint = "https://ec2.amazonaws.com";

            string access_key = Utils.Configuration["AmazonAws:AccessKey"];
            string secret_key = Utils.Configuration["AmazonAws:SecretKey"];

            StrongAssert.NotEmpty(access_key, Severity.ThrowException, "AmazonAwsAccessKey is not found");
            StrongAssert.NotEmpty(secret_key, Severity.ThrowException, "AmazonAwsSecretKey is not found");

            //Assert.False(reader.ReadBoolean());
            //Assert.True(reader.ReadBoolean());
            //Assert.Throws<ArgumentNullException>(() => new BlobReader(null, 1));
            //Assert.Equal(0, new BlobReader(null, 0).Length); // this is valid
            //Assert.Throws<BadImageFormatException>(() => new BlobReader(null, 0).ReadByte()); // but can't read anything non-empty from it...
            //Assert.Same(string.Empty, new BlobReader(null, 0).ReadUtf8NullTerminated()); // can read empty string.

            // Create a date for headers and the credential string
            DateTime t = DateTime.UtcNow;
            string amz_date = t.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture); //.strftime('%Y%m%dT%H%M%SZ') # Format date as YYYYMMDD'T'HHMMSS'Z'
            string datestamp = t.ToString("yyyyMMdd", CultureInfo.InvariantCulture);  //# Date w/o time, used in credential scope

            // ************* TASK 1: CREATE A CANONICAL REQUEST *************
            // http://docs.aws.amazon.com/general/latest/gr/sigv4-create-canonical-request.html

            // Because almost all information is being passed in the query string,
            // the order of these steps is slightly different than examples that
            // use an authorization header.

            // Step 1: Define the verb (GET, POST, etc.)--already done.

            // Step 2: Create canonical URI--the part of the URI from domain to query 
            // string (use '/' if no path)
            string canonical_uri = "/";

            // Step 3: Create the canonical headers and signed headers. Header names
            // and value must be trimmed and lowercase, and sorted in ASCII order.
            // Note trailing \n in canonical_headers.
            // signed_headers is the list of headers that are being included
            // as part of the signing process. For requests that use query strings,
            // only "host" is included in the signed headers.
            string canonical_headers = "host:" + host + "\n";
            string signed_headers = "host";

            // Match the algorithm to the hashing algorithm you use, either SHA-1 or
            // SHA-256 (recommended)
            string algorithm = "AWS4-HMAC-SHA256";
            string credential_scope = datestamp + "/" + region + "/" + service + "/" + "aws4_request";

            // Step 4: Create the canonical query string. In this example, request
            // parameters are in the query string. Query string values must
            // be URL-encoded (space=%20). The parameters must be sorted by name.
            string canonical_querystring = "Action=" + p_actionWithParams + "&Version=2015-10-01";
            canonical_querystring += "&X-Amz-Algorithm=AWS4-HMAC-SHA256";
            string accKeyPlusScope = access_key + "/" + credential_scope;
			var encoder = System.Text.Encodings.Web.UrlEncoder.Default;
			string encAccKeyPlusScope = encoder.Encode(accKeyPlusScope);
            canonical_querystring += "&X-Amz-Credential=" + encAccKeyPlusScope;
            canonical_querystring += "&X-Amz-Date=" + amz_date;
            canonical_querystring += "&X-Amz-Expires=604800";
            canonical_querystring += "&X-Amz-SignedHeaders=" + signed_headers;

            // Step 5: Create payload hash. For GET requests, the payload is an
            // empty string ("").
            HashAlgorithm hashAlg = SHA256.Create();
            string payload_hash = BitConverter.ToString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(""))).Replace("-", string.Empty).ToLower();
            // Step 6: Combine elements to create create canonical request
            string canonical_request = method + "\n" + canonical_uri + "\n" + canonical_querystring + "\n" + canonical_headers + "\n" + signed_headers + "\n" + payload_hash;



            // ************* TASK 2: CREATE THE STRING TO SIGN*************
            string string_to_sign = algorithm + "\n" + amz_date + "\n" + credential_scope + "\n" + BitConverter.ToString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(canonical_request))).Replace("-", string.Empty).ToLower();


            // ************* TASK 3: CALCULATE THE SIGNATURE *************
            // Create the signing key
            byte[] signing_key = GetSignatureKey(secret_key, datestamp, region, service);

            // Sign the string_to_sign using the signing_key
            HMACSHA256 hmac = new(signing_key);
            var computedHexDigest = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(string_to_sign))).Replace("-", string.Empty).ToLower();
            string signature = computedHexDigest;
            //signature = hmac.new(signing_key, (string_to_sign).encode("utf-8"), hashlib.sha256).hexdigest()

            // ************* TASK 4: ADD SIGNING INFORMATION TO THE REQUEST *************
            // The auth information can be either in a query string
            // value or in a header named Authorization. This code shows how to put
            // everything into a query string.
            canonical_querystring += "&X-Amz-Signature=" + signature;


            // ************* SEND THE REQUEST *************
            // The 'host' header is added automatically by the Python 'request' lib. But it
            // must exist as a header in the request.
            string request_url = endpoint + "?" + canonical_querystring;
            //Utils.Logger.Info("request_url:" + request_url);
            string? hmWebsiteStr = Utils.DownloadStringWithRetryAsync(request_url, 5, TimeSpan.FromSeconds(5), true).TurnAsyncToSyncTask(); // caller will handle Exceptions.
            return hmWebsiteStr;
        }

        internal void CheckAmazonAwsInstances_Elapsed(object? p_sender) // Timer is coming on a ThreadPool thread
        {
            Utils.Logger.Info($"CheckAmazonAwsInstances_Elapsed (at every {cCheckAmazonAwsTimerFrequencyMinutes} minutes) BEGIN");

            string senderStr = Convert.ToString(p_sender) ?? string.Empty;      // This has the added benefit of returning an empty string (string.empty) if the object is null, to avoid any null reference exceptions (unless of course you want an exception thrown in such cases).

            try
            {
                StringBuilder sbWarning = new();
                try
                {
                    List<Tuple<string, string, string>> awsInstances = new();
                    string? awsInstancesXml = GetAmazonApiResponse("DescribeInstances");
                    if (awsInstancesXml == null)
                        sbWarning.AppendLine("GetAmazonApiResponse() returned null");
                    else
                    {
                        using XmlReader reader = XmlReader.Create(new StringReader(awsInstancesXml));
                        XmlWriterSettings ws = new();
                        ws.Indent = true;

                        string instanceName = string.Empty, instanceState = string.Empty, instancePublicIp = string.Empty;
                        while (reader.Read())
                        {
                            switch (reader.NodeType)
                            {
                                case XmlNodeType.Element:
                                    if (reader.Name == "instanceState")
                                    {
                                        while (reader.Name != "name" && reader.Read()) ;
                                        reader.Read();
                                        instanceState = reader.Value;
                                    }
                                    if (reader.Name == "tagSet")
                                    {
                                        while (reader.Value != "Name" && reader.Read()) ;
                                        reader.Read();  // </key>
                                        reader.Read();  // whitespace, Value = "\n"
                                        reader.Read();  // <value>
                                        reader.Read();  // "HQaVirtualBrokerDev"
                                        instanceName = reader.Value;
                                    }
                                    if (reader.Name == "association")
                                    {
                                        while (reader.Name != "publicIp" && reader.Read()) ;        // stopped instances doesn't have publicIp
                                        reader.Read();
                                        instancePublicIp = reader.Value;
                                    }
                                    break;
                                case XmlNodeType.EndElement:
                                    if (reader.Name == "instancesSet")  // "</instancesSet>"
                                    {
                                        awsInstances.Add(new Tuple<string, string, string>(instanceName, instancePublicIp, instanceState));
                                        instanceName = string.Empty; instanceState = string.Empty; instancePublicIp = string.Empty;
                                    }
                                    break;
                            }
                        }
                    }

                    bool isCheckedHQaVirtualBrokerAgent = false;
                    foreach (var inst in awsInstances)
                    {
                        Utils.Logger.Info(inst.Item1 + " (" + inst.Item2 + "): " + inst.Item3);
                        if (inst.Item1 == "HQaVirtualBrokerAgent")
                        {
                            isCheckedHQaVirtualBrokerAgent = true;
                            //if (inst.Item3 != "stopped")        // 2016-02-18: we expect the VBAgent server to be stopped. When it is live, we expect it to be "running"
                            if (inst.Item3 != "running")        // 2016-03-17: we expect the VBAgent server to be running.
                                sbWarning.AppendLine("Instance " + inst.Item1 + " has unexpected state: " + inst.Item3);
                        }
                    }

                    if (!isCheckedHQaVirtualBrokerAgent)
                        sbWarning.AppendLine("Instance HQaVirtualBrokerAgent was not found in AWS reply.");
                }
                catch (Exception e) // Exceptions from GetAmazonApiResponse(): NoAuthorization will be caught here
                {
                    sbWarning.AppendLine("Exception: " + e.Message);
                }

                
                bool isOK = (sbWarning.Length == 0);
                if (!isOK)
                {
                    Utils.Logger.Info($"CheckAmazonAwsInstances(): !isOK. m_nFailedAmazonAwsInstancesCheck: {++m_nFailedAmazonAwsInstancesCheck}");
                    if (!m_isCheckAmazonAwsInstancesEmailWasSent && m_nFailedAmazonAwsInstancesCheck >= 2)  // send email only the second time. First time maybe there is a server Restart, althought AWS will probably say 'running' even when it is under rebooting, but better be safe
                    {
                        Utils.Logger.Info("CheckAmazonAwsInstances(). Sending Warning email.");
                        new Email
                        {
                            ToAddresses = Utils.Configuration["Emails:Gyant"],
                            Subject = "SQ HealthMonitor: WARNING! CheckAmazonAwsInstances was NOT successfull. ",
                            Body = "SQ HealthMonitor: WARNING! CheckAmazonAwsInstances was NOT successfull. " + sbWarning.ToString(),
                            IsBodyHtml = false
                        }.Send();
                        m_isCheckAmazonAwsInstancesEmailWasSent = true;
                    }
                }
                else
                {
                    m_nFailedAmazonAwsInstancesCheck = 0;
                    Utils.Logger.Info("CheckAmazonAwsInstances(): isOK.");
                    if (m_isCheckAmazonAwsInstancesEmailWasSent)
                    {  // it was bad, but now it is correct somehow
                        new Email
                        {
                            ToAddresses = Utils.Configuration["Emails:Gyant"],
                            Subject = "SQ HealthMonitor: OK! CheckAmazonAwsInstances was successfull again.",
                            Body = "SQ HealthMonitor: OK! CheckAmazonAwsInstances was successfull again.",
                            IsBodyHtml = false
                        }.Send();
                        m_isCheckAmazonAwsInstancesEmailWasSent = false;
                    }
                }

                if (senderStr.Equals("ConsoleMenu"))
                {
                    Console.WriteLine($"SQ HealthMonitor: CheckAmazonAwsInstances was { (isOK ? "": "NOT ") }successfull.");
                }


            }
            catch (Exception e) // if the Exception is while Sending email, then we cannot send email about it. User has to view the Log files.
            {
                Utils.Logger.Info(e, "CheckAmazonAwsInstances() Exception.");
            }
            Utils.Logger.Info("CheckAmazonAwsInstances() END");
        }
    }
}
