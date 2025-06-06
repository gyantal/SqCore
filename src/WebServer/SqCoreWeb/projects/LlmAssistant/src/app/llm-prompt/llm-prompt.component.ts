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
  m_uniquePromptCategories: string[] = [];
  m_selectedPromptNames: string[] = [];
  m_selectedCategory: string = '';
  m_selectedPromptName: string = '';
  m_prompt: string = '';

  constructor() {}

  ngOnInit(): void {}

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'LlmPromptsData':
        console.log('webSocketOnMessage() - LlmPromptsData :', msgObjStr);
        this.m_llmPrompts = JSON.parse(msgObjStr);
        for (const prmptCat of this.m_llmPrompts) {
          if (!this.m_uniquePromptCategories.includes(prmptCat.Category))
            this.m_uniquePromptCategories.push(prmptCat.Category);
        }
        // Once data is avaiable, utomatically populate the promptCategory, promptName, and corresponding prompt
        if (this.m_uniquePromptCategories.length > 0) {
          this.m_selectedCategory = this.m_uniquePromptCategories[0];
          this.onClickPromptCategory(this.m_selectedCategory);
        }
        return true;
      default:
        return false;
    }
  }

  onClickPromptCategory(category: string): void {
    this.m_selectedCategory = category;
    // Reset previously selected prompt data
    this.m_selectedPromptName = '';
    this.m_prompt = '';
    this.m_selectedPromptNames = [];
    // Populate prompt names for the selected category
    for (const prmpt of this.m_llmPrompts) {
      if (prmpt.Category == category && !this.m_selectedPromptNames.includes(prmpt.PromptName))
        this.m_selectedPromptNames.push(prmpt.PromptName);
    }
    // Based on the prompt category, process the promptName and prompt
    for (const prmpt of this.m_llmPrompts) {
      if (prmpt.Category == this.m_selectedCategory) {
        this.onClickPromptName(prmpt.PromptName);
        break;
      }
    }
  }

  onClickPromptName(promptName: string): void {
    this.m_selectedPromptName = promptName;
    this.getPrompt(promptName);
  }

  getPrompt(promptName: string): void {
    for (const prmpt of this.m_llmPrompts) {
      if (prmpt.PromptName == promptName) {
        this.m_prompt = prmpt.Prompt;
        break;
      }
    }
  }
}