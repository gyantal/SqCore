import { Component, ViewChild } from '@angular/core';
import { LlmChatComponent } from './llm-chat/llm-chat.component';
import { LlmScanComponent } from './llm-scan/llm-scan.component';
import { LlmPromptComponent } from './llm-prompt/llm-prompt.component';

class HandshakeMessage {
  public email = '';
  public userId = -1;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  @ViewChild(LlmChatComponent) private childLlmChatComponent!: LlmChatComponent;
  @ViewChild(LlmScanComponent) private childLlmScanComponent!: LlmScanComponent;
  @ViewChild(LlmPromptComponent) private childLlmPromptComponent!: LlmPromptComponent;

  m_activeTab: string = 'Chat';
  m_socket: WebSocket;

  constructor() {
    this.m_socket = new WebSocket('wss://' + document.location.hostname + '/ws/llmassist');
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
        default:
          let isHandled = this.childLlmChatComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childLlmScanComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childLlmPromptComponent.webSocketOnMessage(msgCode, msgObjStr);

          if (!isHandled)
            console.log('ws: Warning! OnConnected Message arrived, but msgCode is not recognized.Code:' + msgCode + ', Obj: ' + msgObjStr);

          break;
      }
    };
  }

  onClickActiveTab(activeTab: string) {
    this.m_activeTab = activeTab;
  }
}