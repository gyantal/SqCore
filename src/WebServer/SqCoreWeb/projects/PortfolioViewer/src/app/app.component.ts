import { Component } from '@angular/core';
import { PortfolioJs, PrtfRunResultJs, UiPrtfRunResult, prtfsParseHelper, statsParseHelper, updateUiWithPrtfRunResult, TradeAction, AssetType, CurrencyId, ExchangeId, fundamentalDataParseHelper, TickerClosePrice, SeasonalityData, getSeasonalityData, UiSeasonalityChartPoint, getDetailedStats, ChartResolution } from '../../../../TsLib/sq-common/backtestCommon';
import { minDate, SqNgCommonUtilsTime } from '../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { drawBarChartFromSeasonalityData } from '../../../../TsLib/sq-common/chartSimple';
import { BacktestDetailedStatistics } from '../../../../TsLib/sq-common/backtestStatistics';
import * as d3 from 'd3';

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
  m_userWarning: string | null = null;

  // Positions tabpage:
  m_histPosEndDate: string = '';
  m_positionsTabSortColumn: string = 'plPct';
  m_isPositionsTabSortDirAscend: boolean = false;

  // PortfolioReport tabpage:
  m_seasonalityData: SeasonalityData = new SeasonalityData();
  m_detailedStatistics: BacktestDetailedStatistics = new BacktestDetailedStatistics();

  // Trades tabpage: internal data
  m_trades: TradeUi[] = [];
  m_editedTrade: TradeJs = new TradeJs();
  m_editedTradeOptionFields: OptionFieldsUi = new OptionFieldsUi(); // parts of the m_editedTrade.Symbol in case of Options
  m_editedTradeFutureFields: FuturesFieldsUi = new FuturesFieldsUi(); // parts of the m_editedTrade.Symbol in case of Futures
  m_isEditedTradeDirty: boolean = false;

  // Trades tabpage: UI handling
  m_isEditedTradeSectionVisible: boolean = false; // toggle the m_editedTrade widgets on the UI
  m_editedTradeTotalValue: number = 0; // UI helper variable, which is not part of the TradeJs data. Used for displaying/editing the total$Value on UI
  m_isCopyToClipboardDialogVisible: boolean = false;

  // Trades tabpage: UI handling enums
  // How to pass enum value into Angular HTML? Answer: assign the Enum Type to a member variable. See. https://stackoverflow.com/questions/69549927/how-to-pass-enum-value-in-angular-template-as-an-input
  m_enumTradeAction = TradeAction;
  m_enumAssetType = AssetType;
  m_enumCurrencyId = CurrencyId;
  m_enumExchangeId = ExchangeId;

  // LegacyDbTrades tabpage
  m_legacyDbInsCompletionDateUtcStr: string = SqNgCommonUtilsTime.Date2PaddedIsoStr(SqNgCommonUtilsTime.ConvertDateLocToEt(new Date())); // convert the localDate to UTC then convert to DateStr format "YYYY-MM-DD". The HTML date <input> requires a YYYY-MM-DD formatted 'string' as value. E.g. <input type="date" value="2017-06-01" />
  m_legacyDbInsTradesSyntaxCheckResult: string = '';
  m_legacyDbTradesTestAndInsertResult: string = '';
  m_legacyDbInsTrades: TradeJs[] = [];
  m_legacyDbTrades: TradeUi[] = [];
  m_legacyDbTradesMaxDate: Date = minDate;

  // common for both trades and legacyDbTrades
  m_tradesTabSortColumn: string = 'time';
  m_isTradesTabSortDirAscend: boolean = false;

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
          this.m_trades = AppComponent.processHistoricalTrades(msgObjStr);
          this.onSortingTradesClicked(this.m_tradesTabSortColumn);
          break;
        case 'PrtfVwr.PrtfTickersFundamentalData':
          console.log('PrtfVwr.PrtfTickersFundamentalData:' + msgObjStr);
          this.processFundamentalData(msgObjStr);
          break;
        case 'PrtfVwr.TickerClosePrice':
          console.log('PrtfVwr.TickerClosePrice:' + msgObjStr);
          const closePriceObj: TickerClosePrice = JSON.parse(msgObjStr);
          this.m_editedTrade.price = closePriceObj.closePrice;
          break;
        case 'PrtfVwr.LegacyDbTradesTestAndInsert':
          console.log('PrtfVwr.LegacyDbTradesTestAndInsert:' + msgObjStr);
          this.m_legacyDbTradesTestAndInsertResult = msgObjStr;
          if (this.m_legacyDbTradesTestAndInsertResult.startsWith('OK')) {
            this.getLegacyDbPortfolioTradeHistory();
            const tradesStrInputElement = document.getElementById('inputTradesStr') as HTMLTextAreaElement;
            tradesStrInputElement.value = ''; // empty the textarea, if the trades are processed and pushed into the Sql database
          }
          break;
        case 'PrtfVwr.LegacyDbTradesHist':
          console.log('PrtfVwr.LegacyDbTradesHist:' + msgObjStr);
          this.m_legacyDbTrades = AppComponent.processHistoricalTrades(msgObjStr);
          this.m_legacyDbTradesMaxDate = AppComponent.getLegacyDbTradesMaxDate(this.m_legacyDbTrades);
          this.m_isTradesTabSortDirAscend = false;
          this.onSortingLegacyDbTradesClicked(this.m_tradesTabSortColumn);
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
    if (this.m_prtfRunResult?.chrtData.chartResolution == ChartResolution.Minute || this.m_prtfRunResult?.chrtData.chartResolution == ChartResolution.Minute5) // Check if the portfolio is of per minute resolution
      this.m_userWarning = 'PerMinute strategies not fully supported';
    updateUiWithPrtfRunResult(this.m_prtfRunResult, this.m_uiPrtfRunResult, this.m_chrtWidth, this.m_chrtHeight);
    this.onSortingPositionsClicked(this.m_positionsTabSortColumn);
    this.getFundamentalData();
    this.m_seasonalityData = getSeasonalityData(this.m_prtfRunResult!.chrtData);
    let isMonthlySeasonalityAvgArrayAllNaN: boolean = true; // This is added to resolve console errors when plotting the bar chart for mean and median. The seasonality data array contains only NaN values, e.g., for portfolioId = 7, which has data for only 5 days.
    for (let i = 0; i < this.m_seasonalityData.monthlySeasonalityAvg.length; i++) {
      if (!isNaN(this.m_seasonalityData.monthlySeasonalityAvg[i])) {
        isMonthlySeasonalityAvgArrayAllNaN = false;
        break;
      }
    }
    if (!isMonthlySeasonalityAvgArrayAllNaN)
      this.processUiWithSeasonalityChart(this.m_seasonalityData);
    this.m_detailedStatistics = getDetailedStats(this.m_prtfRunResult!.chrtData, this.m_prtfRunResult!.name);
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
        prtfPosItem.marketCap = prtfPosItem.priorClose * prtfPosItem.sharesOutstanding;
      }
    }
  }

  onActiveTabCicked(activeTab: string) {
    this.m_activeTab = activeTab;
    if (this.m_activeTab == 'Trades')
      this.getTradesHistory();

    if (this.m_activeTab == 'LegacyDbTrades')
      this.getLegacyDbPortfolioTradeHistory();
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

  static processHistoricalTrades(msgObjStr: string): TradeUi[] {
    console.log('PrtfVwr.processHistoricalTrades() START');
    const tradeObjects : object[] = JSON.parse(msgObjStr, function(key, value) {
      if (key == 'time')
        return new Date(value); // converting time value of string type to Date object, e.g. ("2023-01-04T02:31:00" -> "Wed Jan 04 2023 02:31:00").
      else
        return value;
    }); // The Json string contains enums as numbers, which is how we store it in RAM in JS. So, e.g. 'actionNumber as Action' type cast would be correct, but not necessary as both the input data and the output enum are 'numbers'
    // manually create an instance and then populate its properties with the values from the parsed JSON object.
    const trades: TradeUi[] = new Array(tradeObjects.length);
    for (let i = 0; i < tradeObjects.length; i++) {
      trades[i] = new TradeUi();
      trades[i].CopyFrom(tradeObjects[i] as TradeUi);
    }
    return trades;
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
    this.m_editedTradeTotalValue = parseFloat((this.m_editedTrade.price * this.m_editedTrade.quantity).toFixed(2)); // calculate the totalValue for the user selected item
    this.m_isEditedTradeDirty = false; // Reset the dirty flag, when the user selects a new item from the trades.
  }

  onClickInsertOrUpdateTrade(isInsertNew: boolean) {
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();

    if (isInsertNew)
      this.m_editedTrade.id = -1;

    const tradeJson: string = this.tradeStringifyHelper(this.m_editedTrade);
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
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
  }

  onInputOptionType(option: string) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.optionType = option;
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
  }

  onInputOptionExpiry(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.dateExpiry = (event.target as HTMLInputElement).value.trim();
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
  }

  onInputOptionStrikePrice(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeOptionFields.strikePrice = parseFloat((event.target as HTMLInputElement).value.trim());
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
  }

  onInputFutureExpiry(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeFutureFields.dateExpiry = (event.target as HTMLInputElement).value.trim();
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
  }

  onInputFutureMultiplier(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeFutureFields.multiplier = parseFloat((event.target as HTMLInputElement).value.trim());
    this.m_editedTrade.symbol = this.getEditedTradeSymbol();
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
    this.updateEditedTradePriceFromPrHist();
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
    this.m_editedTradeTotalValue = parseFloat((this.m_editedTrade.price * this.m_editedTrade.quantity).toFixed(2)); // calculate the totalValue when use enter the price
  }

  onCurrencyTypeSelectionClicked(enumCurrencyIdStr: any) { // ex: enumCurrencyIdStr = "USD"as string. The ":string" type would be more accurate instead of ":any", but 'as' is not allowed in Angular HTML. AngularHtml thinks (correctly) that the enum CurrencyId is a JS object = general dictionary where keys and values can be any types.
    this.m_editedTrade.currency = CurrencyId[enumCurrencyIdStr as keyof CurrencyId];
    this.onTradeInputChange();
  }

  onInputQuantity(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTrade.quantity = parseInt((event.target as HTMLInputElement).value.trim());
    this.m_editedTradeTotalValue = parseFloat((this.m_editedTrade.price * this.m_editedTrade.quantity).toFixed(2)); // calculate the totalValue when use enter the quantity
  }

  onInputTotalValue(event: Event) {
    this.m_isEditedTradeDirty = true;
    this.m_editedTradeTotalValue = parseInt((event.target as HTMLInputElement).value);
    this.m_editedTrade.quantity = this.calculateQuantity(this.m_editedTrade.price, this.m_editedTradeTotalValue);
  }

  calculateQuantity(price: number, totalVal: number): number {
    // For example, if the stock price is 9.9 and the user has $989, the Quantity he can buy is 99. We have to round it to the lower integer."
    let quantity = Math.floor(totalVal / price); // Calculate the maximum quantity the user can afford
    if (quantity * price > totalVal) // Check if the calculated quantity is correct by recalculating the total cost
      quantity -= 1;
    return quantity;
  }

  onBlurTotalValue() { // This ensures the recalculated (adjusted) totalValue is based on the price and quantity.
    this.m_editedTradeTotalValue = Math.floor(this.m_editedTrade.price * this.m_editedTrade.quantity);
  }

  updateEditedTradePriceFromPrHist() { // fetch the historical Close Price on that date from YF
    if (this.m_editedTrade.symbol == '')// If the symbol is empty, it means the user either forgot to enter a symbol or did not select an existing one. In this scenario, we should not send the request to the server when the user tries to change the date.
      return;
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('GetClosePrice:Symb:' + this.m_editedTrade.symbol + ',Date:' + SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_editedTrade.time));
  }

  onSortingTradesClicked(sortColumn: string) { // sort the trades data table
    this.m_tradesTabSortColumn = sortColumn;
    this.sortTrades(this.m_trades, this.m_isTradesTabSortDirAscend, this.m_tradesTabSortColumn);
  }

  onClickNextOrPrevDate(nextOrPrev: string) {
    if (nextOrPrev == 'next') {
      this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() + 1));
      const newDayOfWeek = this.m_editedTrade.time.getDay();// Check if the new date is a weekend, if so, move to the next working day
      if (newDayOfWeek == 6) // Saturday
        this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() + 2));
      else if (newDayOfWeek == 0) // Sunday
        this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() + 1));
    } else {
      this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() - 1));
      const newDayOfWeek = this.m_editedTrade.time.getDay(); // Check if the new date is a weekend, if so, move to the previous working day
      if (newDayOfWeek == 6) // Saturday
        this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() - 1));
      else if (newDayOfWeek == 0) // Sunday
        this.m_editedTrade.time = new Date(this.m_editedTrade.time.setDate(this.m_editedTrade.time.getDate() - 2));
    }
    this.updateEditedTradePriceFromPrHist();
  }

  processUiWithSeasonalityChart(seasonalityData: SeasonalityData) {
    d3.selectAll('#seasonalityChrt > *').remove();
    const barChrtDiv = document.getElementById('seasonalityChrt') as HTMLElement;
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const meanAndMedianSeasonalityData: UiSeasonalityChartPoint[] = [];

    for (let i = 0; i < seasonalityData.monthlySeasonalityAvg.length; i++) { // Iterate through monthly seasonality data to create chart points
      const chartItem: UiSeasonalityChartPoint = {
        month: months[i],
        mean: seasonalityData.monthlySeasonalityAvg[i] * 100, // multiplying mean and median values by 100 to convert to percentages
        median: seasonalityData.monthlySeasonalityMedian[i] * 100
      };
      meanAndMedianSeasonalityData.push(chartItem);
    }
    const margin = { top: 30, right: 60, bottom: 30, left: 60 };
    const chartWidth = this.m_chrtWidth * 0.95 - margin.left - margin.right; // 95% of the PanelChart Width
    const chartHeight = this.m_chrtHeight * 0.4 - margin.top - margin.bottom; // 40% of the PanelChart Height
    drawBarChartFromSeasonalityData(meanAndMedianSeasonalityData, barChrtDiv, chartWidth, chartHeight, margin);
  }

  onSortingPositionsClicked(sortColumn: string) { // sort the postions data table
    this.m_positionsTabSortColumn = sortColumn;
    this.m_uiPrtfRunResult.prtfPosValues = this.m_uiPrtfRunResult.prtfPosValues.sort((n1, n2) => {
      if (this.m_isPositionsTabSortDirAscend)
        return (n1[sortColumn] > n2[sortColumn]) ? 1 : ((n1[sortColumn] < n2[sortColumn]) ? -1 : 0);
      else
        return (n2[sortColumn] > n1[sortColumn]) ? 1 : ((n2[sortColumn] < n1[sortColumn]) ? -1 : 0);
    });
    this.m_isPositionsTabSortDirAscend = !this.m_isPositionsTabSortDirAscend;
  }

  onInputLegacyDbDateInsertion(event: Event) {
    this.m_legacyDbInsCompletionDateUtcStr = (event.target as HTMLInputElement).value.trim();
  }

  onClickConvertTradesStrToTradesJs() {
    this.m_legacyDbInsTradesSyntaxCheckResult = '';
    this.m_legacyDbTradesTestAndInsertResult = '';
    this.m_legacyDbInsTrades.length = 0;
    const tradesStrInputElement = document.getElementById('inputTradesStr') as HTMLTextAreaElement;
    const tradesStr: string = tradesStrInputElement.value;
    if (tradesStr == '') { // check if the tradesStr has data
      this.m_legacyDbInsTradesSyntaxCheckResult = 'Error. No trade was recognized';
      return;
    }

    const tradeRows: string[] = tradesStr.trim().split('\n'); // Split the data by newline to get each row
    for (let i = 0; i < tradeRows.length; i++) {
      const tradeRowStr = tradeRows[i];
      const tradeDataProcessResult: { tradeObj: TradeJs | null; errorStr: string | null } = this.validateAndProcessTradeData(tradeRowStr, i);
      if (tradeDataProcessResult.errorStr != null) {
        this.m_legacyDbInsTradesSyntaxCheckResult = tradeDataProcessResult.errorStr;
        return;
      } else if (tradeDataProcessResult.tradeObj != null) {
        this.m_legacyDbInsTradesSyntaxCheckResult = 'Syntax OK';
        this.m_legacyDbInsTrades.push(tradeDataProcessResult.tradeObj);
      }
    }
  }

  validateAndProcessTradeData(tradeRowStr: string, rowInd:number): { tradeObj: TradeJs | null; errorStr: string | null } {
    const errorStr: string | null = null;
    const tradeObj: TradeJs | null = new TradeJs();
    if (tradeRowStr.startsWith('-')) // Check for partial trades
      return { tradeObj: null, errorStr: `Error. Processing stopped. "-" was found at row ${rowInd + 1 }, indicating partial trades are present. Remove partial trades.` };

    tradeRowStr = tradeRowStr.startsWith('+') ? tradeRowStr.substring(2) : tradeRowStr.startsWith('\t') ? tradeRowStr.substring(1) : tradeRowStr; // removing the '+' and '\t' from the tradeRecord
    const trade: string[] = tradeRowStr.split('\t');
    if (trade.length < 2) // check to validate the tradeStr has valid trade data
      return { tradeObj: null, errorStr: `Error. Trade "${trade}" is invalid.` };
    const quantity = Number(trade[2].replace(/,/g, '')); // replace the comma and convert to number
    if (isNaN(quantity) || !isFinite(quantity) || !Number.isInteger(quantity)) // Check if quantity is a valid number (including floats) and is finite
      return { tradeObj: null, errorStr: `Error. Quantity ${trade[2]} at row ${rowInd + 1} is invalid.` };
    tradeObj.quantity = quantity; // Assign the valid quantity to tradeObj

    const price = Number(trade[3].replace(/,/g, '')); // replace the comma and convert to number
    if (isNaN(price) || !isFinite(price)) // Check if price is a valid number (including floats) and is finite
      return { tradeObj: null, errorStr: `Error. Price ${trade[3]} at row ${rowInd + 1} is invalid.` };
    tradeObj.price = price;

    if (CurrencyId[trade[4]] == undefined) // Validate CurrencyId
      return { tradeObj: null, errorStr: `Error. CurrencyId: '${trade[4]}' at row ${rowInd + 1} is invalid.` };
    tradeObj.currency = CurrencyId[trade[4]];

    switch (trade[0]) { // Validate and process TradeAction
      case 'BOT':
        tradeObj.action = TradeAction.Buy;
        break;
      case 'SLD':
        tradeObj.action = TradeAction.Sell;
        break;
      case 'DEPOSIT':
        tradeObj.action = TradeAction.Deposit;
        break;
      default:
        return { tradeObj: null, errorStr: `Error. TradeAction: '${trade[0]}' at row ${rowInd + 1} is invalid.` };
    }

    const validTradeDtStr: string | null = SqNgCommonUtilsTime.ValidateDateStr(trade[5], rowInd); // ValidateDateStr() - returns errorStr or null if success.
    if (validTradeDtStr != null)
      return { tradeObj: null, errorStr: validTradeDtStr };

    const tradeDt: Date = new Date(trade[5]);
    const completionDateStr: string[] = this.m_legacyDbInsCompletionDateUtcStr.split('-');
    if (isNaN(tradeDt.getTime())) { // If tradeDt is invalid, assume it only contains the time part.
      const timeStr: string = trade[5];
      tradeDt.setUTCFullYear(parseInt(completionDateStr[0], 10), parseInt(completionDateStr[1], 10) - 1, parseInt(completionDateStr[2], 10));
      tradeDt.setHours(0, 0, 0, 0); // Reset time to start of the day
      const timeParts: string[] = timeStr.split(':'); // Split the timeStr and manually parse each part
      tradeDt.setHours(parseInt(timeParts[0], 10), parseInt(timeParts[1], 10), parseInt(timeParts[2], 10)); // Set the time on tradeDt to the parsed hours, minutes, and seconds
    }
    tradeDt.setUTCFullYear(parseInt(completionDateStr[0], 10));
    tradeObj.time = tradeDt;
    switch (trade[8]) { // Validate and process AssetType
      case 'STK':
        tradeObj.assetType = AssetType.Stock;
        break;
      case 'OPT':
        tradeObj.assetType = AssetType.Option;
        break;
      default:
        return { tradeObj: null, errorStr: `Error. AssetType: '${trade[8]}' at row ${rowInd + 1} is invalid.` };
    }
    tradeObj.symbol = trade[1];
    return { tradeObj, errorStr };
  }

  tradeStringifyHelper(trade: TradeJs): string {
    // Date in JSON problem: The JSON.stringify() uses Date.toISOString(), which from a Local date (2024-01-18T18:30:00.000), creates a string as "2024-01-18T13:00:00.000Z", but we don't want this format with the Server communication
    // Furthermore, it converts the time from Local time to UTC, and we don't want that it changes the time in any way.
    // Potential fixes that we tried:
    // 1. JSON.stringify() doesn't have a Bool parameter or a Config parameter that controls this behaviour.
    // 2. Overwriting Date.prototype.toJSON = function(){} is possible, but we don't like overwriting Global functions (that could be used somewhere else in the code)
    // 3. We can create a temporary local variable editedTradeToServer, and do this Date => string custom conversion ourselves, before calling JSON.stringify() with the cloned object.
    // E.g. const editedTradeToServer = new TradeJs();
    // editedTradeToServer.CopyFrom(this.m_editedTrade);
    // Sorry TypeScript!: instead of introducing a new class TradeJsToServer just for changing the type from Date to string, we push the string object into that '.time' field, which is supposed to be Date.
    // but it is only temporary (before sending to the Server), and only for this local variable.
    // editedTradeToServer.time = SqNgCommonUtilsTime.DateTime2PaddedIsoStr(editedTradeToServer.time) as any; // putting the 'string' into the 'Date' field. Violation of TS rules, but fine. Target format is: "2023-12-10T21:00:00"
    // Disadvantages: 1. need to MemCopy Clone the whole this.m_editedTrade big oject. 2. We have to use "as any" to convince TS to fill the Date field with a String
    // 4. The JSON.stringify() (key, value) => callback function receives key='time', value='2024-01-18T13:00:00.000Z' as String, that is already a converted string. The Date object doesn't arrive here unfortunatelly.
    // But we can remedy it to do the inverse of Date.toISOString(), which is the ctor 'new Date(ISO-Utc-string), that will give us back the original Date (in local timezone)
    // And we can use our utility function DateTime2PaddedIsoStr() to get the string representation of that local Date.
    const tradeStr = JSON.stringify(trade, (key:string, value: any) => {
      switch (key) {
        case 'time':
          const tradeTime: Date = new Date(value); // JSON.stringify() already used Date.toISOString() to convert it to a string. We do the inverse to get back the original Date.
          const newValue = SqNgCommonUtilsTime.DateTime2PaddedIsoStr(tradeTime); // get the string representation without converting to Utc and without the "Z" postfix
          return newValue;
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
    return tradeStr;
  }

  onClickTestAndInsertLegacyDbTrades() {
    // Trades are sent to legacyDb as a JSON-formatted string
    let legacyDbInsTradesJsonStr = '[';
    for (let i =0; i < this.m_legacyDbInsTrades.length; i++) {
      const tradeJs: string = this.tradeStringifyHelper(this.m_legacyDbInsTrades[i]);
      legacyDbInsTradesJsonStr += tradeJs;

      if (i < this.m_legacyDbInsTrades.length - 1) // Only add comma if it's not the last element
        legacyDbInsTradesJsonStr += ',';
    }
    legacyDbInsTradesJsonStr += ']';

    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('LegacyDbTradesTestAndInsert:legacyPfName:' + this.m_portfolio?.legacyDbPortfName + '&trades' + legacyDbInsTradesJsonStr);
  }

  getLegacyDbPortfolioTradeHistory() {
    if (this.m_socket != null && this.m_socket.readyState == this.m_socket.OPEN)
      this.m_socket.send('LegacyDbTradesHist:legacyPfName:' + this.m_portfolio?.legacyDbPortfName);
  }

  onSortingLegacyDbTradesClicked(sortColumn: string) { // sort the LegacyDbTrades data table
    this.m_tradesTabSortColumn = sortColumn;
    this.sortTrades(this.m_legacyDbTrades, this.m_isTradesTabSortDirAscend, this.m_tradesTabSortColumn);
  }

  sortTrades(trades: TradeUi[], isSortDirAscend: boolean, sortColumn: string) { // sort trades or legacyDbTrades
    trades = trades.sort((n1, n2) => {
      if (isSortDirAscend)
        return (n1[sortColumn] > n2[sortColumn]) ? 1 : ((n1[sortColumn] < n2[sortColumn]) ? -1 : 0);
      else
        return (n2[sortColumn] > n1[sortColumn]) ? 1 : ((n2[sortColumn] < n1[sortColumn]) ? -1 : 0);
    });
    this.m_isTradesTabSortDirAscend = !this.m_isTradesTabSortDirAscend;
  }

  static getLegacyDbTradesMaxDate(trades: TradeUi[]): Date {
    let maxDate = minDate;
    for (let i = 0; i < trades.length; i++) {
      if (trades[i].time > maxDate)
        maxDate = trades[i].time;
    }
    return maxDate;
  }
}