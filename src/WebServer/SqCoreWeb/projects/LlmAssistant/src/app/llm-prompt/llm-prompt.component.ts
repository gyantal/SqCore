import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

class LlmPromptCategoryJs {
  Category: string = '';
  PromptName: string = '';
  Prompt: string = '';
}

@Component({
  selector: 'app-llm-prompt',
  templateUrl: './llm-prompt.component.html',
  styleUrls: ['./llm-prompt.component.scss']
})

export class LlmPromptComponent implements OnInit {
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;
  m_llmPromptCategory: LlmPromptCategoryJs[] = [];

  constructor(http: HttpClient) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/';
  }

  ngOnInit(): void {
    // responseType: 'text' // instead of JSON, because return text can contain NewLines, \n and JSON.Parse() will fail with "SyntaxError: Bad control character in string literal in JSON"
    // this._httpClient.post(this._chatGptUrl, body, { responseType: 'text'}).subscribe(resultText => { // if message comes not as a properly formatted JSON string
    this.m_httpClient.post<LlmPromptCategoryJs[]>(this.m_controllerBaseUrl + 'getllmpromptresponse', { responseType: 'text'}).subscribe((llmPromptsResponse) => {
      this.m_llmPromptCategory = llmPromptsResponse;
      console.log(`length: ${this.m_llmPromptCategory.length}`);
    }, (error) => console.error(error));
  }
}