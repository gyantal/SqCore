import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { UserInput } from '../lib/gpt-common';

interface NewsItem {
  Title: string;
  Description: string;
  Link: string;
  Guid: string;
  PubDate: string;
  NewsSummary: string;
}

interface TickerNews {
  Ticker: string;
  NewsItems: NewsItem[];
}

interface StockPriceItem
{
  Ticker: string;
  PriorClose: number;
  LastPrice: number;
  PercentChange: number;
  [key: string]: string | number; // Adding [key: string]: string | number; to the StockPriceItems interface, this allows us to use any string as an index to access properties.
}

interface ServerStockPriceDataResponse {
  Logs: string[];
  StocksPriceResponse: StockPriceItem[];
}

interface ServerNewsResponse {
  Logs: string[];
  Response: TickerNews[];
}

@Component({
  selector: 'app-gpt-scan',
  templateUrl: './gpt-scan.component.html',
  styleUrls: ['./gpt-scan.component.scss']
})
export class GptScanComponent {
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
  _stockPrices: StockPriceItem[] = [];
  sortColumn: string = 'PercentChange'; // default sortColumn field, pricedata is sorted initial based on the 'PercentChange'.
  isSortingDirectionAscending: boolean = false;
  isSpinnerVisible: boolean = false;

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._httpClient = http;
    this._baseUrl = baseUrl;
    this._controllerBaseUrl = baseUrl + 'gptscan/';
  }

  sendUserInputToBackEnd(p_tickers: string): void {
    console.log(p_tickers);
    this._selectedTickers = p_tickers;
    this._tickerNewss.length = 0; // on every userinput to get the stockPriceData, we have to empty the tickerNews array.
    this.isSortingDirectionAscending = true;

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
    this.isSpinnerVisible = true;

    this._httpClient.post<ServerStockPriceDataResponse>(this._controllerBaseUrl + 'getstockprice', body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._stockPrices = result.StocksPriceResponse;
      console.log(this._stockPrices);
      this.onSortingClicked(this.sortColumn);
      if (this._stockPrices.length > 0) // making the spinner invisible once we recieve the data.
        this.isSpinnerVisible = false;
    }, error => console.error(error));
  }

  onSortingClicked(sortColumn: string) { // sort the stockprices data table
    this._stockPrices = this._stockPrices.sort((n1: StockPriceItem, n2: StockPriceItem) => {
      if (this.isSortingDirectionAscending)
        return (n1[sortColumn] > n2[sortColumn]) ? 1 : ((n1[sortColumn] < n2[sortColumn]) ? -1 : 0);
      else
        return (n2[sortColumn] > n1[sortColumn]) ? 1 : ((n2[sortColumn] < n1[sortColumn]) ? -1 : 0);
    });
    this.isSortingDirectionAscending = !this.isSortingDirectionAscending;
  }

  onClickGetNews(selectedNewsTicker: string) {
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: selectedNewsTicker };
    console.log(body);
    this._httpClient.post<ServerNewsResponse>(this._controllerBaseUrl + 'getnews', body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._tickerNewss = result.Response;
      console.log(this._tickerNewss);
    }, error => console.error(error));
  }

  getNewsAndSummarize(newsItem: NewsItem) {
    console.log('link for summarizing the news', newsItem.Link);
    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: newsItem.Link };
    console.log(body);

    this._httpClient.post<string>(this._controllerBaseUrl + 'summarizenews', body).subscribe(result => {
      newsItem.NewsSummary = result;
    }, error => console.error(error))
  }
}
