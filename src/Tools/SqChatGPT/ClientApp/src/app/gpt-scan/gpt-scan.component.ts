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
  ClosePrice: number;
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
  _httpClient: HttpClient;
  _baseUrl: string;
  _controllerBaseUrl: string;

  _selectedLlmModel: string  = 'auto';

  _selectedTickers: string = '';
  _possibleTickers: string[] = ['AAPL', 'AMZN', 'AMZN,TSLA', 'TSLA'];
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

    // // HttpGet if input is simple and can be placed in the Url
    // // this._httpClient.get<ServerResponse>(this._baseUrl + 'chatgpt/sendString').subscribe(result => {
    // //   alert(result.Response);
    // // }, error => console.error(error));

    // HttpPost if input is complex with NewLines and ? characters, so it cannot be placed in the Url, but has to go in the Body
    const body: UserInput = { LlmModelName: this._selectedLlmModel, Msg: p_tickers };
    console.log(body);

    this._httpClient.post<ServerStockPriceDataResponse>(this._controllerBaseUrl + 'sendStockPriceData', body).subscribe(result => { // if message comes as a properly formatted JSON string ("\n" => "\\n")
      this._stockPrices = result.StocksPriceResponse;
      console.log(this._stockPrices);
    }, error => console.error(error));

  }
}

