import { Component, OnInit, ViewChild } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';
import { gDiag } from './../sq-globals';

class HandshakeMessage {
  public email = '';
  public param2  = '';
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'MarketDashboard';
  version = '0.1.1';
  user = {
    name: 'Anonymous',
    email: 'anonymous@gmail.com'
  };
  isToolSelectionVisible = false;
  isUserSelectionVisible = false;
  toolSelectionMsg = 'Click red arrow in toolbar! isToolSelectionVisible is set to ' + this.isToolSelectionVisible;
  activeTool = 'MarketHealth';
  theme = '';
  sqDiagnosticsMsg = 'Benchmarking time, connection speed';

  // http://localhost:4202/hub/exsvpush/negotiate?negotiateVersion=1 404 (Not Found), if it is not served on port 4202 on ng serve (proxy)
  public _hubConnection: HubConnection = new HubConnectionBuilder().withUrl('/hub/dashboardpush', { skipNegotiation: true, transport: HttpTransportType.WebSockets }).build();

  @ViewChild(SettingsDialogComponent) private settingsDialogComponent!: SettingsDialogComponent;

  constructor() { // Called first time before the ngOnInit()
    gDiag.mainAngComponentConstructorTime = new Date();
    console.log('sq.d: ' + gDiag.mainAngComponentConstructorTime.toISOString() + ': mainAngComponentConstructor()'); // called 17ms after main.ts
  }

  // called after Angular has initialized all data-bound properties before any of the view or content children have been checked. Called after the constructor and called  after the first ngOnChanges()
  ngOnInit() {
    gDiag.mainAngComponentOnInitTime = new Date();
    console.log('sq.d: ' + gDiag.mainAngComponentOnInitTime.toISOString() + ': mainAngComponentOnInitTime()');  // called 21ms after constructor

    this.onSetTheme('sqClassic');

    gDiag.wsConnectionStartTime = new Date();
    console.log('sq.d: ' + gDiag.wsConnectionStartTime.toISOString() + ': wsConnectionStartTime()');
    this._hubConnection
      .start()
      .then(() => {
        gDiag.wsConnectionReadyTime = new Date();
        console.log('sq.d: ' + gDiag.wsConnectionReadyTime.toISOString() + ': wsConnectionReadyTime()');
        console.log('ws: Connection started! _hubConnection.send() can be used now.');
      })
      .catch(err => console.log('Error while establishing connection :('));

    this._hubConnection.on('OnConnected', (message: HandshakeMessage) => {
      gDiag.wsOnConnectedMsgArrivedTime = new Date();
      console.log('sq.d: ' + gDiag.wsOnConnectedMsgArrivedTime.toISOString() + ': wsOnConnectedMsgArrivedTime()');  // called 500ms after windowOnLoad()
      console.log('ws: OnConnected Message arrived:' + message.email);
      this.user.email = message.email;
    });
  }

  public onSetTheme($event: string) {
    this.theme = $event;
    console.log(this.theme);
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
    console.log('Sq: set theme.');
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
      'DOM loaded: ' + (gDiag.dOMContentLoadedTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Window loaded: ' + (gDiag.windowOnLoadTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'App constructor: ' + (gDiag.mainAngComponentConstructorTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Websocket connection start in OnInit: ' + (gDiag.wsConnectionStartTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Websocket connection ready: ' + (gDiag.wsConnectionReadyTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Websocket userdata(email) arrived: ' + (gDiag.wsOnConnectedMsgArrivedTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Websocket First NonRtStat: ' + (gDiag.wsOnFirstRtMktSumNonRtStatTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' +
      'Websocket First RtStat: ' + (gDiag.wsOnFirstRtMktSumRtStatTime.getTime() - gDiag.mainTsTime.getTime() ) + 'ms\n' + // if wsOnFirstRtMktSumRtStatTime == minTime, it can be negative
      'Websocket #RtStat: ' + gDiag.wsNumRtMktSumRtStat + '\n' +
      'Websocket Last RtStat: ' + (new Date().getTime() - gDiag.wsOnLastRtMktSumRtStatTime.getTime() ) + 'ms ago\n';
      this.sqDiagnosticsMsg = diag;
    }
 }

}
