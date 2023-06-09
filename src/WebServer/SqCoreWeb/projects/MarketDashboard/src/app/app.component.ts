import { Component, OnInit, ViewChild } from '@angular/core';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';
import { gDiag, AssetLastJs } from '../../../../TsLib/sq-common/sq-globals';
import { BrAccViewerComponent } from './bracc-viewer/bracc-viewer.component';
import { PortfolioManagerComponent } from './portfolio-manager/portfolio-manager.component';
import { MarketHealthComponent } from './market-health/market-health.component';
import { QuickfolioNewsComponent } from './quickfolio-news/quickfolio-news.component';
import { ChangeNaNstringToNaNnumber, SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils'; // direct reference, instead of via 'public-api.ts' as an Angular library. No need for 'ng build sq-ng-common'. see https://angular.io/guide/creating-libraries
import { SqNgCommonUtilsTime, minDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';

type Nullable<T> = T | null;

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
  @ViewChild(BrAccViewerComponent) private childBrAccViewerComponent!: BrAccViewerComponent;
  @ViewChild(PortfolioManagerComponent) private childPortfolioManagerComponent!: PortfolioManagerComponent;
  @ViewChild(MarketHealthComponent) private childMktHealthComponent!: MarketHealthComponent;
  @ViewChild(QuickfolioNewsComponent) private childQckflNewsComponent!: QuickfolioNewsComponent;

  title = 'MarketDashboard';
  version = '0.1.1';

  // UrlQueryParams (keep them short): // ?t=bav
  // t (Active (T)ool) = mh (Market Health), bav (Broker Portfolio Viewer)
  public urlQueryParamsArr : string[][];
  public urlQueryParamsObj = {}; // empty object. If queryParamsObj['t'] doesn't exist, it returns 'undefined'
  user = {
    name: 'Anonymous',
    email: '             '
  };
  isToolSelectionVisible = false;
  isUserSelectionVisible = false;
  isDshbrdOpenManyTimes: boolean = false;
  isDshbrdOpenManyTimesDialogVisible: boolean = false;
  isSrvConnectionAlive: boolean = true;
  toolSelectionMsg = 'Click red arrow in toolbar! isToolSelectionVisible is set to ' + this.isToolSelectionVisible;
  public activeTool = 'MarketHealth';
  theme = '';
  sqDiagnosticsMsg = 'Benchmarking time, connection speed';
  lastTimeOpSysSleepWakeupChecked = 0;

  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams

  public urlParamActiveTool2UiActiveTool = {
    'bav': 'BrAccViewer',
    'pm': 'PortfolioManager',
    'mh': 'MarketHealth',
    'cs': 'CatalystSniffer',
    'qn': 'QuickfolioNews'
  };

  nLstValArrived = 0;
  lstValStr = '[Nothing arrived yet]';
  lstValObj: Nullable<AssetLastJs[]> = null;

  constructor() { // Called first time before the ngOnInit()
    gDiag.mainAngComponentConstructorTime = new Date();
    // console.log('sq.d: ' + gDiag.mainAngComponentConstructorTime.toISOString() + ': mainAngComponentConstructor()'); // called 17ms after main.ts
    // console.log('AppComponent.ctor: ' + window.location.search);

    this.urlQueryParamsArr = SqNgCommonUtils.getUrlQueryParamsArray();
    this.urlQueryParamsObj = SqNgCommonUtils.Array2Obj(this.urlQueryParamsArr);
    console.log('AppComponent.ctor: queryParamsArr.Length: ' + this.urlQueryParamsArr.length);
    console.log('AppComponent.ctor: Active Tool, queryParamsObj["t"]: ' + this.urlQueryParamsObj['t']);
    // console.log('AppComponent.ctor: queryParams.t: ' + queryParams.t);

    let wsQueryStr = '';
    const paramActiveTool = this.urlQueryParamsObj['t'];
    if (paramActiveTool != undefined && paramActiveTool != 'mh') // if it is not missing and not the default active tool: MarketHealth
      wsQueryStr = '?t=' + paramActiveTool; // ?t=bav

    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/dashboard' + wsQueryStr); // "wss://127.0.0.1/ws/dashboard" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection

    this.lastTimeOpSysSleepWakeupChecked = (new Date()).getTime();
    setInterval(() => { // there is no official JS callback for sleep/wakeup events, but this is the standard way to detect waking up.
      const currentTimeNum = (new Date()).getTime();
      if (currentTimeNum > (this.lastTimeOpSysSleepWakeupChecked + 5 * 60 * 1000)) { // ignore delays less than 5 minute (that can happen because breakpoint debugging in Chrome F12)
        // detect disruptions in the JS timeline (e.g. laptop sleep, alert windows that block JS excecution, debugger statements that open the debugger)
        alert('Sleep and wakup was detected. You probably lost connection to server. Refresh (reload) page in the browser manually.'); // Probably just woke up!
      }
      this.lastTimeOpSysSleepWakeupChecked = currentTimeNum;

      this.isSrvConnectionAlive = this._socket != null && this._socket.readyState === WebSocket.OPEN;
    }, 5 * 1000); // refresh at every 5 secs
  }

  // called after Angular has initialized all data-bound properties before any of the view or content children have been checked. Called after the constructor and called  after the first ngOnChanges()
  ngOnInit() {
    gDiag.mainAngComponentOnInitTime = new Date();
    // console.log('sq.d: ' + gDiag.mainAngComponentOnInitTime.toISOString() + ': mainAngComponentOnInitTime()');  // called 21ms after constructor

    this.onSetTheme('sqClassic');

    // WebSocket connection
    gDiag.wsConnectionStartTime = new Date();
    // console.log('sq.d: ' + gDiag.wsConnectionStartTime.toISOString() + ': wsConnectionStartTime()');

    this._socket.onopen = () => { // we don't use the event param now: (event) =>
      gDiag.wsConnectionReadyTime = new Date();
      // console.log('sq.d: ' + gDiag.wsConnectionReadyTime.toISOString() + ': wsConnectionReadyTime()');
      console.log('ws: Connection started! _socket.send() can be used now.');
    };

    this._socket.onmessage = (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'All.LstVal': // this is the most frequent case. Should come first.
          if (gDiag.wsOnFirstRtMktSumRtStatTime === minDate)
            gDiag.wsOnFirstRtMktSumRtStatTime = new Date();

          gDiag.wsOnLastRtMktSumRtStatTime = new Date();
          gDiag.wsNumRtMktSumRtStat++;
          this.nLstValArrived++;

          const jsonArrayObjRt = JSON.parse(msgObjStr, function(this: any, key, value) {
            // property names and values are transformed to a shorter ones for decreasing internet traffic. 700bytes => 395bytes (-45%) per 5sec. Per hour: 500KB => 280KB Transform them back to normal for better code reading.

            // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
            // eslint-disable-next-line no-invalid-this
            const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

            if (key === 'id') {
              _this.assetId = value;
              return; // if return undefined, original property will be removed
            }
            if (key === 't') {
              const mSecSinceUnixEpoch : number = value * 1000; // data comes a seconds. JS uses milliseconds since Epoch.
              _this.lastUtc = new Date(mSecSinceUnixEpoch); // the Date object is nothing but a number.  That number is the number of milliseconds since 1970 jan 1 UTC, to now (now being in UTC also).
              return; // if return undefined, original property will be removed
            }
            if (key === 'l') {
              _this.last = ChangeNaNstringToNaNnumber(value); // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
              return; // if return undefined, original property will be removed
            }
            return value;
          });

          // this.lstValStr = jsonArrayObjRt.map((r) => r.assetId + '=>' + r.last.toFixed(2).toString()).join(', '); // It is not shown on HTML UI for quicker execution. Enable only for debugging if needed
          // console.log('ws: RtMktSumRtStat arrived: ' + this.lstValStr);  // enable only for debugging if needed
          this.lstValObj = jsonArrayObjRt;

          this.childMktHealthComponent.webSocketLstValArrived(this.lstValObj);
          this.childBrAccViewerComponent.webSocketLstValArrived(this.lstValObj);
          break;
        case 'Ping': // Server sends heartbeats, ping-pong messages to check zombie websockets.
          console.log('ws: Ping message arrived:', msgObjStr);
          if (this._socket != null && this._socket.readyState === WebSocket.OPEN)
            this._socket.send('Pong:');
          break;
        case 'OnConnected':
          gDiag.wsOnConnectedMsgArrivedTime = new Date();
          console.log('ws: OnConnected message arrived:' + event.data);

          const handshakeMsg: HandshakeMessage = Object.assign(new HandshakeMessage(), JSON.parse(msgObjStr));
          this.user.email = handshakeMsg.email;
          break;
        case 'Dshbrd.IsDshbrdOpenManyTimes':
          console.log('The Dashboard opened many times string:', msgObjStr);
          this.isDshbrdOpenManyTimes = String(msgObjStr).toLowerCase() === 'true';
          if (this.isDshbrdOpenManyTimes) {
            this.isDshbrdOpenManyTimesDialogVisible = true;
            const dialogAnimate = document.getElementById('manyDshbrdClientsDialog') as HTMLElement;
            dialogAnimate.style.animationName = 'dialogFadein';
            dialogAnimate.style.animationDuration = '3s';
            dialogAnimate.style.animationTimingFunction = 'linear'; // default would be ‘ease’, which is a slow start, then fast, before it ends slowly. We prefer the linear.
            // dialogAnimate.style.animationDelay = '0s';
            dialogAnimate.style.animationIterationCount = '1'; // only once
            dialogAnimate.style.animationFillMode = 'forwards';
          }
          break;
        default:
          let isHandled = this.childBrAccViewerComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childPortfolioManagerComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childMktHealthComponent.webSocketOnMessage(msgCode, msgObjStr);
          if (!isHandled)
            isHandled = this.childQckflNewsComponent.webSocketOnMessage(msgCode, msgObjStr);

          if (!isHandled)
            console.log('ws: Warning! OnConnected Message arrived, but msgCode is not recognized.Code:' + msgCode + ', Obj: ' + msgObjStr);

          break;
      }
    };

    // resizing the sqLogo dynamically
    window.addEventListener('resize', () => {
      const sqLogo = SqNgCommonUtils.getNonNullDocElementById('toolBarImg1');
      sqLogo.style.width = window.innerWidth * 0.00225 - 1 + 'rem'; // with the horizontal 5px padding left and right, so subtract with 1rem(10px). 0.00225 => (46/2048) which (sqLogo.clientWidth / window.innerWidth) at normal screen size.
      sqLogo.style.height = window.innerHeight * 0.0035 + 'rem'; // 0.0035 => (36/1031) which (sqLogo.clientHeight / window.innerHeight) at normal screen size.
    });

    // 'beforeunload' will be fired if the user submits a form, clicks a link, closes the window (or tab), or goes to a new page using the address bar, search box, or a bookmark.
    window.addEventListener('beforeunload', (unloadEvent) => {
      // Dispose objects logic.
      // Signal the server that it can remove this client from DashboardClient.g_clients list.
      console.log('window.beforeunload()');

      if (this._socket != null && this._socket.readyState === WebSocket.OPEN)
        this._socket.send('Dshbrd.BrowserWindowUnload:');
      else
        alert('socket not connected');

      this._socket.close(1000, 'Closing from client'); // the close() method does not discard previously-sent messages before starting that closing handshake; even if the user agent is still busy sending those messages, the handshake will only start after the messages are sent.

      // unloadEvent.preventDefault();
      // unloadEvent.returnValue = 'window.beforeunload event: Unsaved modifications are possible';  // Define the returnValue only if you want to prompt user before unload.
      return unloadEvent;
    });

    // Change the Active tool if it is requested by the Url Query String ?t=bav
    const paramActiveTool = this.urlQueryParamsObj['t'];
    if (paramActiveTool != undefined && paramActiveTool != 'mh') { // if it is not missing and not the default active tool: MarketHealth
      const uiActiveTool = this.urlParamActiveTool2UiActiveTool[paramActiveTool];
      if (uiActiveTool != undefined)
        this.onChangeActiveTool(uiActiveTool); // we need some mapping of 'bav' => 'BrAccViewer'
    }

    setTimeout(() => {
      if (this._socket != null && this._socket.readyState === WebSocket.OPEN)
        this._socket.send('Dshbrd.IsDshbrdOpenManyTimes:');
    }, 3000);
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
    if (this.activeTool === tool)
      return;

    console.log('Changing activeTool to ' + tool);
    this.activeTool = tool;
    return false; // assure that HREF will not reload the page  // https://stackoverflow.com/questions/13955667/disabled-href-tag
  }

  closeDropdownMenu(menuItem: string) {
    if (menuItem === 'Tools')
      this.isToolSelectionVisible = false;
    else if (menuItem === 'User')
      this.isUserSelectionVisible = false;
  }

  openSettings() {
    this.settingsDialogComponent.openSettingsDialog();
  }

  mouseEnter(div: string) {
    if (div === 'sqDiagnostics') {
      if (this.isSrvConnectionAlive) {
        this.sqDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.mainAngComponentConstructorTime)}\n` +
          `DOM loaded: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.dOMContentLoadedTime)}\n` +
        `Window loaded: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.windowOnLoadTime)}\n` +
        '-----\n' +
        `WS connection start in OnInit: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsConnectionStartTime)}\n` +
        `WS connection ready: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsConnectionReadyTime)}\n` +
        `WS userdata(email) arrived: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnConnectedMsgArrivedTime)}\n` +
        `WS first NonRtStat: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnFirstRtMktSumNonRtStatTime)}\n` +
        `WS first RtStat: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnFirstRtMktSumRtStatTime)}\n` + // if wsOnFirstRtMktSumRtStatTime == minTime, it can be negative
        `WS #RtStat: ${gDiag.wsNumRtMktSumRtStat}\n` +
        `WS last RtStat: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.wsOnLastRtMktSumRtStatTime, new Date())} ago\n` +
        `WS last Lookback Chg latency: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.wsOnLastRtMktSumLookbackChgStart, gDiag.wsOnLastRtMktSumNonRtStatTime)}\n` + // 14-20ms LocalDev, 27-33ms London to Dublin, thanks to the open WS connection. If a new connection has to be opened, it would be 80-130ms; 120ms Bahamas to Dublin (with a new connection it would be 500ms)
        '-----\n' +
        `WS BrAccVw first MktBrLstCls: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsBrAccVwOnFirstMktBrLstCls)}\n` +
        `WS BrAccVw last NavSelChg latency: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.wsBrAccVwOnLastNavSelectChangeStart, gDiag.wsBrAccVwOnLastNavSelectChangeEnd)}\n` +
        `WS BrAccVw last RfrSnpsht latency: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.wsBrAccVwOnLastRefreshSnapshotStart, gDiag.wsBrAccVwOnLastRefreshSnapshotEnd)}\n`;
      } else
        this.sqDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }

  onDshbrdOpenedManyTimesContinueClicked() {
    this.isDshbrdOpenManyTimesDialogVisible = false;
  }

  onDshbrdOpenedManyTimesCloseClicked() {
    window.close();
  }
}