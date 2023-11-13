export interface UserInput {
  LlmModelName: string;
  Msg: string;
}

export interface ServerResponse {
  Logs: string[];
  Response: string;
}
