import { Component, Input, OnInit } from '@angular/core';

class LlmPromptJs {
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
  @Input() m_parentWsConnection?: WebSocket | null = null; // this property will be input from above parent container

  m_llmPrompts: LlmPromptJs[] = [];

  constructor() {}

  ngOnInit(): void {}

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'LlmPromptsData':
        console.log('webSocketOnMessage() - LlmPromptsData :', msgObjStr);
        this.m_llmPrompts = JSON.parse(msgObjStr);
        return true;
      default:
        return false;
    }
  }
}