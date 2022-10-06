using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Primitives;
using SqCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using YahooFinanceApi;
using System.Threading.Tasks;


// *********************************************************************************************************************
// WARNING !!!! THIS SERVICE DOESN'T ADJUST FOR DIVIDENDS (ONLY FOR SPLITS) , BECAUSE YF DATA DOESN'T ADJUST FOR DIVIDEND.
// *********************************************************************************************************************



// 1. Example to use it.
// https://www.snifferquant.net/YahooFinanceForwarder?yffOutFormat=json&yffColumns=dc1&jsonp=JsonpCallbackFunc&yffUri=query1.finance.yahoo.com/v7/finance/download/%5EVIX&period1=1990-01-02&period2=UtcNow&interval=1d&events=history

// 2. YF has non-adjusted data, dividend is not adjusted at all, splits are adjusted, but weirdly
// 2.1 dividend is not adjusted at all
// https://finance.yahoo.com/quote/AAPL/history?p=AAPL
// "May 11, 2017	152.45	154.07	152.31	153.95	153.95	27,255,100
// May 11, 2017	0.63 Dividend"
// https://www.snifferquant.net/YahooFinanceForwarder?yffOutFormat=json&jsonp=JsonpCallbackFunc&yffUri=query1.finance.yahoo.com/v7/finance/download/AAPL&period1=2017-02-02&period2=2017-05-22&interval=1d&events=history
// 2.2 VXX 4:1 spilts are adjusted, but weirdo
// based on VXX 2016-08-09 1:4 split: Open/High/Low is adjusted, Close is not adjusted, AdjClose is adjusted again.
// https://www.snifferquant.net/YahooFinanceForwarder?yffOutFormat=json&jsonp=JsonpCallbackFunc&yffUri=query1.finance.yahoo.com/v7/finance/download/VXX&period1=2016-08-01&period2=2017-05-22&interval=1d&events=history
// Date         Open        High        Low         Close       Adj Close   Volume
// 08/08/2016	37.599998	37.799999	37.200001	9.3	        37.200001	46298800
// 09/08/2016	36.709999	37.200001	35.880001	36.560001	36.560001	13481100
// 2.3 Luckily, yffColumns=dc1 which is c1=adjustedClose, it works well, because it uses the last column = AdjClose, which is adjusted.
// https://www.snifferquant.net/YahooFinanceForwarder?yffOutFormat=json&yffColumns=dc1&jsonp=JsonpCallbackFunc&yffUri=query1.finance.yahoo.com/v7/finance/download/VXX&period1=2016-08-01&period2=2017-05-22&interval=1d&events=history

// 3. It is an option to discontinue this web-service, as there is a chance it gives bad data. It gives bad data if there are dividends. However, in case of indices (VIX, SPX), or ETN-s (VXX), there is no dividend
// only splits, and in this case this service works fine. But be warned. WARNING !!!! THIS SERVICE DOESN'T ADJUST FOR DIVIDENDS (ONLY FOR SPLITS) , BECAUSE YF DATA DOESN'T ADJUST FOR DIVIDEND.
// We need to develop a separate service, getting data from SQL and supporting many tickers at once, that will be Dividend adjusted. We have C# code for that, so we don't need to implement this service for C# usage, only if we want to use it from JavaScript on the client side.

// 4. Future work: after Robert confirmed that v8 (without crumbs) gives same good data as v7 (with crumbs), we may change implementation to that  (maybe not, as it gives slightly different data)
// Temporary comment here, remove it after Robert made his investigation:
// "egy hasonló már le van implementálva a programban,  csak    query1.finance.yahoo.com/v7/finance/download/... helyett query2.finance.yahoo.com/v8/finance/chart/...  lásd itt:
// https://incode.browse.cloudforge.com/cgi-bin/hedgequant/HedgeQuant/src/Server/YahooQuoteCrawler/Crawler.cs?revision=7881&view=markup#l1612
// és ehhez nem kell se crumbs, se cookie a tapasztalatom szerint, és mint mondtam sok benne az adathiba.
// Majd valamikor megnézem hogy a query1.finance.yahoo.com/v7/-es API jobb adatokat ad-e mint a query2.finance.yahoo.com/v8/-as,
// de ez egy hosszabb nekigyűrkőzést igénylő munka/vizsgálódás."


namespace SqCoreWeb.Controllers;

// [Route("api/[controller]")]
public class YahooFinanceForwarder : Microsoft.AspNetCore.Mvc.Controller
{
    public YahooFinanceForwarder()
    {
    }

    [Authorize]
    public async Task<ActionResult> Index()
    {
        Tuple<string, string> contentAndType = await GenerateYffResponse();
        return Content(contentAndType.Item1, contentAndType.Item2);
    }

    private async Task<Tuple<string, string>> GenerateYffResponse()
    {
        try
        {
            // 1. Prepare input parameters
            string uriQuery = Request.QueryString.ToString();    // "?s=VXX,SVXY,^vix&f=ab&o=csv" from the URL http://localhost:58213/api/rtp?s=VXX,XIV,^vix&f=ab&o=csv
            if (uriQuery.Length > 8192)
            { // When you try to pass a string longer than 8192 charachters, a faultException will be thrown. There is a solution, but I don't want
                return new Tuple<string, string>(@"{ ""Message"":  ""Error caught by WebApi Get():: uriQuery is longer than 8192: we don't process that. Uri: " + uriQuery + @""" }", "application/json");
            }

            Dictionary<string, StringValues> allParamsDict = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uriQuery);   // unlike ParseQueryString in System.Web, this returns a dictionary of type IDictionary<string, string[]>, so the value is an array of strings. This is how the dictionary handles multiple query string parameters with the same name.
            if (!allParamsDict.TryGetValue("yffOutFormat", out StringValues queryStrVal))
            {
                return new Tuple<string, string>(@"{ ""Message"":  ""Error: yffOutFormat= was not found. Uri: " + uriQuery + @""" }", "application/json");
            }
            string outputFormat = queryStrVal[0];
            bool isOutputJson = !String.Equals(outputFormat, "csv", StringComparison.CurrentCultureIgnoreCase);

            string? yffColumns = null;
            List<string> yffColumnsList = new();
            if (allParamsDict.TryGetValue("yffColumns", out queryStrVal))
            {
                yffColumns = queryStrVal[0];
                yffColumnsList = new List<string>();
                int columnsFormatStartIdx = 0;
                string? previousCommand = null;
                for (int k = 1; k < yffColumns.Length; k++)
                {
                    if (Char.IsLetter(yffColumns[k]))
                    {
                        // process previous command
                        previousCommand = yffColumns[columnsFormatStartIdx..k];
                        yffColumnsList.Add(previousCommand);
                        columnsFormatStartIdx = k;
                    }
                }
                previousCommand = yffColumns[columnsFormatStartIdx..];
                yffColumnsList.Add(previousCommand);
            }
            else
            {
                yffColumnsList = new List<string>() { "d", "o", "h", "l", "c", "c1", "v" };
            }


            string? jsonpCallback = null, outputVariable = null;
            if (isOutputJson)
            {
                if (allParamsDict.TryGetValue("jsonp", out queryStrVal))
                {
                    jsonpCallback = queryStrVal[0];
                }
                if (allParamsDict.TryGetValue("yffOutVar", out queryStrVal))
                {
                    outputVariable = queryStrVal[0];
                }
                if (jsonpCallback == null && outputVariable == null)
                {
                    return new Tuple<string, string>(@"{ ""Message"":  ""Error: nor yffOutVar= , neiher jsonp= was found. Uri: " + uriQuery + @""" }", "application/json");
                }
                else if (jsonpCallback != null && outputVariable != null)
                {
                    return new Tuple<string, string>(@"{ ""Message"":  ""Error: Both yffOutVar= ,  jsonp= were found. Uri: " + uriQuery + @""" }", "application/json");
                }
            }

            string? targetUriWithoutHttp = null;
            string ticker = string.Empty;
            if (allParamsDict.TryGetValue("yffUri", out queryStrVal)) // yffUri=query1.finance.yahoo.com/v7/finance/download/AAPL&period1=2017-02-02&period2=2017-05-22&interval=1d&events=history
            {
                targetUriWithoutHttp = queryStrVal[0];
                int indSlash = targetUriWithoutHttp.LastIndexOf('/');
                if (indSlash != -1)
                    ticker = targetUriWithoutHttp[(indSlash + 1)..];
            }


            // 2. Obtain Token.Cookie and Crumb (maybe from cache until 12 hours) that is needed for Y!F API from 2017-05
            // after 2017-05: https://query1.finance.yahoo.com/v7/finance/download/VXX?period1=1492941064&period2=1495533064&interval=1d&events=history&crumb=VBSMphmA5gp
            // but we will accept standard dates too and convert to Unix epoch before sending it to YF
            // https://query1.finance.yahoo.com/v7/finance/download/VXX?period1=2017-02-31&period2=2017-05-23&interval=1d&events=history&crumb=VBSMphmA5gp
            // https://github.com/dennislwy/YahooFinanceAPI
            // int maxTryCrumb = 10;                 //first get a valid token from Yahoo Finance
            // while (maxTryCrumb >= 0 && (string.IsNullOrEmpty(Token.Cookie) || string.IsNullOrEmpty(Token.Crumb)))
            // {
            //     //await Token.RefreshAsync().ConfigureAwait(false);
            //     bool tokenSuccess = Token.RefreshAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            //     maxTryCrumb--;
            // }

            // 3. With the Token.Cookie and Crumb download YF CSV file
            string startTimeStr = allParamsDict["period1"];
            string endTimeStr = allParamsDict["period2"];
            DateTime startTime = SqDateOnly.MinValue, endTime = DateTime.UtcNow;
            if (startTimeStr.IndexOf('-') != -1) // format '2017-05-20' has hyphen in it; if it has hyphen, try to convert to Date.
            {
                startTime = DateTime.ParseExact(startTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
            {
                startTime = Utils.UnixTimeStampToDateTimeUtc(long.Parse(startTimeStr)); // it is in UnixTimestamp string, we have to convert to Date
            }

            if (String.Equals(endTimeStr, "UtcNow", StringComparison.CurrentCultureIgnoreCase)) // format '2017-05-20' has hyphen in it; if it has hyphen, try to convert to Date.
            {
                endTime = DateTime.UtcNow;
            }
            else
            {
                if (endTimeStr.IndexOf('-') != -1) // format '2017-05-20' has hyphen in it; if it has hyphen, try to convert to Date.
                    endTime = DateTime.ParseExact(endTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                else
                    endTime = Utils.UnixTimeStampToDateTimeUtc(long.Parse(endTimeStr));

                if (endTime.Date == DateTime.UtcNow.Date) // if today is included, we think the caller wants the real-time (latest) data; so change endTime to UtcNow
                    endTime = DateTime.UtcNow;
                else
                    endTime = endTime.AddHours(12);    // endTimeStr Date is excluded from daily data, so Add a little bit more to the middle of the UTC day
            }

            Period period = Period.Daily;
            if (allParamsDict["interval"] == "1w")
                period = Period.Weekly;
            else if (allParamsDict["interval"] == "1m")
                period = Period.Monthly;

            var history = await Yahoo.GetHistoricalAsync(ticker, startTime, endTime, period);


            // 4.1 Process YF CSV file either as JSON or as CSV: Header
            StringBuilder responseStrBldr = new();
            bool wasDataLineWritten = false;        // real data line, not header line
            if (isOutputJson)
            {
                if (outputVariable != null)
                {
                    responseStrBldr.Append("var " + outputVariable + " = [\n");
                }
                if (jsonpCallback != null)
                {
                    responseStrBldr.Append(jsonpCallback + "([\n");
                }
            }
            else
            { // output is CSV
                WriteRow(isOutputJson, yffColumnsList, new string[] { "Date", "Open", "High", "Low", "Close", "Adj Close", "Volume" }, responseStrBldr, ref wasDataLineWritten);
            }

            foreach (var candle in history)
            {
                string[] cells = new string[7];
                if (isOutputJson)
                    cells[0] = String.Format(@"Date.UTC({0},{1},{2})", candle!.DateTime.Year, candle!.DateTime.Month - 1, candle!.DateTime.Day);
                else
                    cells[0] = String.Format(@"{0}-{1}-{2}", candle!.DateTime.Year, candle!.DateTime.Month, candle!.DateTime.Day);

                // Prices in the given CSV as "15.830000" is pointless. Convert it to "15.8" if possible, 		"16.059999"	should be converted too
                cells[1] = candle!.Open.ToString("0.###");
                cells[2] = candle!.High.ToString("0.###");
                cells[3] = candle!.Low.ToString("0.###");
                cells[4] = candle!.Close.ToString("0.###");
                cells[5] = candle!.AdjustedClose.ToString("0.###");
                cells[6] = candle!.Volume.ToString("0.###");

                WriteRow(isOutputJson, yffColumnsList, cells, responseStrBldr, ref wasDataLineWritten);
            }


            // // 4.2 Process YF CSV file either as JSON or as CSV: Data lines
            // // First is the header, so skip it. Previously, it was upside down, so the latest was the first, but from 2017-05, the oldest is the first. Fine. Leave it like that.
            // string[] lines = csvDownload.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // for (int i = 1; i < lines.Length; i++)
            // {
            //     string[] cells = lines[i].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            //     if (cells.Length != 7)
            //     {
            //         return new Tuple<string, string>(@"{ ""Message"":  ""Error: yF row doesn't have 7 cells: " + lines[i] + @""" }", "application/json");
            //     }

            // DateTime date;
            //     if (!DateTime.TryParseExact(cells[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            //     {
            //         return new Tuple<string, string>(@"{ ""Message"":  ""Error: problem with date format: " + cells[0] + @""" }", "application/json");
            //     }
            //     if (isOutputJson)
            //         cells[0] = String.Format(@"Date.UTC({0},{1},{2})", date.Year, date.Month - 1, date.Day);
            //     else
            //         cells[0] = String.Format(@"{0}-{1}-{2}", date.Year, date.Month, date.Day);

            // // Prices in the given CSV as "15.830000" is pointless. Convert it to "15.8" if possible, 		"16.059999"	should be converted too
            //     for (int j = 1; j < 6; j++)
            //     {
            //         if (Double.TryParse(cells[j], out double price))
            //             cells[j] = price.ToString("0.###");
            //     }

            // //Volume is sometimes "000"; convert it to "0"
            //     if (Int64.TryParse(cells[6], out long volume))  // Volume was the 5th index, not it is the 6th index (the last item)
            //         cells[6] = volume.ToString();

            // WriteRow(isOutputJson, yffColumnsList, cells, responseStrBldr, ref wasDataLineWritten);
            // }

            // 4.3 Process YF CSV file either as JSON or as CSV: Footer and finalizing it
            if (isOutputJson)
            {
                if (outputVariable != null)
                    responseStrBldr.Append("];");
                if (jsonpCallback != null)
                    responseStrBldr.Append("]);");
            }

            return new Tuple<string, string>(responseStrBldr.ToString(), "application/javascript");
        }
        catch (Exception e)
        {
            return new Tuple<string, string>(@"{ ""Message"":  ""Exception caught by WebApi Get(): " + e.Message + @""" }", "application/json");
        }
    }

    private readonly Dictionary<string, int> cCommandToIndDict = new() { { "d", 0 }, { "o", 1 }, { "h", 2 }, { "l", 3 }, { "c", 4 }, { "c1", 5 }, { "v", 6 } };

    private void WriteCell(string p_command, string[] cells, StringBuilder responseStrBldr, ref bool wasCellWritten)
    {
        if (wasCellWritten)
            responseStrBldr.AppendFormat(",");
        responseStrBldr.Append(cells[cCommandToIndDict[p_command]]);
        wasCellWritten = true;
    }

    private void WriteRow(bool p_isOutputJson, List<string> yffColumnsList, string[] cells, StringBuilder responseStrBldr, ref bool wasDataLineWritten)
    {
        if (wasDataLineWritten)
        {
            if (p_isOutputJson)
                responseStrBldr.AppendFormat(",\n");
            else
                responseStrBldr.AppendFormat("\n");
        }

        if (p_isOutputJson)
            responseStrBldr.AppendFormat("[");

        bool wasCellWritten = false;
        foreach (var command in yffColumnsList)
        {
            WriteCell(command, cells, responseStrBldr, ref wasCellWritten);
        }
        if (p_isOutputJson)
            responseStrBldr.AppendFormat("]");
        wasDataLineWritten = true;
    }
}