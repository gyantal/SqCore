export interface UserInput {
  LlmModelName: string;
  Msg: string;
}

export interface ServerResponse {
  Logs: string[];
  Response: string;
}

// Defined for processing the Grok chat response
export interface Message {
  role: string;
  content: string;
}

export interface Choice {
  index: number;
  message: Message;
  finish_reason: string;
}

export interface ChatResponse {
  id: string;
  model: string;
  choices: Choice[];
}