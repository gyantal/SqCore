import { Component, Input, OnInit } from '@angular/core';
import { markdown2HtmlFormatter } from '../../../../../TsLib/sq-common/utils_string';

class NewsItem {
  Title: string = '';
  Description: string = '';
  Link: string = '';
  Guid: string = '';
  PubDate: string = '';
  NewsSummary: string = '';
  IsGptSummaryLikely: string = '';
  ShortDescriptionSentiment: number = 0;
  FutureOrGrowth: string = '';
}

interface TickerNews {
  Ticker: string;
  NewsItems: NewsItem[];
}

interface StockItem {
  Ticker: string;
  PriorClose: number;
  LastPrice: number;
  PercentChange: number;
  EarningsDate: string;
  IsPriceChangeEarningsRelated: boolean;
  [key: string]: string | number | boolean; // Adding a string based Indexer to the interface. Classes have this Indexer by default. This allows to use the field name as a string to access properties. Very useful for sorting based on columns sort((a, b) => a[sortColumn] > b[sortColumn] ? ...)
}

interface ServerStockPriceDataResponse {
  Logs: string[];
  StocksPriceResponse: StockItem[];
}

interface LlmInput {
  LlmModelName: string;
  NewsUrl: string;
  LlmQuestion: string;
}

class TickerEarningsDate {
  Ticker = '';
  EarningsDateStr = '';
}

@Component({
  selector: 'app-llm-scan',
  templateUrl: './llm-scan.component.html',
  styleUrls: ['./llm-scan.component.scss']
})

export class LlmScanComponent implements OnInit {
  @Input() m_parentWsConnection?: WebSocket | null = null; // this property will be input from above parent container

  m_gTickerUniverses: { [key: string]: string } = {
    'GameChanger10...': 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA',
    'GameChanger20...': 'AAPL,ADBE,AMZN,ANET,CDNS,CRM,CRWD,DE,ELF,GOOGL,LLY,MELI,META,MSFT,NOW,NVDA,SHOP,TSLA,UBER,V',
    'Nasdaq100...': 'ADBE,ADP,ABNB,ALGN,GOOG,AMZN,AMD,AEP,AMGN,ADI,ANSS,AAPL,AMAT,ASML,AZN,TEAM,ADSK,BKR,BIIB,BKNG,AVGO,CDNS,CHTR,CTAS,CSCO,CTSH,CMCSA,CEG,CPRT,CSGP,COST,CRWD,CSX,DDOG,DXCM,FANG,DLTR,EBAY,EA,ENPH,EXC,FAST,FTNT,GEHC,GILD,GFS,HON,IDXX,ILMN,INTC,INTU,ISRG,JD,KDP,KLAC,KHC,LRCX,LCID,LULU,MAR,MRVL,MELI,META,MCHP,MU,MSFT,MRNA,MDLZ,MNST,NFLX,NVDA,NXPI,ORLY,ODFL,ON,PCAR,PANW,PAYX,PYPL,PDD,PEP,QCOM,REGN,ROST,SGEN,SIRI,SBUX,SNPS,TMUS,TSLA,TXN,TTD,VRSK,VRTX,WBA,WBD,WDAY,XEL,ZM,ZS',
  }; // Nasdaq100 list from Wikipedia. 100 stocks. GOOGL is removed, because YF news result for GOOG is exactly the same

  m_selectedLlmModel: string = 'grok';
  m_selectedTickers: string = '';
  m_possibleTickers: string[] = ['AMZN', 'AMZN,TSLA', 'GameChanger10...', 'GameChanger20...', 'Nasdaq100...'];
  m_tickerNewss: TickerNews[] = [];
  m_stocks: StockItem[] = [];
  m_sortColumn: string = 'PercentChange'; // default sortColumn field, pricedata is sorted initial based on the 'PercentChange'.
  m_isSortingDirectionAscending: boolean = false;
  m_isSpinnerVisible: boolean = false;
  m_selectedNewsItem: NewsItem = new NewsItem();
  m_countInvalidEarningsDates: number = 0;

  constructor() { }

  ngOnInit(): void { }

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'StockPrice':
        console.log('StockPrice', msgObjStr);
        const stocksPriceData: ServerStockPriceDataResponse = JSON.parse(msgObjStr);
        this.m_stocks = stocksPriceData.StocksPriceResponse;
        console.log(this.m_stocks);
        this.onSortingClicked(this.m_sortColumn);
        return true;
      case 'EarningsDate':
        console.log('EarningsDate', msgObjStr);
        const tickerEarningsDate: TickerEarningsDate[] = JSON.parse(msgObjStr);
        this.processEarningsDate(tickerEarningsDate);
        this.m_countInvalidEarningsDates = this.countInvalidEarningsDates();
        this.m_isSpinnerVisible = false;
        return true;
      case 'TickersNews':
        console.log('TickersNews', msgObjStr);
        this.m_tickerNewss = JSON.parse(msgObjStr);
        this.m_isSpinnerVisible = false;
        return true;
      case 'LlmSummary':
        console.log('LlmSummary', msgObjStr);
        for (const tickerNews of this.m_tickerNewss) {
          for (const newsItem of tickerNews.NewsItems) {
            if (newsItem.Guid == this.m_selectedNewsItem.Guid)
              newsItem.NewsSummary = markdown2HtmlFormatter(msgObjStr);
          }
        }
        this.m_isSpinnerVisible = false;
        return true;
      case 'LlmFutureOrGrowth':
        console.log('LlmFutureOrGrowth', msgObjStr);
        for (const tickerNews of this.m_tickerNewss) {
          for (const newsItem of tickerNews.NewsItems) {
            if (newsItem.Guid == this.m_selectedNewsItem.Guid)
              newsItem.FutureOrGrowth = msgObjStr;
          }
        }
        this.m_isSpinnerVisible = false;
        return true;
      default:
        return false;
    }
  }

  sendUserInputToBackEnd(tickersStr: string): void {
    console.log(tickersStr);
    this.m_selectedTickers = tickersStr;
    this.m_tickerNewss.length = 0; // on every userinput to get the stockPriceData, we have to empty the tickerNews array.
    this.m_isSortingDirectionAscending = true;

    let tickers = this.m_gTickerUniverses[tickersStr]; // get the value of the selected Ticker ex: if user selects ticker(key) as 'GameChanger10...' : it returns the value: 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA'
    if (tickers == null)
      tickers = tickersStr;

    this.m_isSpinnerVisible = true;
    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetStockPrice:' + tickers);
  }

  onSortingClicked(sortColumn: string) { // sort the stockprices data table
    this.m_sortColumn = sortColumn;
    this.m_stocks = this.m_stocks.sort((n1: StockItem, n2: StockItem) => {
      if (this.m_isSortingDirectionAscending)
        return (n1[sortColumn] > n2[sortColumn]) ? 1 : ((n1[sortColumn] < n2[sortColumn]) ? -1 : 0);
      else
        return (n2[sortColumn] > n1[sortColumn]) ? 1 : ((n2[sortColumn] < n1[sortColumn]) ? -1 : 0);
    });
    this.m_isSortingDirectionAscending = !this.m_isSortingDirectionAscending;
  }

  onClickGetNews(selectedNewsTicker: string) {
    this.m_isSpinnerVisible = true;
    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetTickerNews:' + selectedNewsTicker);
  }

  getNewsAndSummarize(newsItem: NewsItem) {
    this.m_selectedNewsItem = newsItem;
    console.log('link for summarizing the news', newsItem.Link);
    const questionStr = 'Summarize this:\n';
    const usrInp: LlmInput = { LlmModelName: this.m_selectedLlmModel, NewsUrl: newsItem.Link, LlmQuestion: questionStr };

    this.m_isSpinnerVisible = true;
    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetLlmAnswer:' + JSON.stringify(usrInp));
  }

  getFutureOrGrowthInfo(newsItem: NewsItem) {
    this.m_selectedNewsItem = newsItem;
    const questionStr = 'Is there future growth or upgrade in the next text:\n';
    const usrInp: LlmInput = { LlmModelName: this.m_selectedLlmModel, NewsUrl: newsItem.Link, LlmQuestion: questionStr};

    this.m_isSpinnerVisible = true;
    if (this.m_parentWsConnection != null && this.m_parentWsConnection.readyState == this.m_parentWsConnection.OPEN)
      this.m_parentWsConnection.send('GetLlmAnswer:' + JSON.stringify(usrInp));
  }

  processEarningsDate(tickersEarningsDate: TickerEarningsDate[]) {
    const today: Date = new Date();
    for (const tickerErnDt of tickersEarningsDate) {
      const stckItem = this.m_stocks!.find((item) => item.Ticker == tickerErnDt.Ticker);
      if (stckItem != null) {
        stckItem.EarningsDate = tickerErnDt.EarningsDateStr;
        // Calculate the number of days to subtract based on the current day of the week
        // If today is Monday (where getDay() returns 1), subtract 3 days to skip the weekend (Saturday and Sunday)
        // Otherwise, subtract 1 day to account for the previous trading day
        // Please be aware that the provided code is effective only when the earningsDate is in the format "Apr 25, 2024". It does not handle formats like "Apr 25, 2024 - Apr 29, 2024".
        // The Earnings Date of Format "Apr 25, 2024 - Apr 29, 2024" is basically a future date, the actual earning date will fall in between them when the specified month arrives
        // Testing Date - const earningsdate: Date = new Date('2024-01-30') and const today: Date = new Date('2024-01-30');
        const earningsdate: Date = new Date(stckItem.EarningsDate);
        const previousTradingDay: Date = new Date(today);
        previousTradingDay.setDate(previousTradingDay.getDate() - (previousTradingDay.getDay() == 1 ? 3 : 1));
        stckItem.IsPriceChangeEarningsRelated = earningsdate.toDateString() == today.toDateString() || earningsdate.toDateString() == previousTradingDay.toDateString(); // today == earningsDate or earingsDate equals to previous day
      }
    }
  }

  // When we encounter invalid data from Yahoo Finance or cannot find the Earnings Date in the link (e.g., https://finance.yahoo.com/quote/AAPL), the EarningsDate is set to null.
  countInvalidEarningsDates(): number {
    let count = 0;
    for (const stckItem of this.m_stocks) {
      if (stckItem.EarningsDate == null)
        count++;
    }
    return count;
  }
}