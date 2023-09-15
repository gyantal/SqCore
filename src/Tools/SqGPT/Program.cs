// See https://aka.ms/new-console-template for more information
using LLama.Common;
using LLama;
using LlmFromRelease;

Console.WriteLine("Hello, World!");

string modelPath = "g:\\LLM\\llama-2-7b.ggmlv3.q4_0.bin"; // Llama2, base version, (in theory, it is uncensored, you just have to give a good prompt) non-woke sometimes. But training data has input from censored ChatGPT sessions. Some, it is somewhat censored. Prompt engineering should help.  Let's use this until the free-speech ElonMuskAi
// string modelPath = "g:\\LLM\\llama-2-7b-chat.ggmlv3.q4_0.bin"; // Llama2, chat version, fine tuned for non 'toxicity'. Uber-woke.
// string modelPath = "g:\\LLM\\llama2_7b_chat_uncensored.ggmlv3.q4_0.bin"; // Woke. (Even though it is uncensored, maybe I need better prompt)
// string modelPath = "g:\\LLM\\Wizard-Vicuna-13B-Uncensored.ggmlv3.q4_0.bin"; // too woke.
// string modelPath = "g:\\LLM\\VicUnlocked-30B-LoRA.ggmlv3.q4_0.bin"; // Woke.
// string modelPath = "g:\\LLM\\WizardLM-7B-uncensored.ggmlv3.q4_0.bin"; // 2023-04: only uncensored model, they tried to ban. But in 023-07 LLama2 Base came which is also uncensored. Not woke, not anti-woke. Balanced. But actually it is also Uber-woke. https://www.reddit.com/r/LocalLLaMA/comments/13c6ukt/the_creator_of_an_uncensored_local_llm_posted/


// new OfficialChatExample().Run(modelPath);
new SqFeedFromCode().Run(modelPath);

