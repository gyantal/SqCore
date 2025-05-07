import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ServerResponse, UserInput, ChatResponse } from '../lib/gpt-common';

@Component({
  selector: 'app-llm-chat',
  templateUrl: './llm-chat.component.html',
  styleUrls: ['./llm-chat.component.scss']
})

export class LlmChatComponent implements OnInit {
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;
  m_selectedLlmModel: string = 'grok';
  m_chatHistory: string[] = [];

  constructor(http: HttpClient) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/';
  }

  sendUserInputToBackEnd(userInput: string): void {
    this.m_chatHistory.push('- User: ' + userInput.replace('\n', '<br/>')); // Show user input it chatHistory

    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body : UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: userInput };
    console.log(body);

    // responseType: 'text' // instead of JSON, because return text can contain NewLines, \n and JSON.Parse() will fail with "SyntaxError: Bad control character in string literal in JSON"
    // this._httpClient.post(this._chatGptUrl, body, { responseType: 'text'}).subscribe(resultText => { // if message comes not as a properly formatted JSON string
    this.m_httpClient.post<ServerResponse>(this.m_controllerBaseUrl + 'getchatresponse', body).subscribe((result) => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this.m_chatHistory.push('- Assistant: ' + result.Response.replace('\n', '<br/>'));
    }, (error) => console.error(error));

    this.m_httpClient.post<ChatResponse>(this.m_controllerBaseUrl + 'getchatresponsellm', body).subscribe((result) => {
      const content = result.choices?.[0]?.message?.content ?? 'No response';
      this.m_chatHistory.push('- Assistant: ' + content.replace(/\n/g, '<br/>'));
    }, (error) => console.error(error));
  }

  ngOnInit(): void {}
}