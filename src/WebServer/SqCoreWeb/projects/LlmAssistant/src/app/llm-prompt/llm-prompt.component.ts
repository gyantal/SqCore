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
  m_promptCategories: string[] = [];
  m_selectedPromptCategory: string = '';
  m_selectedCategoryPromptNames: string[] = [];
  m_selectedPromptName: string = '';
  m_prompt: string = '';
  m_isUpdatePrompt: boolean = true; // The "UpdatePrompt" button is enabled only when the prompt contains "{ticker_list}".
  m_tickersStr: string = 'TSLA, AAPL';

  constructor() {}

  ngOnInit(): void {}

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'LlmPromptsData':
        console.log('webSocketOnMessage() - LlmPromptsData :', msgObjStr);
        this.m_llmPrompts = JSON.parse(msgObjStr);
        for (const prmptCat of this.m_llmPrompts) {
          if (!this.m_promptCategories.includes(prmptCat.Category))
            this.m_promptCategories.push(prmptCat.Category);
        }
        // Once data is avaiable, utomatically populate the promptCategory, promptName, and corresponding prompt
        if (this.m_promptCategories.length > 0) {
          this.m_selectedPromptCategory = this.m_promptCategories[0];
          this.onClickPromptCategory(this.m_selectedPromptCategory);
        }
        return true;
      default:
        return false;
    }
  }

  onClickPromptCategory(category: string): void {
    this.m_selectedPromptCategory = category;
    // Reset previously selected prompt data
    this.m_selectedPromptName = '';
    this.m_prompt = '';
    this.m_selectedCategoryPromptNames = [];
    // Populate prompt names for the selected category
    for (const prmpt of this.m_llmPrompts) {
      if (prmpt.Category == category && !this.m_selectedCategoryPromptNames.includes(prmpt.PromptName))
        this.m_selectedCategoryPromptNames.push(prmpt.PromptName);
    }
    // Based on the prompt category, process the promptName and prompt
    for (const prmpt of this.m_llmPrompts) {
      if (prmpt.Category == this.m_selectedPromptCategory) {
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
    this.m_isUpdatePrompt = false;
    if (this.m_prompt.includes('ticker_list'))
      this.m_isUpdatePrompt = true;
  }

  updatePromptWithTickers(tickers: string) {
    this.m_tickersStr = tickers;
    if (this.m_prompt.includes('ticker_list'))
      this.m_prompt = this.m_prompt.replace('{ticker_list}', this.m_tickersStr);
  }
}