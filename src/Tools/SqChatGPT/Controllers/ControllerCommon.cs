
// >https://openai.com/pricing
// https://platform.openai.com/docs/models/gpt-4
// GPT-3.5 Turbo:
// 	4K context	$0.0015 / 1K tokens, so 1 query that uses 4K tokens = 4*0.0015 = $0.006 (half a cent)
// 	16K context	$0.003 / 1K tokens, so 1 query that uses 16K tokens = 16*0.003 = $0.048
// GPT-4: 
// 	8K context	$0.03 / 1K tokens, so 1 query that uses 8K tokens = 8*0.03 = $0.24 (GPT-4 base (8K) is 50x more expensive than GPT-3.5-turbo(4K))
// 	32K context	$0.06 / 1K tokens, so 1 query that uses 32K tokens = 32*0.06 = $1.92 (pricewise, it is better to use the 8K model 4times = 4*0.24=0.96). So expensive that don't expose it to UI.

namespace SqChatGPT.Controllers;

public class UserInput
{
    public string LlmModelName { get; set; } = string.Empty; // "auto", "gpt-3.5-turbo" (4K), "gpt-3.5-turbo-16k", "gpt-4" (8K), "gpt-4-32k"
    public string Msg { get; set; } = string.Empty;
}

public class ServerResponse
{
    public List<string> Logs { get; set; } = new();
    public string Response { get; set; } = string.Empty;
}