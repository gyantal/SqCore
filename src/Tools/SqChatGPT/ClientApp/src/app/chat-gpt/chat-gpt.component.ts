import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface UserInput {
  Msg: string;
}

interface ServerResponse {
  Response: string;
}

@Component({
  selector: 'app-chat-gpt',
  templateUrl: './chat-gpt.component.html'
})
export class ChatGptComponent {
  _httpClient: HttpClient;
  _baseUrl: string;
  _chatGptUrl: string;

  _chatHistory: string[] = [];

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._httpClient = http;
    this._baseUrl = baseUrl;
    this._chatGptUrl = baseUrl + 'chatgpt/sendUserInput';
  }

  sendUserInputToBackEnd(p_userInput: string): void {
    this._chatHistory.push("- User: " + p_userInput.replace("\n", "<br/>")); // Show user input it chatHistory

    // HttpGet if input is simple and can be placed in the Url
    // this._httpClient.get<ServerResponse>(this._baseUrl + 'chatgpt/sendString').subscribe(result => {
    //   alert(result.Response);
    // }, error => console.error(error));

    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body : UserInput = { Msg: p_userInput };

    // responseType: 'text' // instead of JSON, because return text can contain NewLines, \n and JSON.Parse() will fail with "SyntaxError: Bad control character in string literal in JSON"
    // this._httpClient.post(this._chatGptUrl, body, { responseType: 'text'}).subscribe(resultText => { // if message comes not as a properly formatted JSON string
      this._httpClient.post<ServerResponse>(this._chatGptUrl, body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      // alert(result.Response);
      this._chatHistory.push("- Assistant: " + result.Response.replace("\n", "<br/>"));
    }, error => console.error(error));

  }
}

