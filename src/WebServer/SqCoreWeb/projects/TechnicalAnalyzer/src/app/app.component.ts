import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

class Asset {
  // public sqTicker = ''; // “S/TSLA” // sqTicker identifies the asset uniquely in C#, but we don’t need that at the moment. Here we use only the Symbol.
  public symbol = ''; // “TSLA”
  public pctChnSignal1: number = 0; // 0% or 100%
  public pctChnSignal2: number = 0;
  public pctChnSignal3: number = 0;
  public pctChnSignal4: number = 0;
  public pctChnWeightAggregate: number = 0; // 0% or 25% 50% 75% 100%
}

class HistData {
  Date: Date = new Date();
  Assets: Asset[] = [];
}

// SnapshotData; contain only the latest values of that Technical factor. E.g. SMA50, SMA200.  m_snapDatas;
// HistoricalData (having data for different dates); m_histDatas;

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;
  m_tickersStr: string | null = null;
  m_histDatas: HistData[] = [
    {
      'Date': new Date('2024-05-10'),
      'Assets': [
        {'symbol': 'TSLA', 'pctChnSignal1': 0, 'pctChnSignal2': 0.25, 'pctChnSignal3': 0.55, 'pctChnSignal4': 0.11, 'pctChnWeightAggregate': 0.91 },
        {'symbol': 'MSFT', 'pctChnSignal1': 0.1, 'pctChnSignal2': 0.22, 'pctChnSignal3': 0.45, 'pctChnSignal4': 0.65, 'pctChnWeightAggregate': 1.42 },
        {'symbol': 'TLT', 'pctChnSignal1': 0, 'pctChnSignal2': 0.25, 'pctChnSignal3': 0.55, 'pctChnSignal4': 0.11, 'pctChnWeightAggregate': 0.91 },
        {'symbol': 'SPY', 'pctChnSignal1': 0, 'pctChnSignal2': 0.25, 'pctChnSignal3': 0.55, 'pctChnSignal4': 0.1, 'pctChnWeightAggregate': 0.81 }
      ]
    },
    {
      'Date': new Date('2024-05-9'),
      'Assets': [
        {'symbol': 'TSLA', 'pctChnSignal1': 0.05, 'pctChnSignal2': 0.28, 'pctChnSignal3': 0.52, 'pctChnSignal4': 0.09, 'pctChnWeightAggregate': 0.94 },
        {'symbol': 'MSFT', 'pctChnSignal1': 0.12, 'pctChnSignal2': 0.21, 'pctChnSignal3': 0.41, 'pctChnSignal4': 0.68, 'pctChnWeightAggregate': 1.42 },
        {'symbol': 'TLT', 'pctChnSignal1': 0.03, 'pctChnSignal2': 0.22, 'pctChnSignal3': 0.58, 'pctChnSignal4': 0.1, 'pctChnWeightAggregate': 0.93 },
        {'symbol': 'SPY', 'pctChnSignal1': 0.01, 'pctChnSignal2': 0.24, 'pctChnSignal3': 0.57, 'pctChnSignal4': 0.12, 'pctChnWeightAggregate': 0.94 }
      ]
    }]; // dummy data

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = baseUrl + 'TechnicalAnalyzer/';
    console.log('BaseURl', baseUrl);

    const url = new URL(window.location.href); // https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=TSLA,MSFT
    this.m_tickersStr = url.searchParams.get('tickers');
    if (this.m_tickersStr != null) // If there are no tickers in the URL, do not process the request to obtain getPctChnData.
      this.getPctChnData(this.m_tickersStr);
  }

  ngOnInit(): void {
    console.log('ngOnInit()');
  }

  onInputTickers(event: Event) {
    this.m_tickersStr = (event.target as HTMLInputElement).value.trim().toUpperCase();
    this.getPctChnData(this.m_tickersStr);
  }

  getPctChnData(tickersStr: string) {
    const body: object = { Tickers: tickersStr };
    const url: string = this.m_controllerBaseUrl + 'GetPctChnData';
    this.m_httpClient.post<string>(url, body).subscribe((response) => {
      console.log('percentage channel data:', response);
    }, (error) => console.error(error));
  }
}