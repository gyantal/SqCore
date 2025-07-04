using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using SqCommon;

namespace SqCoreWeb;

public class LlmPromptJs // sent to browser clients
{
    public string Category { get; set; } = string.Empty;
    public string PromptName { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}
public partial class LlmAssistClient
{
    public static void GetPromptsDataFromGSheet(WebSocket webSocket)
    {
        if (string.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) || string.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
        {
            Debug.WriteLine("Missing Google API key.");
            return;
        }
        string? valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1wOY4OeoLbaYSfutiSc0elv26SVwLtBXqXnaNZ4YtggU/values/A1:Z2000?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
        if (valuesFromGSheetStr == null)
        {
            Debug.WriteLine("Failed to retrieve data from Google Sheet.");
            return;
        }
        Debug.WriteLine($"The length of data from gSheet for LlmPromptCategory is {valuesFromGSheetStr!.Length}");

        List<LlmPromptJs> llmPromptCategories = new List<LlmPromptJs>();
        string[] rows = valuesFromGSheetStr.Split(new string[] { "],\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split(new string[] { "\",\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length != 3) // skip the row if it doesn't contain 3 cells (Category, PromptName, Prompt)
                continue;
            string cellFirst = cells[0];
            int categoryStartIdx = cellFirst.IndexOf('\"');
            if (categoryStartIdx == -1)
                continue;
            string category = cellFirst[(categoryStartIdx + 1)..];
            string cellSecond = cells[1];
            int promptNameStartIdx = cellSecond.IndexOf('\"');
            if (promptNameStartIdx == -1)
                continue;
            string promptName = cellSecond[(promptNameStartIdx + 1)..];
            string cellThird = cells[2];
            int promptStartIdx = cellThird.IndexOf('\"');
            if (promptStartIdx == -1)
                continue;

            string tickerLstplaceholder = "_tickerList_"; // To retain "}" in {ticker_list}, temporarily replace {ticker_list} with a placeholder
            string keyStatsplaceholder = "_keyStats_"; // To retain "}" in {key_stats}, temporarily replace {key_stats} with a placeholder
            string prompt = cellThird[(promptStartIdx + 1)..].Replace("{ticker_list}", tickerLstplaceholder).Replace("{key_stats}", keyStatsplaceholder).Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", string.Empty).Replace("\"", string.Empty).Replace("}", string.Empty).Replace("]", string.Empty).Replace(tickerLstplaceholder, "{ticker_list}").Replace(keyStatsplaceholder, "{key_stats}").Trim();
            llmPromptCategories.Add(new LlmPromptJs() { Category = category, PromptName = promptName, Prompt = prompt });
        }

        byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmPromptsData:" + JsonSerializer.Serialize(llmPromptCategories));
        if (webSocket!.State == WebSocketState.Open)
            webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public bool OnReceiveWsAsync_PromptAssist(string msgCode, string msgObjStr)
    {
        throw new NotImplementedException();
    }
}