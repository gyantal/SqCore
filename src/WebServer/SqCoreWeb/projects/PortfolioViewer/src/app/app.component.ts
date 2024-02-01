import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PortfolioJs, PrtfRunResultJs, UiPrtfRunResult, prtfsParseHelper, statsParseHelper, updateUiWithPrtfRunResult } from '../../../../TsLib/sq-common/backtestCommon';

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfToClient: PortfolioJs | null = null;
}

enum TradeAction {
  Unknown = 0,
  Deposit = 1,
  Withdrawal = 2,
  Buy = 3,
  Sell = 4,
  Exercise = 5,
  Expired = 6
}

enum AssetType {
  Unknown = 0,
  CurrencyCash = 1,
  CurrencyPair = 2,
  Stock = 3,
  Bond = 4,
  Fund = 5,
  Futures = 6,
  Option = 7,
  Commodity = 8,
  RealEstate = 9,
  FinIndex = 10,
  BrokerNAV = 11,
  Portfolio = 12,
  GeneralTimeSeries = 13,
  Company = 14
}

enum CurrencyId {
  Unknown = 0,
  USD = 1,
  EUR = 2,
  GBP = 3,
  GBX = 4,
  HUF = 5,
  JPY = 6,
  CNY = 7,
  CAD = 8,
  CHF = 9
}

enum ExchangeId {
  Unknown = -1,
  NASDAQ = 1,
  NYSE = 2,
  AMEX = 3,
  PINK = 4,
  CDNX = 5,
  LSE = 6,
  XTRA = 7,
  CBOE = 8,
  ARCA = 9,
  BATS = 10,
  OTCBB = 11
}

class TradeJs {
  id: number = -1;
  time: Date = new Date();
  action: TradeAction = TradeAction.Unknown;
  assetType: AssetType = AssetType.Unknown;
  symbol: string | null = null;
  underlyingSymbol: string | null = null;
  quantity: number = 0;
  price: number = 0;
  currency: CurrencyId = CurrencyId.Unknown;
  commission: number = 0;
  exchangeId: ExchangeId = ExchangeId.Unknown;
  connectedTrades: number[] | null = null;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_http: HttpClient;
  m_portfolioId = -1; // -1 is invalid ID
  m_portfolio: PortfolioJs | null = null;
  m_activeTab: string = 'Positions';
  m_socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams
  m_chrtWidth: number = 0; // added only to reuse the updateUiWithPrtfRunResult method as is ( variable has no effect today(16012024) may be useful in future)
  m_chrtHeight: number = 0; // added only to reuse the updateUiWithPrtfRunResult method as is ( variable has no effect today(16012024) may be useful in future)
  m_prtfRunResult: PrtfRunResultJs | null = null;
  m_uiPrtfRunResult: UiPrtfRunResult = new UiPrtfRunResult();
  m_histPosEndDate: string = '';
  m_trades: TradeJs[] | null = null;

  user = {
    name: 'Anonymous',
    email: '             '
  };

  constructor(http: HttpClient) {
    this.m_http = http;
    const wsQueryStr = window.location.search;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/PortfolioViewer/?pid=1
    const prtfIdStr = url.searchParams.get('pid');
    if (prtfIdStr != null)
      this.m_portfolioId = parseInt(prtfIdStr);
    this.m_socket = new WebSocket('wss://' + document.location.hostname + '/ws/prtfvwr' + wsQueryStr);
    this.m_chrtWidth = window.innerWidth as number;
    this.m_chrtHeight = window.innerHeight as number * 0.5; // 50% of window height
  }

  ngOnInit(): void {
    this.m_socket.onmessage = async (event) => {
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
            return value; // the original property will not be removed if we return the original value, not undefined
          });
          this.user.email = handshakeMsg.email;
          this.m_portfolio = handshakeMsg.prtfToClient;
          break;
        case 'PrtfVwr.PrtfRunResult':
          console.log('PrtfVwr.PrtfRunResult:' + msgObjStr);
          this.processPortfolioRunResult(msgObjStr);
          break;
        case 'PrtfVwr.TradesHist':
          console.log('PrtfVwr.TradesHist:' + msgObjStr);
          this.m_trades = JSON.parse(msgObjStr);
          break;
      }
    };
  }

  public processPortfolioRunResult(msgObjStr: string) {
    console.log('PrtfVwr.processPortfolioRunResult() START');
    this.m_prtfRunResult = JSON.parse(msgObjStr, function(this: any, key, value) {
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      const isRemoveOriginal: boolean = statsParseHelper(_this, key, value);
      if (isRemoveOriginal)
        return; // if return undefined, original property will be removed

      return value; // the original property will not be removed if we return the original value, not undefined
    });
    updateUiWithPrtfRunResult(this.m_prtfRunResult, this.m_uiPrtfRunResult, this.m_chrtWidth, this.m_chrtHeight);
  }

  onActiveTabCicked(activeTab: string) {
    this.m_activeTab = activeTab;
    if (this.m_activeTab == 'Trades')
      this.getTradesHistory();
  }

  onHistPeriodChangeClicked() { // send this when user changes the historicalPosDates
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('RunBacktest:' + '?pid=' + this.m_portfolioId + '&Date=' + this.m_histPosEndDate);
  }

  getTradesHistory() { // send this when user clicks on Trades tab
    console.log('getTradesHistory');
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('GetTradesHist:' + this.m_portfolio?.id);
  }
}