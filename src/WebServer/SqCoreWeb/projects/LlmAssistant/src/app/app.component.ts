import { Component, ViewChild } from '@angular/core';
import { LlmChatComponent } from './llm-chat/llm-chat.component';
import { LlmBasicChatComponent } from './llm-basic-chat/llm-basic-chat.component';
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
  @ViewChild(LlmBasicChatComponent) private childLlmBasicChatComponent!: LlmBasicChatComponent;
  @ViewChild(LlmScanComponent) private childLlmScanComponent!: LlmScanComponent;
  @ViewChild(LlmPromptComponent) private childLlmPromptComponent!: LlmPromptComponent;

  m_activeTab: string = 'Chat';
  m_socket: WebSocket;
  isLlmAssistOpenManyTimes: boolean = false;
  isLlmAssistOpenManyTimesDialogVisible: boolean = false;

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
        case 'Ping': // Server sends heartbeats, ping-pong messages to check zombie websockets.
          console.log('ws: Ping message arrived:', msgObjStr);
          if (this.m_socket != null && this.m_socket.readyState === WebSocket.OPEN)
            this.m_socket.send('Pong:');
          break;
        case 'LlmAssist.IsLlmAssistOpenManyTimes':
          console.log('The LlmAssistant opened many times string:', msgObjStr);
          this.isLlmAssistOpenManyTimes = String(msgObjStr).toLowerCase() === 'true';
          if (this.isLlmAssistOpenManyTimes) {
            this.isLlmAssistOpenManyTimesDialogVisible = true;
            const dialogAnimate = document.getElementById('manyLlmAssistClientsDialog') as HTMLElement;
            dialogAnimate.style.animationName = 'dialogFadein';
            dialogAnimate.style.animationDuration = '3s';
            dialogAnimate.style.animationTimingFunction = 'linear'; // default would be ‘ease’, which is a slow start, then fast, before it ends slowly. We prefer the linear.
            // dialogAnimate.style.animationDelay = '0s';
            dialogAnimate.style.animationIterationCount = '1'; // only once
            dialogAnimate.style.animationFillMode = 'forwards';
          }
          break;
        default:
          let isHandled = this.childLlmChatComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childLlmBasicChatComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childLlmScanComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childLlmPromptComponent.webSocketOnMessage(msgCode, msgObjStr);

          if (!isHandled)
            console.log('ws: Warning! OnConnected Message arrived, but msgCode is not recognized.Code:' + msgCode + ', Obj: ' + msgObjStr);

          break;
      }
    };

    setTimeout(() => {
      if (this.m_socket != null && this.m_socket.readyState === WebSocket.OPEN)
        this.m_socket.send('LlmAssist.IsLlmAssistOpenManyTimes:');
    }, 3000);
  }

  onClickActiveTab(activeTab: string) {
    this.m_activeTab = activeTab;
  }

  onLlmAssistOpenedManyTimesContinueClicked() {
    this.isLlmAssistOpenManyTimesDialogVisible = false;
  }

  onLlmAssistOpenedManyTimesCloseClicked() {
    // on Date: 2025-06-20 there was an warning when we click on close button on UI saying "Scripts may close only the windows that were opened by them." see https://stackoverflow.com/questions/25937212/window-close-doesnt-work-scripts-may-close-only-the-windows-that-were-opene
    // window.close was not working on Daya's PC and Laptop but its was working on George's PC. At this point it was decided to keep as it was earlier.
    window.close();
  }
}