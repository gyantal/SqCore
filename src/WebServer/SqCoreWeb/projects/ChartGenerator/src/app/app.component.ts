import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
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

  // UrlQueryParams (keep them short): // ?t=bav
  public urlQueryParamsArr : string[][];
  public urlQueryParamsObj = {}; // empty object. If queryParamsObj['t'] doesn't exist, it returns 'undefined'
  user = {
    name: 'Anonymous',
    email: '             '
  };
  public activeTool = 'ChartGenerator';
  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams

  constructor(http: HttpClient) {
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
        default:
          return false;
      }
    };

    // Yet to Develop - Daya;
  // public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
  //   switch (msgCode) {
  //     case 'ChartGenerator.Handshake': // The least frequent message should come last.
  //       console.log('ChartGenerator.Handshake:' + msgObjStr);
  //       // this.handshakeObj = JSON.parse(msgObjStr);
  //       return true;
  //     default:
  //       return false;
  //   }
  }
}