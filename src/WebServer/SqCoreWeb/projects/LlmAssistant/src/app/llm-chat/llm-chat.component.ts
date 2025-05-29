import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { UserInput } from '../lib/gpt-common';
// import { ServerResponse, UserInput } from '../lib/gpt-common'; // commentting this as we need this for chatgpt

class HandshakeMessage {
  public email = '';
  public userId = -1;
}

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
  m_socket: WebSocket;

  constructor(http: HttpClient) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/';
    this.m_socket = new WebSocket('wss://' + document.location.hostname + '/ws/llmassist');
  }

  sendUserInputToBackEnd(userInput: string): void {
    this.m_chatHistory.push('- User: ' + userInput.replace('\n', '<br/>')); // Show user input it chatHistory
    const usrInp : UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: userInput };
    console.log(usrInp);

    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('GetChatResponseLlm:' + JSON.stringify(usrInp));
  }

  ngOnInit(): void {
    this.m_socket.onmessage = async (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'OnConnected':
          const handshakeMsg: HandshakeMessage = JSON.parse(msgObjStr);
          console.log('ws: OnConnected HandshakeMsg', handshakeMsg);
          break;
        case 'LlmResponse':
          this.m_chatHistory.push('- Assistant: ' + msgObjStr);
          break;
        default:
          return false;
      }
    };
  }
}