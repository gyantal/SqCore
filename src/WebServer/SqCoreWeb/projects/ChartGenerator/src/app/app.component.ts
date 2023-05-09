import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { gDiag } from '../../../MarketDashboard/src/sq-globals';

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
  m_http: HttpClient;
  m_portfolioId = -1; // -1 is invalid ID

  isSrvConnectionAlive: boolean = true;
  chrtGenDiagnosticsMsg = 'Benchmarking time, connection speed';

  // UrlQueryParams (keep them short): // ?t=bav
  public urlQueryParamsArr : string[][];
  public urlQueryParamsObj = {}; // empty object. If queryParamsObj['t'] doesn't exist, it returns 'undefined'
  user = {
    name: 'Anonymous',
    email: '             '
  };
  public activeTool = 'ChartGenerator';
  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams
  isSelectTreevisible: boolean = false;

  constructor(http: HttpClient) {
    gDiag.mainAngComponentConstructorTime = new Date();
    this.m_http = http;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/ChartGenerator/?id=1
    const prtfIdStr = url.searchParams.get('id');
    if (prtfIdStr != null)
      this.m_portfolioId = parseInt(prtfIdStr);

    this.urlQueryParamsArr = SqNgCommonUtils.getUrlQueryParamsArray();
    this.urlQueryParamsObj = SqNgCommonUtils.Array2Obj(this.urlQueryParamsArr);
    console.log('AppComponent.ctor: queryParamsArr.Length: ' + this.urlQueryParamsArr.length);
    console.log('AppComponent.ctor: Active Tool, queryParamsObj["t"]: ' + this.urlQueryParamsObj['t']);

    const wsQueryStr = '';
    // const paramActiveTool = this.urlQueryParamsObj['t'];
    // if (paramActiveTool != undefined && paramActiveTool != 'mh') // if it is not missing and not the default active tool: MarketHealth
    //   wsQueryStr = '?t=' + paramActiveTool; // ?t=bav

    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/dashboard" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection
  }

  ngOnInit(): void {
    // WebSocket connection
    gDiag.wsConnectionStartTime = new Date();

    this._socket.onopen = () => {
      gDiag.wsConnectionReadyTime = new Date();
      console.log('ws: Connection started! _socket.send() can be used now.');
    };

    this._socket.onmessage = (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'OnConnected':
          gDiag.wsOnConnectedMsgArrivedTime = new Date();
          console.log('ws: OnConnected message arrived:' + event.data);
          const handshakeMsg: HandshakeMessage = Object.assign(new HandshakeMessage(), JSON.parse(msgObjStr));
          this.user.email = handshakeMsg.email;
          break;
        // case 'PrtfRunResult':
        //   console.log('ws: PrtfRunResult message arrived:' + runRes);
        //   break;
        default:
          return false;
      }
    };
  }

  onSelectFromTreeClicked() { // Feature to show the entire Portfolio Tree from Portfolio Manager component - Yet to Develop
    this.isSelectTreevisible = true;
  }

  onRunBacktestClicked() {
    if (this._socket != null && this._socket.readyState === this._socket.OPEN)
      this._socket.send('RunBacktest:');
  }

  // "Server backtest time: 300ms, Communication overhead: 120ms, Total UI response: 420ms."
  mouseEnter(div: string) { // giving some data to display - Daya
    if (div === 'chrtGenDiagnosticsMsg') {
      if (this.isSrvConnectionAlive) {
        this.chrtGenDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.mainAngComponentConstructorTime)}\n` +
        `Server backtest time: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsConnectionStartTime)}\n` +
        `Communication Overhead: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnConnectedMsgArrivedTime)}\n` +
        `Total UI response: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnConnectedMsgArrivedTime)}\n`;
      } else
        this.chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }
}