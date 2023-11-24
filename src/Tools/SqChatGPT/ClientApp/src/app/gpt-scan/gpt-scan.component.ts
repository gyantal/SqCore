import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { UserInput } from '../lib/gpt-common';

interface NewsItem {
  Title: string;
  Description: string;
  Link: string;
  Guid: string;
  PubDate: string;
}

interface TickerNews {
  Ticker: string;
  NewsItems: NewsItem[];
}

interface StockPriceItems
{
  Ticker: string;
  PriorClose: number;
  LastPrice: number;
  PercentChange: number;
}

interface ServerStockPriceDataResponse {
  Logs: string[];
  StocksPriceResponse: StockPriceItems[];
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
  _possibleTickers: string[] = ['AMZN', 'AMZN,TSLA', 'GameChanger10...', 'GameChanger20...','Nasdaq100...'];
  _tickerNews: TickerNews[] = [];
  _stockPrices: StockPriceItems[] = [];

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._httpClient = http;
    this._baseUrl = baseUrl;
    this._controllerBaseUrl = baseUrl + 'gptscan/';
  }

  sendUserInputToBackEnd(p_tickers: string): void {
    console.log(p_tickers);
    this._selectedTickers = p_tickers;

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

    this._httpClient.post<ServerStockPriceDataResponse>(this._controllerBaseUrl + 'getstockprice', body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._stockPrices = result.StocksPriceResponse;
      console.log(this._stockPrices);
    }, error => console.error(error));

  }
}

