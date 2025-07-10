import { Component, Input, OnInit } from '@angular/core';
import { UserInput } from '../lib/gpt-common';
import { markdown2HtmlFormatter } from '../../../../../TsLib/sq-common/utils_string';
// import { ServerResponse, UserInput } from '../lib/gpt-common'; // commentting this as we need this for chatgpt

export interface LlmChatUserInput extends UserInput {
  IsSearchOnWeb: boolean;
}

class ChatItem {
  isUser : boolean = false; // User messages are written by user. LlmMessages are written by LlmModels.
  chatMdStr: string = ''; // C# server sends the raw Llm answer as MD text
  chatHtmlStr: string = ''; // formatted in HTML for visualization
}

@Component({
  selector: 'app-llm-chat',
  templateUrl: './llm-chat.component.html',
  styleUrls: ['./llm-chat.component.scss']
})

export class LlmChatComponent implements OnInit {
  @Input() m_parentWsConnection?: WebSocket | null = null; // this property will be input from above parent container

  m_selectedLlmModel: string = 'grok';
  m_chatItems: ChatItem[] = [];
  m_isWebSearch: boolean = false;

  constructor() {}

  sendUserInputToBackEnd(userInput: string): void {
    const chatItem = new ChatItem();
    chatItem.isUser = true;
    chatItem.chatMdStr = userInput;
    chatItem.chatHtmlStr = userInput.replace('\n', '<br/>');
    this.m_chatItems.push(chatItem);
    const usrInp : LlmChatUserInput = { LlmModelName: this.m_selectedLlmModel, Msg: userInput, IsSearchOnWeb: this.m_isWebSearch};
    console.log(usrInp);

    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetChatResponseLlm:' + JSON.stringify(usrInp));
  }

  ngOnInit(): void {}

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'LlmResponse':
        let lastChat: ChatItem = this.m_chatItems[this.m_chatItems.length - 1];
        if (lastChat.isUser) { // new message streaming starts
          lastChat = new ChatItem();
          this.m_chatItems.push(lastChat);
        }
        lastChat.chatMdStr += msgObjStr;
        lastChat.chatHtmlStr = markdown2HtmlFormatter(lastChat.chatMdStr);
        return true;
      default:
        return false;
    }
  }

  onClickNewChat() {
    this.m_chatItems.length = 0;
    // empty the user input textarea
    const userInputText: HTMLTextAreaElement = document.getElementById('userInput') as HTMLTextAreaElement;
    userInputText.value = '';
    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('LlmAssistNewChat:');
  }

  onChangeWebSearchCheckbox(event: Event): void {
    const webSearchInputElement: HTMLInputElement = event.target as HTMLInputElement;
    this.m_isWebSearch = webSearchInputElement.checked;
  }
}