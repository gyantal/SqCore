import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

enum PctChnSignal { Unknown = 0, NonValidBull = 1, NonValidBear = 2, ValidBull = 3, ValidBear = 4 }

class PctChnData {
  public Date: Date = new Date();
  public pctChnSignal1: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal2: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal3: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal4: PctChnSignal = PctChnSignal.Unknown;
  public pctChnWeightAggregate: number = 0;
}

class AssetHistData {
  // public sqTicker = ''; // “S/TSLA” // sqTicker identifies the asset uniquely in C#, but we don’t need that at the moment. Here we use only the Symbol.
  public symbol = ''; // “TSLA”
  public pctChnDatas: PctChnData[] = [];
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
  m_assetHistDatas: AssetHistData[] = [];
  m_enumPctChnSignal = PctChnSignal;

  constructor(http: HttpClient) {
    this.m_httpClient = http;

    // Angular ctor @Inject('BASE_URL') contains the full path: 'https://sqcore.net/webapps/TechnicalAnalyzer', but we have to call our API as 'https://sqcore.net/TechnicalAnalyzer/GetPctChnData', so we need the URL without the '/webapps/TechnicalAnalyzer' Path.
    // And anyway, better to go non-Angular for less complexity. And 'window.location' is the fastest, native JS option for getting the URL.
    this.m_controllerBaseUrl = window.location.origin + '/TechnicalAnalyzer/'; // window.location.origin (URL without the path) = Local: "https://127.0.0.1:4206", Server: https://sqcore.net"
    console.log('window.location.origin', window.location.origin);

    const url = new URL(window.location.href); // https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=TSLA,MSFT
    this.m_tickersStr = url.searchParams.get('tickers');
    if (this.m_tickersStr != null && this.m_tickersStr.length != 0) // If there are no tickers in the URL, do not process the request to obtain getPctChnData.
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
    const url: string = this.m_controllerBaseUrl + 'GetPctChnData'; // Server: it needs to be https://sqcore.net/webapps/TechnicalAnalyzer/GetPctChnData
    this.m_httpClient.post<string>(url, body).subscribe((response) => {
      console.log('percentage channel data:', response);
      this.processAssetHistPctChnData(response);
    }, (error) => console.error(error));
  }

  processAssetHistPctChnData(assetPctChnData: any) {
    this.m_assetHistDatas.length = 0; // empty the m_assetHistDatas
    for (const assetData of assetPctChnData) {
      const assetHistData = new AssetHistData();
      assetHistData.symbol = assetData.Item1;
      for (let i = assetData.Item2.length - 1; i >= 0; i--) { // The data is currently sorted in ascending order by date, but for display on the UI, we need the latest date at the top followed by earlier dates.
        const pctChn = assetData.Item2[i];
        const pctChnData = new PctChnData();
        pctChnData.Date = new Date(pctChn.Item1);
        pctChnData.pctChnWeightAggregate = pctChn.Item2;
        for (let j = 0; j < 4; j++)
          pctChnData[`pctChnSignal${j + 1}`] = pctChn.Item3[j].Item2;
        assetHistData.pctChnDatas.push(pctChnData);
      }
      this.m_assetHistDatas.push(assetHistData);
    }
  }
}