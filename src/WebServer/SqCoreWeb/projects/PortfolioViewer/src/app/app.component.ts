import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FolderJs, PortfolioJs, fldrsParseHelper, prtfsParseHelper } from '../../../../TsLib/sq-common/backtestCommon';

type Nullable<T> = T | null;

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfsToClient: Nullable<PortfolioJs[]> = null;
  public fldrsToClient: Nullable<FolderJs[]> = null;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_http: HttpClient;
  m_portfolioId = -1; // -1 is invalid ID
  activeTab: string = 'Positions';
  _allPortfolios: Nullable<PortfolioJs[]> = null;
  _allFolders: Nullable<FolderJs[]> = null;
  public gPortfolioIdOffset: number = 10000;
  _backtestedPortfolios: PortfolioJs[] = [];
  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams

  user = {
    name: 'Anonymous',
    email: '             '
  };

  constructor(http: HttpClient) {
    this.m_http = http;
    const wsQueryStr = window.location.search;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/PortfolioViewer/?id=1
    const prtfIdStr = url.searchParams.get('id');
    if (prtfIdStr != null)
      this.m_portfolioId = parseInt(prtfIdStr);
    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/prtfvwr' + wsQueryStr);
  }

  ngOnInit(): void {
    this._socket.onmessage = async (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'OnConnected':
          console.log('ws: OnConnected message arrived:' + event.data);

          const handshakeMsg: HandshakeMessage = JSON.parse(msgObjStr, function(this: any, key: string, value: any) {
            // eslint-disable-next-line no-invalid-this
            const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used
            const isRemoveOriginalPrtfs: boolean = prtfsParseHelper(_this, key, value);
            if (isRemoveOriginalPrtfs)
              return; // if return undefined, original property will be removed
            const isRemoveOriginalFldrs: boolean = fldrsParseHelper(_this, key, value);
            if (isRemoveOriginalFldrs)
              return; // if return undefined, original property will be removed
            return value; // the original property will not be removed if we return the original value, not undefined
          });
          this.user.email = handshakeMsg.email;
          this._allPortfolios = handshakeMsg.prtfsToClient;
          this._allFolders = handshakeMsg.fldrsToClient;
          // Get the Url param of PrtfIds and fill the backtestedPortfolios
          if (this._allPortfolios == null) // it can be null if Handshake message is wrong.
            return;
          const url = new URL(window.location.href);
          const prtfStrIds: string[] = url.searchParams.get('id')!.trim().split(',');
          for (let i = 0; i < prtfStrIds.length; i++) {
            for (let j = 0; j < this._allPortfolios.length; j++) {
              const id = this._allPortfolios[j].id - this.gPortfolioIdOffset;
              if (id == parseInt(prtfStrIds[i]))
                this._backtestedPortfolios.push(this._allPortfolios[j]);
            }
          }
          break;
      }
    };
  }

  onActiveTabCicked(activeTab: string) {
    this.activeTab = activeTab;
  }
}