import { Component, Input, OnInit } from '@angular/core';
import { UserInput } from '../lib/gpt-common';
// import { ServerResponse, UserInput } from '../lib/gpt-common'; // commentting this as we need this for chatgpt

@Component({
  selector: 'app-llm-chat',
  templateUrl: './llm-chat.component.html',
  styleUrls: ['./llm-chat.component.scss']
})

export class LlmChatComponent implements OnInit {
  @Input() m_parentWsConnection?: WebSocket | null = null; // this property will be input from above parent container

  m_selectedLlmModel: string = 'grok';
  m_chatHistory: string[] = [];

  constructor() {}

  sendUserInputToBackEnd(userInput: string): void {
    this.m_chatHistory.push('- User: ' + userInput.replace('\n', '<br/>')); // Show user input it chatHistory
    const usrInp : UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: userInput };
    console.log(usrInp);

    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetChatResponseLlm:' + JSON.stringify(usrInp));
  }

  ngOnInit(): void {}

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'LlmResponse':
        this.m_chatHistory.push('- Assistant: ' + msgObjStr);
        return true;
      default:
        return false;
    }
  }
}