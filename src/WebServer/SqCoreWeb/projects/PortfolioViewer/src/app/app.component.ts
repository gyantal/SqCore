import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PortfolioJs, PrtfRunResultJs, UiPrtfRunResult, prtfsParseHelper, statsParseHelper, updateUiWithPrtfRunResult, TradeAction, AssetType, CurrencyId, ExchangeId } from '../../../../TsLib/sq-common/backtestCommon';
import { SqNgCommonUtilsTime } from '../../../sq-ng-common/src/lib/sq-ng-common.utils_time';

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfToClient: PortfolioJs | null = null;
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
  note: string | null = null;

  Clear(): void {
    this.id = -1;
    this.time = new Date();
    this.action = TradeAction.Unknown;
    this.assetType = AssetType.Unknown;
    this.symbol = null;
    this.underlyingSymbol = null;
    this.quantity = 0;
    this.price = 0;
    this.currency = CurrencyId.Unknown;
    this.commission = 0;
    this.exchangeId = ExchangeId.Unknown;
    this.connectedTrades = null;
    this.note = null;
  }

  CopyFrom(tradeFrom: TradeJs): void { // a Clone function would create a new object with new MemAlloc, but we only want to copy the fields without ctor
    this.id = tradeFrom.id;
    this.time = tradeFrom.time;
    this.action = tradeFrom.action;
    this.assetType = tradeFrom.assetType;
    this.symbol = tradeFrom.symbol;
    this.underlyingSymbol = tradeFrom.underlyingSymbol;
    this.quantity = tradeFrom.quantity;
    this.price = tradeFrom.price;
    this.currency = tradeFrom.currency;
    this.commission = tradeFrom.commission;
    this.exchangeId = tradeFrom.exchangeId;
    this.connectedTrades = tradeFrom.connectedTrades;
    this.note = tradeFrom.note;
  }
}

class TradeUi extends TradeJs {
  isSelected: boolean = false; // a flag whether that row is selected (with highlighted background) in the Trades-Matrix on the UI. This allows multi-selection if it is needed in the future.

  override CopyFrom(tradeFrom: TradeUi): void { // a Clone function would create a new object with new MemAlloc, but we only want to copy the fields without ctor
    super.CopyFrom(tradeFrom);

    if (tradeFrom.isSelected == undefined) // tradeFrom parameter is defined as TradeUi. However, its runtime type can be a general JS object, when it comes from JSON.parse(), and then that field is undefined (missing).
      this.isSelected = false; // In the undefined case we set it as False, the default.
    else
      this.isSelected = tradeFrom.isSelected;
  }
}

class OptionFieldsUi {
  public optionType: string = ''; // option: Put/Call
  public strikePrice: number = 0;
  public dateExpiry: string = '';
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
  m_trades: TradeUi[] | null = null;
  m_editedTrade: TradeJs = new TradeJs();
  m_isEditedTradeDirty: boolean = false;

  m_optionFieldsUi: OptionFieldsUi = new OptionFieldsUi();
  m_enumAssetType = AssetType; // Assigning AssetType to m_enumAssetType is necessary to directly reference the enum in Angular templates. Refer: https://stackoverflow.com/questions/69549927/how-to-pass-enum-value-in-angular-template-as-an-input

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
          this.m_isEditedTradeDirty = false;
          break;
        case 'PrtfVwr.TradesHist':
          console.log('PrtfVwr.TradesHist:' + msgObjStr);
          this.processHistoricalTrades(msgObjStr);
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

  processHistoricalTrades(msgObjStr: string) {
    console.log('PrtfVwr.processHistoricalTrades() START');
    const tradeObjects : object[] = JSON.parse(msgObjStr, function(key, value) { // JSON.parse() always returns just a general a JavaScript 'object'. That doesn't have Class specific fields as 'isSelected' (Undefined)
      switch (key) { // Perform type conversion based on property names
        case 'action':
          return TradeAction[value];
        case 'assetType':
          return AssetType[value];
        case 'currency':
          return CurrencyId[value];
        case 'exchangeId':
          return ExchangeId[value];

        default: // If no type conversion needed, return the original value
          return value;
      }
    });

    // manually create an instance and then populate its properties with the values from the parsed JSON object.
    this.m_trades = new Array(tradeObjects.length);
    for (let i = 0; i < tradeObjects.length; i++) {
      this.m_trades[i] = new TradeUi();
      this.m_trades[i].CopyFrom(tradeObjects[i] as TradeUi);
    }
  }

  onClickSelectedTradeItem(trade: TradeUi, event: MouseEvent) {
    if (event.ctrlKey) // If the Ctrl key is pressed, toggle the selection of the clicked trade
      trade.isSelected = !trade.isSelected;
    else { // If Ctrl key is not pressed, deselect all previously selected trades
      for (const item of this.m_trades!)
        item.isSelected = false;

      trade.isSelected = true; // Select the clicked trade
    }

    this.m_editedTrade.CopyFrom(trade);
  }

  onClickInsertOrUpdateTrade(isInsertNew: boolean) {
    if (AssetType[this.m_editedTrade.assetType.toString()] == AssetType.Option) // When a user selects an option, the symbol comprises the underlying asset, the expiration date, the option type (put/call abbreviated as P/C), and the strike price. For instance, in the example "QQQ 20241220C494.78", "QQQ" represents the underlying symbol, "20241220" indicates the expiration date, "C" denotes a call option, and "494.78" signifies the strike price.
      this.m_editedTrade.symbol = this.m_editedTrade.underlyingSymbol + ' ' + SqNgCommonUtilsTime.RemoveHyphensFromDateStr(this.m_optionFieldsUi.dateExpiry) + this.m_optionFieldsUi.optionType + this.m_optionFieldsUi.strikePrice;

    if (isInsertNew)
      this.m_editedTrade.id = -1;
    const tradeJson: string = this.Trade2EnumJsonStr(this.m_editedTrade);
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('InsertOrUpdateTrade:pfId:' + this.m_portfolioId + ':' + tradeJson);
  }

  onClickDeleteTrade() {
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('DeleteTrade:pfId:' + this.m_portfolioId + ',tradeId:' + this.m_editedTrade.id);
  }

  onClickClearFields() {
    this.m_editedTrade.Clear();
  }

  // Trade2EnumJsonStr() - Without this conversion we will not be able to insert or update the trade in Db
  // Exception in C# Json deserialize -  System.Text.Json.JsonException: The JSON value could not be converted to Fin.Base.TradeAction.
  // when we stringify the tradeJson is - {\"id\":17,\"time\":\"2024-02-26T11:08:21\",\"action\":\"Buy\",\"assetType\":\"Stock\",\"symbol\":\"JD\",\"underlyingSymbol\":\"JD\",\"quantity\":0,\"price\":0,\"currency\":\"JPY\",\"commission\":0,\"exchangeId\":\"Unknown\",\"connectedTrades\":null}
  // whereas we need enum type for TradeAction, AssetType, Currency and ExchangeId data members.
  Trade2EnumJsonStr(editedTrade: TradeJs): string {
    const tradeJson = JSON.stringify(editedTrade, function(key, value) {
      switch (key) {
        case 'action':
          return TradeAction[value.toString()];
        case 'assetType':
          return AssetType[value.toString()];
        case 'currency':
          return CurrencyId[value.toString()];
        case 'exchangeId':
          return ExchangeId[value.toString()];

        default: // If no type conversion needed, return the original value
          return value;
      }
    }); // Convert the new trade object to JSON string
    return tradeJson;
  }

  onTradeInputChange() { // Dynamically switch between the save and unsaved icons when a user attempts to create or edit a trade.
    this.m_isEditedTradeDirty = true;
  }

  onClickSetOpenOrClose(setTime: string) {
    const now = new Date(); // Get the current date and time in the user's local time zone
    if (TradeAction[(this.m_editedTrade.action).toString()] == TradeAction.Buy) { // Buy
      if (setTime == 'open') // Set the opening time to 9:31 AM local time (NYSE opening time)
        now.setUTCHours(13, 31, 0);
      else if (setTime == 'close') // Set the closing time to 4:00 PM local time (NYSE closing time)
        now.setUTCHours(20, 0, 0);
    } else if (TradeAction[(this.m_editedTrade.action).toString()] == TradeAction.Sell) { // Sell
      if (setTime == 'open') // Set the opening time to 9:30 AM local time (NYSE opening time)
        now.setUTCHours(13, 30, 0);
      else if (setTime == 'close') // Set the closing time to 3:59 PM local time (NYSE closing time)
        now.setUTCHours(19, 59, 0);
    }

    this.m_editedTrade.time = now; // Update m_editedTrade.time with the calculated time in UTC format
  }

  setTradeTime(dateStr: string) { // display the time part of the editedTrade.time in <input> time element.
    const datePart = this.m_editedTrade.time ? new Date(this.m_editedTrade.time) : new Date(); // Get the current date part of the time

    const timePart = dateStr.split(':'); // Get the time portion from the input and set it to the date
    datePart.setHours(parseInt(timePart[0], 10));
    datePart.setMinutes(parseInt(timePart[1], 10));

    this.m_editedTrade.time = datePart; // Assign the updated date to m_editedTrade.time
  }

  onClickSelectAllOrDeselectAll(isSelectAll: boolean) {
    for (const item of this.m_trades!)
      item.isSelected = isSelectAll;
  }
}