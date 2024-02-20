import { Component, Inject, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { UserInput } from '../lib/gpt-common';
import { sleep } from '../../../../../../WebServer/SqCoreWeb/TsLib/sq-common/utils-common'

interface NewsItem {
  Title: string;
  Description: string;
  Link: string;
  Guid: string;
  PubDate: string;
  NewsSummary: string;
  IsGptSummaryLikely: string;
  ShortDescriptionSentiment: number;
  FullTextSentiment: number;
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

interface ChatGptInput {
  LlmModelName: string;
  NewsUrl: string;
  ChatGptQuestion: string;
}

@Component({
  selector: 'app-gpt-scan',
  templateUrl: './gpt-scan.component.html',
  styleUrls: ['./gpt-scan.component.scss']
})
export class GptScanComponent implements OnInit {
  _gTickerUniverses: { [key: string]: string } = {
    'GameChanger10...': 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA',
    'GameChanger20...': 'AAPL,ADBE,AMZN,ANET,CDNS,CRM,CRWD,DE,ELF,GOOGL,LLY,MELI,META,MSFT,NOW,NVDA,SHOP,TSLA,UBER,V',
    'Nasdaq100...': 'ADBE,ADP,ABNB,ALGN,GOOG,AMZN,AMD,AEP,AMGN,ADI,ANSS,AAPL,AMAT,ASML,AZN,TEAM,ADSK,BKR,BIIB,BKNG,AVGO,CDNS,CHTR,CTAS,CSCO,CTSH,CMCSA,CEG,CPRT,CSGP,COST,CRWD,CSX,DDOG,DXCM,FANG,DLTR,EBAY,EA,ENPH,EXC,FAST,FTNT,GEHC,GILD,GFS,HON,IDXX,ILMN,INTC,INTU,ISRG,JD,KDP,KLAC,KHC,LRCX,LCID,LULU,MAR,MRVL,MELI,META,MCHP,MU,MSFT,MRNA,MDLZ,MNST,NFLX,NVDA,NXPI,ORLY,ODFL,ON,PCAR,PANW,PAYX,PYPL,PDD,PEP,QCOM,REGN,ROST,SGEN,SIRI,SBUX,SNPS,TMUS,TSLA,TXN,TTD,VRSK,VRTX,WBA,WBD,WDAY,XEL,ZM,ZS',
  }; // Nasdaq100 list from Wikipedia. 100 stocks. GOOGL is removed, because YF news result for GOOG is exactly the same
  _httpClient: HttpClient;
  _baseUrl: string;
  _controllerBaseUrl: string;
  _selectedLlmModel: string  = 'auto';
  _selectedTickers: string = '';
  _possibleTickers: string[] = ['AMZN', 'AMZN,TSLA', 'GameChanger10...', 'GameChanger20...', 'Nasdaq100...'];
  _tickerNewss: TickerNews[] = [];
  _stocks: StockItem[] = [];
  _sortColumn: string = 'PercentChange'; // default sortColumn field, pricedata is sorted initial based on the 'PercentChange'.
  _isSortingDirectionAscending: boolean = false;
  _isSpinnerVisible: boolean = false;

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._httpClient = http;
    this._baseUrl = baseUrl;
    this._controllerBaseUrl = baseUrl + 'gptscan/';
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.countInvalidEarningsDates();
    }, 3000);
  }

  sendUserInputToBackEnd(p_tickers: string): void {
    console.log(p_tickers);
    this._selectedTickers = p_tickers;
    this._tickerNewss.length = 0; // on every userinput to get the stockPriceData, we have to empty the tickerNews array.
    this._isSortingDirectionAscending = true;

    let tickers = this._gTickerUniverses[p_tickers]; // get the value of the selected Ticker ex: if user selects ticker(key) as 'GameChanger10...' : it returns the value: 'ADBE,AMZN,ANET,CRM,GOOG,LLY,MSFT,NOW,NVDA,TSLA'
    if(tickers == null)
      tickers = p_tickers;

    // // HttpGet if input is simple and can be placed in the Url
    // // this._httpClient.get<ServerResponse>(this._baseUrl + 'chatgpt/sendString').subscribe(result => {
    // //   alert(result.Response);
    // // }, error => console.error(error));

    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: tickers };
    console.log(body);
    this._isSpinnerVisible = true;

    this._httpClient.post<ServerStockPriceDataResponse>(this._controllerBaseUrl + 'getstockprice', body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._stocks = result.StocksPriceResponse;
      console.log(this._stocks);
      this.onSortingClicked(this._sortColumn);
      if (this._stocks.length > 0) // making the spinner invisible once we recieve the data.
        this._isSpinnerVisible = false;
      this.getEarningsDate(tickers) // method to get earnings date.
    }, error => console.error(error));
  }

  onSortingClicked(sortColumn: string) { // sort the stockprices data table
    this._stocks = this._stocks.sort((n1: StockItem, n2: StockItem) => {
      if (this._isSortingDirectionAscending)
        return (n1[sortColumn] > n2[sortColumn]) ? 1 : ((n1[sortColumn] < n2[sortColumn]) ? -1 : 0);
      else
        return (n2[sortColumn] > n1[sortColumn]) ? 1 : ((n2[sortColumn] < n1[sortColumn]) ? -1 : 0);
    });
    this._isSortingDirectionAscending = !this._isSortingDirectionAscending;
  }

  onClickGetNews(selectedNewsTicker: string) {
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: selectedNewsTicker };
    console.log(body);
    this._httpClient.post<ServerNewsResponse>(this._controllerBaseUrl + 'getnews', body).subscribe(async result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._tickerNewss = result.Response;

      let i = 0;
      for (const tickerNews of this._tickerNewss) {
        for (const newsItem of tickerNews.NewsItems) {
          const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: newsItem.Link };
          this._httpClient.post<string>(this._controllerBaseUrl + 'getisgptsummarylikely', body).subscribe(result => {
            newsItem.IsGptSummaryLikely = result;
          }, error => console.error(error))

          i++;
          if (i % 5 == 0) // to not overwhelm the C# server, we only ask 5 downloads at once, then wait a little. The top 5 is the most important for the user at first.
            await sleep(2000);
        }
      }
      console.log(this._tickerNewss);
    }, error => console.error(error));
  }

  getNewsAndSummarize(newsItem: NewsItem) {
    console.log('link for summarizing the news', newsItem.Link);
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: newsItem.Link };
    console.log(body);
    this._isSpinnerVisible = true;

    this._httpClient.post<string>(this._controllerBaseUrl + 'summarizenews', body).subscribe(result => {
      newsItem.NewsSummary = result;
      this._isSpinnerVisible = false;
    }, error => console.error(error))
  }

  getFutureOrGrowthInfo(newsItem: NewsItem) {
    const questionStr = 'Is there future growth or upgrade in the next text:\n';
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: ChatGptInput = { LlmModelName: this._selectedLlmModel, NewsUrl: newsItem.Link, ChatGptQuestion: questionStr};
    console.log(body);
    this._isSpinnerVisible = true;

    this._httpClient.post<string>(this._controllerBaseUrl + 'getChatGptAnswer', body).subscribe(result => {
      newsItem.FutureOrGrowth = result;
      this._isSpinnerVisible = false;
    }, error => console.error(error))
  }

  getNewsSentiment(newsItem: NewsItem) {
    console.log('link for finding the sentiment of the news', newsItem.Link);
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: newsItem.Link };
    console.log(body);
    this._isSpinnerVisible = true;

    this._httpClient.post<string>(this._controllerBaseUrl + 'newssentiment', body).subscribe(result => {
      newsItem.FullTextSentiment = parseFloat(result);
      this._isSpinnerVisible = false;
    }, error => console.error(error))
  }

  getEarningsDate(tickersStr: string) {
    let tickers: string[] = tickersStr.split(',');
    let i = 0;
    const today: Date = new Date();
    for (const ticker of tickers) {
      // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
      const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: ticker };
      this._httpClient.post<string>(this._controllerBaseUrl + 'earningsdate', body).subscribe(async result => {
        const stckItem = this._stocks!.find((item) => item.Ticker == ticker);
        if(stckItem != null) {
          stckItem.EarningsDate = result;
          // Calculate the number of days to subtract based on the current day of the week
          // If today is Monday (where getDay() returns 1), subtract 3 days to skip the weekend (Saturday and Sunday)
          // Otherwise, subtract 1 day to account for the previous trading day
          // Please be aware that the provided code is effective only when the earningsDate is in the format "Apr 25, 2024". It does not handle formats like "Apr 25, 2024 - Apr 29, 2024".
          // The Earnings Date of Format "Apr 25, 2024 - Apr 29, 2024" is basically a future date, the actual earning date will fall in between them when the specified month arrives
          // Testing Date - const earningsdate: Date = new Date('2024-01-30') and const today: Date = new Date('2024-01-30');
          const earningsdate: Date = new Date(stckItem.EarningsDate);
          const previousTradingDay: Date = new Date(today);
          previousTradingDay.setDate(previousTradingDay.getDate() - (previousTradingDay.getDay() == 1 ? 3 : 1))
          stckItem.IsPriceChangeEarningsRelated = earningsdate.toDateString() == today.toDateString() || earningsdate.toDateString() == previousTradingDay.toDateString(); // today == earningsDate or earingsDate equals to previous day
        }
        i++;
        if (i % 5 == 0) // to not overwhelm the C# server, we only ask 5 downloads at once, then wait a little.
          await sleep(2000);
      }, error => console.error(error));
    }
  }

  // When we encounter invalid data from Yahoo Finance or cannot find the Earnings Date in the link (e.g., https://finance.yahoo.com/quote/AAPL), the EarningsDate is set to null.
  countInvalidEarningsDates(): number {
    let count = 0;
    for (const stckItem of this._stocks) {
      if (stckItem.EarningsDate == null)
        count++;
    }
    return count;
  }
}
