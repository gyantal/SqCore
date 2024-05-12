import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

class Asset {
  // public sqTicker = ''; // “S/TSLA” // sqTicker identifies the asset uniquely in C#, but we don’t need that at the moment. Here we use only the Symbol.
  public symbol = ''; // “TSLA”
  public pctChnValue1: number = 0; // 0% or 100%
  public pctChnValue2: number = 0;
  public pctChnValue3: number = 0;
  public pctChnValue4: number = 0;
  public pctChnValueAggregate: number = 0; // 0% or 25% 50% 75% 100%
}

class HistData {
  Date: String = '';
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
      'Date': '2024-05-10',
      'Assets': [
        {'symbol': 'TSLA', 'pctChnValue1': 0, 'pctChnValue2': 0.25, 'pctChnValue3': 0.55, 'pctChnValue4': 0.11, 'pctChnValueAggregate': 0.91 },
        {'symbol': 'MSFT', 'pctChnValue1': 0.1, 'pctChnValue2': 0.22, 'pctChnValue3': 0.45, 'pctChnValue4': 0.65, 'pctChnValueAggregate': 1.42 },
        {'symbol': 'TLT', 'pctChnValue1': 0, 'pctChnValue2': 0.25, 'pctChnValue3': 0.55, 'pctChnValue4': 0.11, 'pctChnValueAggregate': 0.91 },
        {'symbol': 'SPY', 'pctChnValue1': 0, 'pctChnValue2': 0.25, 'pctChnValue3': 0.55, 'pctChnValue4': 0.1, 'pctChnValueAggregate': 0.81 }
      ]
    },
    {
      'Date': '2024-05-09',
      'Assets': [
        {'symbol': 'TSLA', 'pctChnValue1': 0.05, 'pctChnValue2': 0.28, 'pctChnValue3': 0.52, 'pctChnValue4': 0.09, 'pctChnValueAggregate': 0.94 },
        {'symbol': 'MSFT', 'pctChnValue1': 0.12, 'pctChnValue2': 0.21, 'pctChnValue3': 0.41, 'pctChnValue4': 0.68, 'pctChnValueAggregate': 1.42 },
        {'symbol': 'TLT', 'pctChnValue1': 0.03, 'pctChnValue2': 0.22, 'pctChnValue3': 0.58, 'pctChnValue4': 0.1, 'pctChnValueAggregate': 0.93 },
        {'symbol': 'SPY', 'pctChnValue1': 0.01, 'pctChnValue2': 0.24, 'pctChnValue3': 0.57, 'pctChnValue4': 0.12, 'pctChnValueAggregate': 0.94 }
      ]
    }]; // dummy data

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = baseUrl + 'TechnicalAnalyzer/';
    console.log('BaseURl', baseUrl);

    const url = new URL(window.location.href); // https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=TSLA,MSFT
    this.m_tickersStr = url.searchParams.get('tickers');
  }

  ngOnInit(): void {
    console.log('ngOnInit()');
  }

  onInputTickers(event: Event) {
    const tickersStr = (event.target as HTMLInputElement).value.trim().toUpperCase();
    const url = this.m_controllerBaseUrl + 'GetPctChnData';
    this.m_httpClient.post<string>(url, tickersStr).subscribe((response) => {
      console.log('percentage channel data:', response);
    }, (error) => console.error(error));
  }
}