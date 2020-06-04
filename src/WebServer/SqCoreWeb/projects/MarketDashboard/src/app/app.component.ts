import { Component, OnInit, ViewChild } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';

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

  // http://localhost:4202/hub/exsvpush/negotiate?negotiateVersion=1 404 (Not Found), if it is not served on port 4202 on ng serve (proxy)
  public _hubConnection: HubConnection = new HubConnectionBuilder().withUrl('/hub/dashboardpush').build();

  @ViewChild(SettingsDialogComponent) private settingsDialogComponent!: SettingsDialogComponent;

  // called after Angular has initialized all data-bound properties before any of the view or content children have been checked. Handle any additional initialization tasks.
  ngOnInit() {
    console.log('Sq: ngOnInit()');
    this.onSetTheme('sqClassic');

    this._hubConnection
      .start()
      .then(() => {
        console.log('ws: Connection started! _hubConnection.send() can be used now.');
      })
      .catch(err => console.log('Error while establishing connection :('));

    this._hubConnection.on('OnConnected', (message: HandshakeMessage) => {
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

}
