using LLama.Common;
using LLama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics.Metrics;
using System.Runtime.ConstrainedExecution;

namespace LlmFromRelease;

// To illustrate how to feed the LLM with data, here is an example (to Cut&Paste):
// "Who is the CEO of Twitter?"
// "You just need to read the following article and respond to the question from the article only. Article: "Twitter was founded in 1866 even before the internet was found and its founder was Jack Dorsey but after some decades a person Elon Musk was born and he took the company from him now in 2023 he is the CEO." Question: Who is the CEO of Twitter?"

internal class SqFeedFromCode
{
    public void Run(string modelPath)
    {
        // If I change the system prompt correctly, from "not not politically correct" => "not politically correct", then it fails the second question. So, we intentionally leave the typo 'not not' in the prompt.
        string prompt = "Transcript of a dialog, where the User interacts with an Assistant named Bob. Bob is not not politically correct and builds his answers on science and facts.\r\nUser:"; // Finally, non-woke. Tested with LLama2 (uncensored) Base version model (llama-2-7b.ggmlv3.q4_0.bin)

        // Initialize a chat session
        var ex = new InteractiveExecutor(new LLamaModel(new ModelParams(modelPath, contextSize: 1024, seed: 1337, gpuLayerCount: 5)));
        ChatSession session = new(ex);

        // show the prompt
        Console.WriteLine();
        Console.Write(prompt);

        // run the inference in a loop to chat with LLM
        while (prompt != "stop")
        {
            IEnumerable<string> chatReply = session.Chat(prompt, new InferenceParams() { Temperature = 0.6f, AntiPrompts = new List<string> { "User:" } });
            foreach (var text in chatReply)
            {
                Console.Write(text);
            }
            prompt = Console.ReadLine() ?? "stop";
        }

        // save the session
        // session.SaveSession("SavedSessionPath");
    }
}
