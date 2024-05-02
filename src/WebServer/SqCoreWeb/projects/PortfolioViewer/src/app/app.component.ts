import { Component } from '@angular/core';
import { PortfolioJs, PrtfRunResultJs, UiPrtfRunResult, prtfsParseHelper, statsParseHelper, updateUiWithPrtfRunResult, TradeAction, AssetType, CurrencyId, ExchangeId, fundamentalDataParseHelper } from '../../../../TsLib/sq-common/backtestCommon';
import { SqNgCommonUtilsTime } from '../../../sq-ng-common/src/lib/sq-ng-common.utils_time';

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfToClient: PortfolioJs | null = null;
}

class TradeJs {
  id: number = -1;
  time: Date = new Date();
  action: TradeAction = TradeAction.Buy;
  assetType: AssetType = AssetType.Stock;
  symbol: string = '';
  underlyingSymbol: string = '';
  quantity: number = NaN;
  price: number = NaN;
  currency: CurrencyId = CurrencyId.USD;
  commission: number = 0;
  exchangeId: ExchangeId = ExchangeId.Unknown;
  connectedTrades: number[] | null = null;
  note: string | null = null;

  Clear(): void {
    this.id = -1;
    this.time = new Date();
    this.action = TradeAction.Buy;
    this.assetType = AssetType.Stock;
    this.symbol = '';
    this.underlyingSymbol = '';
    this.quantity = NaN;
    this.price = NaN;
    this.currency = CurrencyId.USD;
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
  public strikePrice: number = NaN;
  public dateExpiry: string = '';
}

class FuturesFieldsUi {
  public dateExpiry: string = '';
  public multiplier: number = NaN;
}

class FundamentalData {
  ticker: string = '';
  name: string = ''; // shortname
  sharesOutstanding: number = 0;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  // General fields
  m_portfolioId = -1; // -1 is invalid ID
  m_portfolio: PortfolioJs | null = null;
  m_activeTab: string = 'Positions';
  m_socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams
  m_chrtWidth: number = 0; // added only to reuse the updateUiWithPrtfRunResult method as is ( variable has no effect today(16012024) may be useful in future)
  m_chrtHeight: number = 0; // added only to reuse the updateUiWithPrtfRunResult method as is ( variable has no effect today(16012024) may be useful in future)
  m_prtfRunResult: PrtfRunResultJs | null = null;
  m_uiPrtfRunResult: UiPrtfRunResult = new UiPrtfRunResult();

  // Positions tabpage:
  m_histPosEndDate: string = '';

  // Trades tabpage: internal data
  m_trades: TradeUi[] = [];
  m_editedTrade: TradeJs = new TradeJs();
  m_editedTradeOptionFields: OptionFieldsUi = new OptionFieldsUi(); // parts of the m_editedTrade.Symbol in case of Options
  m_editedTradeFutureFields: FuturesFieldsUi = new FuturesFieldsUi(); // parts of the m_editedTrade.Symbol in case of Futures
  m_isEditedTradeDirty: boolean = false;

  // Trades tabpage: UI handling
  m_isEditedTradeSectionVisible: boolean = false; // toggle the m_editedTrade widgets on the UI
  m_isCopyToClipboardDialogVisible: boolean = false;

  // Trades tabpage: UI handling enums
  // How to pass enum value into Angular HTML? Answer: assign the Enum Type to a member variable. See. https://stackoverflow.com/questions/69549927/how-to-pass-enum-value-in-angular-template-as-an-input
  m_enumTradeAction = TradeAction;
  m_enumAssetType = AssetType;
  m_enumCurrencyId = CurrencyId;
  m_enumExchangeId = ExchangeId;

  user = {
    name: 'Anonymous',
    email: '             '
  };

  constructor() {
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
        case 'PrtfVwr.PrtfTickersFundamentalData':
          console.log('PrtfVwr.PrtfTickersFundamentalData:' + msgObjStr);
          this.processFundamentalData(msgObjStr);
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
    this.getFundamentalData();
  }

  getFundamentalData() {
    const tickers: string[] = [];
    for (const item of this.m_prtfRunResult?.prtfPoss) // Extract tickers from portfolio positions
      tickers.push(item.sqTicker.split('/')[1]);

    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('GetFundamentalData:' + '?tickers=' + tickers + '&Date=' + this.m_histPosEndDate);
  }

  public processFundamentalData(msgObjStr: string) {
    console.log('PrtfVwr.processFundamentalData() START', msgObjStr);
    const fundamentalData: FundamentalData[] = JSON.parse(msgObjStr, function(this: any, key, value) {
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      const isRemoveOriginal: boolean = fundamentalDataParseHelper(_this, key, value);
      if (isRemoveOriginal)
        return; // if return undefined, original property will be removed

      return value; // the original property will not be removed if we return the original value, not undefined
    });

    for (const prtfPosItem of this.m_uiPrtfRunResult.prtfPosValues) {
      const ticker = prtfPosItem.sqTicker.split('/')[1]; // Extract ticker from prtfPosItem sqTicker, e.g. S/TLT => TLT
      const fundamentalDataItem = fundamentalData.find((item) => item.ticker == ticker);

      if (fundamentalDataItem != null) {
        prtfPosItem.name = fundamentalDataItem.name;
        prtfPosItem.sharesOutstanding = fundamentalDataItem.sharesOutstanding;
        prtfPosItem.marketCap = prtfPosItem.price * prtfPosItem.sharesOutstanding;
      }
    }
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
    const tradeObjects : object[] = JSON.parse(msgObjStr); // The Json string contains enums as numbers, which is how we store it in RAM in JS. So, e.g. 'actionNumber as Action' type cast would be correct, but not necessary as both the input data and the output enum are 'numbers'
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
      for (const item of this.m_trades)
        item.isSelected = false;

      trade.isSelected = true; // Select the clicked trade
    }

    this.m_editedTrade.CopyFrom(trade);
    this.m_isEditedTradeDirty = false; // Reset the dirty flag, when the user selects a new item from the trades.
  }

  onClickInsertOrUpdateTrade(isInsertNew: boolean) {
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();

    if (isInsertNew)
      this.m_editedTrade.id = -1;

    const tradeJson: string = JSON.stringify(this.m_editedTrade, (key, value) => {
      switch (key) {
        case 'currency':
          if (value == CurrencyId.USD) // also omitting the value of currency , if its 'USD'.
            return undefined;
          break;
        case 'commission':
          if (value == 0) // also omitting the value of commission , if its '0'.
            return undefined;
          break;
        case 'exchangeId':
          if (value == ExchangeId.Unknown) // also omitting the value of ExchangeId , if its 'Unknown'.
            return undefined;
          break;
        default:
          if (value == null || value === '') // Omit null and empty strings
            return undefined;
          break;
      }
      return value;
    });
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('InsertOrUpdateTrade:pfId:' + this.m_portfolioId + ':' + tradeJson);
  }

  onClickDeleteTrade() {
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('DeleteTrade:pfId:' + this.m_portfolioId + ',tradeId:' + this.m_editedTrade.id);
  }

  getEditedTradeSymbol(): string {
    if (this.m_editedTrade.assetType === AssetType.Option) // When a user selects an option, the symbol comprises the underlying asset, the expiration date, the option type (put/call abbreviated as P/C), and the strike price. For instance, in the example "QQQ 20241220C494.78", "QQQ" represents the underlying symbol, "20241220" indicates the expiration date, "C" denotes a call option, and "494.78" signifies the strike price.
      return this.m_editedTrade.underlyingSymbol + ' ' + SqNgCommonUtilsTime.RemoveHyphensFromDateStr(this.m_editedTradeOptionFields.dateExpiry) + this.m_editedTradeOptionFields.optionType + (isNaN(this.m_editedTradeOptionFields.strikePrice) ? '-' : this.m_editedTradeOptionFields.strikePrice);
    else if (this.m_editedTrade.assetType === AssetType.Futures) // ex: symbol: VIX 20240423M1000 => VIX(underlyingSymbol) 20240423(Date) M(Mulitplier)1000.
      return this.m_editedTrade.underlyingSymbol + ' ' + SqNgCommonUtilsTime.RemoveHyphensFromDateStr(this.m_editedTradeFutureFields.dateExpiry) + 'M' + (isNaN(this.m_editedTradeFutureFields.multiplier) ? '-' : this.m_editedTradeFutureFields.multiplier);
    else
      return this.m_editedTrade.symbol;
  }

  onClickClearFields() {
    this.m_editedTrade.Clear();
  }

  onTradeInputChange() { // Dynamically switch between the save and unsaved icons when a user attempts to create or edit a trade.
    this.m_isEditedTradeDirty = true;
  }

  onClickSelectAllOrDeselectAll(isSelectAll: boolean) {
    for (const item of this.m_trades)
      item.isSelected = isSelectAll;
  }

  toggleTradeSectionVisibility() {
    this.m_isEditedTradeSectionVisible = !this.m_isEditedTradeSectionVisible;
  }

  onClickCopyToClipboard() {
    if (this.m_trades == null) // Check if m_trades is null or undefined
      return;

    let content = '';
    const tradeFieldNames = Object.keys(this.m_trades[0]); // Extract keys(fieldName) from the first trade object
    content += tradeFieldNames.join('\t') + '\n'; // Append keys(fieldName) as the top row in the content string, separated by tabs

    let isAnyTradeSelected = false; // The variable isAnyTradeSelected is beneficial for scenarios where the user hasn't selected any trades but wishes to copy data to the clipboard.
    for (const trade of this.m_trades) {
      if (trade.isSelected) {
        isAnyTradeSelected = true;
        break;
      }
    }

    for (const trade of this.m_trades) {
      if (trade.isSelected || !isAnyTradeSelected) { // Overwrite the user behaviour. If no row is selected, then we copy All rows to clipboard.
        for (const fieldName of tradeFieldNames)
          content += trade[fieldName] + '\t'; // Append the value of the current fieldName from the trade object to the content string, separated by tabs
        content += '\n'; // Append a new line character after appending all fieldName values for the current trade
      }
    }

    window.navigator.clipboard.writeText(content) // Write the content string to the clipboard using the navigator.clipboard
        .then(() => { this.m_isCopyToClipboardDialogVisible = true; }) // If successful, set the flag to show the copy to clipboard dialog
        .catch((error) => { console.error('Failed to copy: ', error); }); // If an error occurs, log the error to the console
  }

  onCopyDialogCloseClicked() {
    this.m_isCopyToClipboardDialogVisible = false;
  }

  onInputAssetType(assetType: AssetType) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.assetType = assetType;
  }

  onInputTradeId(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.id = parseInt((event.target as HTMLInputElement).value.trim());
  }

  onTradeActionSelectionClicked(enumTradeActionStr: any) { // e.g.: enumTradeActionStr = "Buy" as string. The ":string" type would be more accurate instead of ":any", but 'as' is not allowed in Angular HTML. AngularHtml thinks (correctly) that the enum TradeAction is a JS object = general dictionary where keys and values can be any types.
    this.m_editedTrade.action = TradeAction[enumTradeActionStr as keyof TradeAction];
    this.onTradeInputChange();
  }

  onInputSymbol(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.symbol = (event.target as HTMLInputElement).value.trim().toUpperCase();
  }

  onInputUnderlyingSymbol(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.underlyingSymbol = (event.target as HTMLInputElement).value.trim().toUpperCase();
  }

  onInputOptionType(option: string) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.optionType = option;
  }

  onInputOptionExpiry(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.dateExpiry = (event.target as HTMLInputElement).value.trim();
  }

  onInputOptionStrikePrice(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.strikePrice = parseFloat((event.target as HTMLInputElement).value.trim());
  }

  onInputFutureExpiry(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeFutureFields.dateExpiry = (event.target as HTMLInputElement).value.trim();
  }

  onInputFutureMultiplier(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeFutureFields.multiplier = parseFloat((event.target as HTMLInputElement).value.trim());
  }

  onClickSetOpenOrClose(setTime: string) {
    this.m_isEditedTradeDirty = true;
    const etDate: Date = this.m_editedTrade.time;
    if (this.m_editedTrade.action == TradeAction.Buy) { // Buy
      if (setTime == 'open') // Set the opening time to 9:31 AM local time (NYSE opening time)
        etDate.setHours(9, 31, 0);
      else if (setTime == 'close') // Set the closing time to 4:00 PM local time (NYSE closing time)
        etDate.setHours(16, 0, 0);
    } else if (this.m_editedTrade.action == TradeAction.Sell) { // Sell
      if (setTime == 'open') // Set the opening time to 9:30 AM local time (NYSE opening time)
        etDate.setHours(9, 30, 0);
      else if (setTime == 'close') // Set the closing time to 3:59 PM local time (NYSE closing time)
        etDate.setHours(15, 59, 0);
    }
    const utcDate: Date = SqNgCommonUtilsTime.ConvertDateEtToUtc(etDate);
    // this.m_editedTrade.time = utcDate; // Warning! Angular change detection doesn't notice the change without creating new object, if we just update the date's UTC milliseconds number inside the Date object
    this.m_editedTrade.time = new Date(utcDate); // Angular change detection detects only the 'pointer change'. It only notice the change if we create a new object, with a new allocated memory and new pointer.
  }

  onDateChange(event: Event) {
    this.m_isEditedTradeDirty = true;
    const dateStr: string = (event.target as HTMLInputElement).value;
    const timeStr: string = this.m_editedTrade.time.toTimeString().split(' ')[0]; // we extract the time from the current m_editedTrade.time and combine it with the new date to create a new Date object.
    this.m_editedTrade.time = new Date(dateStr + 'T' + timeStr);
  }

  onTimeChange(event: Event) {
    this.m_isEditedTradeDirty = true;
    const timeStr: string = (event.target as HTMLInputElement).value;
    const dateStr: string = this.m_editedTrade.time.toISOString().split('T')[0]; // we extract the date from the current m_editedTrade.time and combine it with the new time to create a new Date object.
    this.m_editedTrade.time = new Date(dateStr + 'T' + timeStr);
  }

  onInputPrice(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.price = parseFloat((event.target as HTMLInputElement).value.trim());
  }

  onCurrencyTypeSelectionClicked(enumCurrencyIdStr: any) { // ex: enumCurrencyIdStr = "USD"as string. The ":string" type would be more accurate instead of ":any", but 'as' is not allowed in Angular HTML. AngularHtml thinks (correctly) that the enum CurrencyId is a JS object = general dictionary where keys and values can be any types.
    this.m_editedTrade.currency = CurrencyId[enumCurrencyIdStr as keyof CurrencyId];
    this.onTradeInputChange();
  }

  onInputQuantity(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.quantity = parseInt((event.target as HTMLInputElement).value.trim());
  }
}