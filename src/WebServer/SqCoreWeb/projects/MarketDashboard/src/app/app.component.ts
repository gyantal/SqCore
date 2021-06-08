import { Component, OnInit, ViewChild } from '@angular/core';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';
import { gDiag, minDate } from './../sq-globals';
import { MarketHealthComponent } from './market-health/market-health.component';
import { QuickfolioNewsComponent } from './quickfolio-news/quickfolio-news.component';
import { BrPrtfViewerComponent } from './brprtf-viewer/brprtf-viewer.component';

class HandshakeMessage {
  public email = '';
  public param2 = '';
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  @ViewChild(SettingsDialogComponent) private settingsDialogComponent!: SettingsDialogComponent;
  @ViewChild(MarketHealthComponent) private childMktHealthComponent!: MarketHealthComponent;
  @ViewChild(QuickfolioNewsComponent) private childQckflNewsComponent!: QuickfolioNewsComponent;
  @ViewChild(BrPrtfViewerComponent) private childBrPrtfViewerComponent!: BrPrtfViewerComponent;

  title = 'MarketDashboard';
  version = '0.1.1';
  user = {
    name: 'Anonymous',
    email: '             '
  };
  isToolSelectionVisible = false;
  isUserSelectionVisible = false;
  toolSelectionMsg = 'Click red arrow in toolbar! isToolSelectionVisible is set to ' + this.isToolSelectionVisible;
  activeTool = 'MarketHealth';
  theme = '';
  sqDiagnosticsMsg = 'Benchmarking time, connection speed';

  public _socket: WebSocket = new WebSocket('wss://' + document.location.hostname + '/ws/dashboard');   // "wss://127.0.0.1/ws/dashboard" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection

  constructor() { // Called first time before the ngOnInit()
    gDiag.mainAngComponentConstructorTime = new Date();
    // console.log('sq.d: ' + gDiag.mainAngComponentConstructorTime.toISOString() + ': mainAngComponentConstructor()'); // called 17ms after main.ts
  }

  // called after Angular has initialized all data-bound properties before any of the view or content children have been checked. Called after the constructor and called  after the first ngOnChanges()
  ngOnInit() {
    gDiag.mainAngComponentOnInitTime = new Date();
    // console.log('sq.d: ' + gDiag.mainAngComponentOnInitTime.toISOString() + ': mainAngComponentOnInitTime()');  // called 21ms after constructor

    this.onSetTheme('sqClassic');

    // WebSocket connection
    gDiag.wsConnectionStartTime = new Date();
    // console.log('sq.d: ' + gDiag.wsConnectionStartTime.toISOString() + ': wsConnectionStartTime()');

    this._socket.onopen = (event) => {
      gDiag.wsConnectionReadyTime = new Date();
      // console.log('sq.d: ' + gDiag.wsConnectionReadyTime.toISOString() + ': wsConnectionReadyTime()');
      console.log('ws: Connection started! _socket.send() can be used now.');
    };

    this._socket.onmessage = (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'OnConnected':
          gDiag.wsOnConnectedMsgArrivedTime = new Date();
          // console.log('sq.d: ' + gDiag.wsOnConnectedMsgArrivedTime.toISOString() + ': wsOnConnectedMsgArrivedTime()');
          console.log('ws: OnConnected Message arrived:' + event.data);

          const handshakeMsg: HandshakeMessage = Object.assign(new HandshakeMessage(), JSON.parse(msgObjStr));
          this.user.email = handshakeMsg.email;
          break;
        default:
          let isHandled = this.childMktHealthComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
          isHandled = this.childBrPrtfViewerComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childQckflNewsComponent.webSocketOnMessage(msgCode, msgObjStr);

          if (!isHandled) {
            console.log('ws: Warning! OnConnected Message arrived, but msgCode is not recognized:' + msgCode + 'obj: ' + msgObjStr);
          }
          break;
      }
    };

    // 'beforeunload' will be fired if the user submits a form, clicks a link, closes the window (or tab), or goes to a new page using the address bar, search box, or a bookmark.
    window.addEventListener('beforeunload', (unloadEvent) => {
      // dispose objects logic.
      // WebSocket Disconnection at page exit is not necessary, as server will timeout it. But it can be useful to release server resources earlier.
      console.log('window.beforeunload()');

      if (!this._socket || this._socket.readyState !== WebSocket.OPEN) {
        alert('socket not connected');
      }
      this._socket.close(1000, 'Closing from client');

      // unloadEvent.preventDefault();
      // unloadEvent.returnValue = 'window.beforeunload event: Unsaved modifications are possible';  // Define the returnValue only if you want to prompt user before unload.
      return unloadEvent;
    });

  }

  public onSetTheme($event: string) {
    this.theme = $event;
    console.log('Sq: set theme: ' + this.theme);
    let bgColor = '';
    let bgImage = '';
    let textColor = '';
    switch (this.theme) {
      case 'sqClassic':
        bgColor = 'rgb(255,255,255)';
        bgImage = 'none';
        textColor = 'rgb(0,0,0)';
        break;
      case 'sqGrad':
        bgColor = 'rgb(172, 225, 107)';
        bgImage = 'linear-gradient(to right, #3080c7 5%, #5b9dd7 10%, #d5f5f6 30%, #d5f5f6 70%, #ace16b 90%, #91d73a 95%)';
        textColor = 'rgb(0,0,255)';
        break;
      case 'ibClassic':
        bgColor = 'rgb(218, 218, 255)';
        bgImage = 'none';
        textColor = 'rgb(0,0,0)';
        break;
    }
    document.body.style.setProperty('--bgColor', bgColor);
    document.body.style.setProperty('--bgImage', bgImage);
    document.body.style.setProperty('--textColor', textColor);
  }

  onClickToolSelection() {
    this.isToolSelectionVisible = !this.isToolSelectionVisible;
    this.toolSelectionMsg = 'Click red arrow in toolbar! isToolSelectionVisible is set to ' + this.isToolSelectionVisible;
    this.isUserSelectionVisible = false;
  }

  onClickToolSelected() {
    this.isToolSelectionVisible = !this.isToolSelectionVisible;
    this.toolSelectionMsg = 'Click red arrow in toolbar! isToolSelectionVisible is set to ' + this.isToolSelectionVisible;
  }

  onClickUserSelection() {
    this.isUserSelectionVisible = !this.isUserSelectionVisible;
    this.isToolSelectionVisible = false;
  }

  onClickUserSelected() {
    this.isUserSelectionVisible = !this.isUserSelectionVisible;
  }

  // input comes from HTML. such as 'Docs-WhatIsNew'
  onChangeActiveTool(tool: string) {
    if (this.activeTool === tool) {
      return;
    }
    this.activeTool = tool;
  }

  closeDropdownMenu(menuItem: string) {
    if (menuItem === 'Tools') {
      this.isToolSelectionVisible = false;
    } else if (menuItem === 'User') {
      this.isUserSelectionVisible = false;
    }
  }

  openSettings() {
    this.settingsDialogComponent.openSettingsDialog();
  }

  mouseEnter(div: string) {
    console.log('mouse enter : ' + div);
    if (div === 'sqDiagnostics') {
      const diag =
        'App constructor: ' + (gDiag.mainAngComponentConstructorTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'DOM loaded: ' + (gDiag.dOMContentLoadedTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'Window loaded: ' + (gDiag.windowOnLoadTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        '-----\n' +
        'WebSocket connection start in OnInit: ' + (gDiag.wsConnectionStartTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'WebSocket connection ready: ' + (gDiag.wsConnectionReadyTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'WebSocket userdata(email) arrived: ' + (gDiag.wsOnConnectedMsgArrivedTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'WebSocket First NonRtStat: ' + (gDiag.wsOnFirstRtMktSumNonRtStatTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' +
        'WebSocket First RtStat: ' + (gDiag.wsOnFirstRtMktSumRtStatTime.getTime() - gDiag.mainTsTime.getTime()) + 'ms\n' + // if wsOnFirstRtMktSumRtStatTime == minTime, it can be negative
        'WebSocket #RtStat: ' + gDiag.wsNumRtMktSumRtStat + '\n' +
        'WebSocket Last RtStat: ' + (new Date().getTime() - gDiag.wsOnLastRtMktSumRtStatTime.getTime()) + 'ms ago\n' +
        'WebSocket Last Lookback Chg latency: ' + ((gDiag.wsOnLastRtMktSumLookbackChgStart === minDate) ? 'NaN\n' : (gDiag.wsOnLastRtMktSumNonRtStatTime.getTime() - gDiag.wsOnLastRtMktSumLookbackChgStart.getTime()) + 'ms\n');  // 14-20ms LocalDev, 27-33ms London to Dublin, thanks to the open WS connection. If a new connection has to be opened, it would be 80-130ms; 120ms Bahamas to Dublin (with a new connection it would be 500ms)
      this.sqDiagnosticsMsg = diag;
    }
  }

}
