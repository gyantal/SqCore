import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { UserInput } from '../lib/gpt-common';
import { sleep } from '../../../../../../SqCoreWeb/TsLib/sq-common/utils-common';

interface NewsItem {
  Title: string;
  Description: string;
  Link: string;
  Guid: string;
  PubDate: string;
  NewsSummary: string;
  IsGptSummaryLikely: string;
  ShortDescriptionSentiment: number;
  FutureOrGrowth: string;
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

interface ServerNewsResponse {
  Logs: string[];
  Response: TickerNews[];
}

interface LlmInput {
  LlmModelName: string;
  NewsUrl: string;
  LlmQuestion: string;
}

@Component({
  selector: 'app-llm-scan',
  templateUrl: './llm-scan.component.html',
  styleUrls: ['./llm-scan.component.scss']
})

export class LlmScanComponent implements OnInit {
  m_gTickerUniverses: { [key: string]: string } = {
    'GameChanger10...': 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA',
    'GameChanger20...': 'AAPL,ADBE,AMZN,ANET,CDNS,CRM,CRWD,DE,ELF,GOOGL,LLY,MELI,META,MSFT,NOW,NVDA,SHOP,TSLA,UBER,V',
    'Nasdaq100...': 'ADBE,ADP,ABNB,ALGN,GOOG,AMZN,AMD,AEP,AMGN,ADI,ANSS,AAPL,AMAT,ASML,AZN,TEAM,ADSK,BKR,BIIB,BKNG,AVGO,CDNS,CHTR,CTAS,CSCO,CTSH,CMCSA,CEG,CPRT,CSGP,COST,CRWD,CSX,DDOG,DXCM,FANG,DLTR,EBAY,EA,ENPH,EXC,FAST,FTNT,GEHC,GILD,GFS,HON,IDXX,ILMN,INTC,INTU,ISRG,JD,KDP,KLAC,KHC,LRCX,LCID,LULU,MAR,MRVL,MELI,META,MCHP,MU,MSFT,MRNA,MDLZ,MNST,NFLX,NVDA,NXPI,ORLY,ODFL,ON,PCAR,PANW,PAYX,PYPL,PDD,PEP,QCOM,REGN,ROST,SGEN,SIRI,SBUX,SNPS,TMUS,TSLA,TXN,TTD,VRSK,VRTX,WBA,WBD,WDAY,XEL,ZM,ZS',
  }; // Nasdaq100 list from Wikipedia. 100 stocks. GOOGL is removed, because YF news result for GOOG is exactly the same
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;
  m_selectedLlmModel: string = 'auto';
  m_selectedTickers: string = '';
  m_possibleTickers: string[] = ['AMZN', 'AMZN,TSLA', 'GameChanger10...', 'GameChanger20...', 'Nasdaq100...'];
  m_tickerNewss: TickerNews[] = [];
  m_stocks: StockItem[] = [];
  m_sortColumn: string = 'PercentChange'; // default sortColumn field, pricedata is sorted initial based on the 'PercentChange'.
  m_isSortingDirectionAscending: boolean = false;
  m_isSpinnerVisible: boolean = false;

  constructor(http: HttpClient) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/';
    console.log('window.location.origin', window.location.origin);
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.countInvalidEarningsDates();
    }, 3000);
  }

  sendUserInputToBackEnd(tickersStr: string): void {
    console.log(tickersStr);
    this.m_selectedTickers = tickersStr;
    this.m_tickerNewss.length = 0; // on every userinput to get the stockPriceData, we have to empty the tickerNews array.
    this.m_isSortingDirectionAscending = true;

    let tickers = this.m_gTickerUniverses[tickersStr]; // get the value of the selected Ticker ex: if user selects ticker(key) as 'GameChanger10...' : it returns the value: 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA'
    if (tickers == null)
      tickers = tickersStr;

    // // HttpGet if input is simple and can be placed in the Url
    // // this._httpClient.get<ServerResponse>(this._baseUrl + 'chatgpt/sendString').subscribe(result => {
    // //   alert(result.Response);
    // // }, error => console.error(error));

    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: tickers };
    console.log(body);
    this.m_isSpinnerVisible = true;

    this.m_httpClient.post<ServerStockPriceDataResponse>(this.m_controllerBaseUrl + 'getstockprice', body).subscribe((result) => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this.m_stocks = result.StocksPriceResponse;
      console.log(this.m_stocks);
      this.onSortingClicked(this.m_sortColumn);
      if (this.m_stocks.length > 0) // making the spinner invisible once we recieve the data.
        this.m_isSpinnerVisible = false;
      this.getEarningsDate(tickers); // method to get earnings date.
    }, (error) => console.error(error));
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
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: selectedNewsTicker };
    console.log(body);
    this.m_httpClient.post<ServerNewsResponse>(this.m_controllerBaseUrl + 'getnews', body).subscribe(async (result) => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this.m_tickerNewss = result.Response;

      let i = 0;
      for (const tickerNews of this.m_tickerNewss) {
        for (const newsItem of tickerNews.NewsItems) {
          const body: UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: newsItem.Link };
          this.m_httpClient.post<string>(this.m_controllerBaseUrl + 'getisllmsummarylikely', body).subscribe((result) => {
            newsItem.IsGptSummaryLikely = result;
          }, (error) => console.error(error));

          i++;
          if (i % 5 == 0) // to not overwhelm the C# server, we only ask 5 downloads at once, then wait a little. The top 5 is the most important for the user at first.
            await sleep(2000);
        }
      }
      console.log(this.m_tickerNewss);
    }, (error) => console.error(error));
  }

  getNewsAndSummarize(newsItem: NewsItem) {
    console.log('link for summarizing the news', newsItem.Link);
    const questionStr = 'summarize this:\n';
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: LlmInput = { LlmModelName: this.m_selectedLlmModel, NewsUrl: newsItem.Link, LlmQuestion: questionStr };
    console.log(body);
    this.m_isSpinnerVisible = true;

    this.m_httpClient.post<string>(this.m_controllerBaseUrl + 'getllmAnswer', body).subscribe((result) => {
      newsItem.NewsSummary = result;
      this.m_isSpinnerVisible = false;
    }, (error) => console.error(error));
  }

  getFutureOrGrowthInfo(newsItem: NewsItem) {
    const questionStr = 'Is there future growth or upgrade in the next text:\n';
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: LlmInput = { LlmModelName: this.m_selectedLlmModel, NewsUrl: newsItem.Link, LlmQuestion: questionStr};
    console.log(body);
    this.m_isSpinnerVisible = true;

    this.m_httpClient.post<string>(this.m_controllerBaseUrl + 'getllmAnswer', body).subscribe((result) => {
      newsItem.FutureOrGrowth = result;
      this.m_isSpinnerVisible = false;
    }, (error) => console.error(error));
  }

  getEarningsDate(tickersStr: string) {
    const tickers: string[] = tickersStr.split(',');
    let i = 0;
    const today: Date = new Date();
    for (const ticker of tickers) {
      // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
      const body: UserInput = { LlmModelName: this.m_selectedLlmModel, Msg: ticker };
      this.m_httpClient.post<string>(this.m_controllerBaseUrl + 'earningsdate', body).subscribe(async (result) => {
        const stckItem = this.m_stocks!.find((item) => item.Ticker == ticker);
        if (stckItem != null) {
          stckItem.EarningsDate = result;
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
        i++;
        if (i % 5 == 0) // to not overwhelm the C# server, we only ask 5 downloads at once, then wait a little.
          await sleep(2000);
      }, (error) => console.error(error));
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