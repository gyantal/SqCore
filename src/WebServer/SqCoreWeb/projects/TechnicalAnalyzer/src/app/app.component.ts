import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { drawHistChartFromData } from '../../../../TsLib/sq-common/chartSimple';
import { AssetHistValues } from '../../../../TsLib/sq-common/sq-globals';
import * as d3 from 'd3';


enum PctChnSignal { Unknown = 0, NonValidBull = 1, NonValidBear = 2, ValidBull = 3, ValidBear = 4 }

class PctChnData {
  public Date: Date = new Date();
  public pctChnWeightAggregate: number = 0;
  public pctChnVal1: number = 0;
  public pctChnVal2: number = 0;
  public pctChnVal3: number = 0;
  public pctChnVal4: number = 0;
  public pctChnSignal1: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal2: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal3: PctChnSignal = PctChnSignal.Unknown;
  public pctChnSignal4: PctChnSignal = PctChnSignal.Unknown;
}

class AssetHistData {
  // public sqTicker = ''; // “S/TSLA” // sqTicker identifies the asset uniquely in C#, but we don’t need that at the moment. Here we use only the Symbol.
  public symbol = ''; // “TSLA”
  public pctChnDatas: PctChnData[] = [];
}


// Hist chart values
class UiChrtval {
  public date = new Date('2021-01-01');
  public sdaClose = NaN;
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
  m_pctChnDataForTooltip: PctChnData = new PctChnData();
  m_isShowPctChnTooltip: boolean = false;
  m_isMouseInStckSymbolCell: boolean = false;
  m_stockSymbol: string = ''; // used in stckChrt
  m_isShowStckChrtTooltip: boolean = false;
  m_isMouseInTooltip: boolean = false;

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

  onMouseoverPctChnWtAggCell() {
    this.m_isShowPctChnTooltip = true;
  }

  onMouseenterPctChnWtAggCell(event: MouseEvent, pctChnData: PctChnData) {
    this.m_pctChnDataForTooltip = pctChnData; // Assign the passed in percentage change data to a property used for displaying the tooltip.
    const pctChnTooltipId: HTMLElement = (document.getElementById('pctChnTooltipText') as HTMLElement); // Get the tooltip element from the DOM where the tooltip text will be displayed.
    this.tooltipPositioning(event, pctChnTooltipId);
  }

  onMouseleavePctChnWtAggCell() {
    this.m_isShowPctChnTooltip = false;
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
      assetHistData.symbol = assetData.t; // 't' is Ticker
      for (let i = assetData.ad.length - 1; i >= 0; i--) { // The data is currently sorted in ascending order by date, but for display on the UI, we need the latest date at the top followed by earlier dates.
        const pctChn = assetData.ad[i]; // 'ad' is AggregateDatePctlChannel
        const pctChnData = new PctChnData();
        pctChnData.Date = pctChn.d; // 'd' is Date
        pctChnData.pctChnWeightAggregate = pctChn.a; // 'a' is aggregate value
        for (let j = 0; j < 4; j++) {
          pctChnData[`pctChnVal${j + 1}`] = pctChn.c[j].v; // 'v' is pctChnvalue
          pctChnData[`pctChnSignal${j + 1}`] = pctChn.c[j].s; // 's' is pctChnSig
        }
        assetHistData.pctChnDatas.push(pctChnData);
      }
      this.m_assetHistDatas.push(assetHistData);
    }
  }

  onMouseoverStckSymbol() {
    this.m_isMouseInStckSymbolCell = true;
    this.m_isShowStckChrtTooltip = this.m_isMouseInStckSymbolCell || this.m_isMouseInTooltip;
  }

  onMouseenterStckSymbol(event: any, symbol:string) {
    console.log('onMouseenterStockSymbol', event, symbol);
    this.m_stockSymbol = symbol;
    this.getStckChrtData(symbol);
    const stckChrtTooltipId: HTMLElement = (document.getElementById('stockChrtTooltip') as HTMLElement); // Get the tooltip element from the DOM where the tooltip text will be displayed.
    this.tooltipPositioning(event, stckChrtTooltipId);
  }

  onMouseleaveStckSymbol() {
    this.m_isMouseInStckSymbolCell = false;
    setTimeout(() => { this.m_isShowStckChrtTooltip = this.m_isMouseInStckSymbolCell || this.m_isMouseInTooltip; }, 200); // don't remove tooltip immediately, because onMouseEnterStockTooltip() will only be called later if Tooltip doesn't disappear
  }

  onMouseenterStckChrtTooltip() {
    this.m_isMouseInStckSymbolCell = true;
    this.m_isShowStckChrtTooltip = this.m_isMouseInStckSymbolCell || this.m_isMouseInTooltip;
  }

  onMouseleaveStckChrtTooltip() {
    this.m_isMouseInTooltip = false;
    this.m_isShowStckChrtTooltip = this.m_isMouseInTooltip;
  }

  getStckChrtData(symbol: string) {
    const body: object = { Tickers: symbol };
    const url: string = this.m_controllerBaseUrl + 'GetStckChrtData'; // Server: it needs to be https://sqcore.net/webapps/TechnicalAnalyzer/GetStckChrtData
    this.m_httpClient.post<AssetHistValues>(url, body).subscribe((response) => {
      this.updateStockHistData(response);
      console.log('getStckChrtData: AssetHistValues ', response);
    }, (error) => console.error(error));
  }

  updateStockHistData(assetHistValues: AssetHistValues) {
    if (assetHistValues == null)
      return;
    const stockChartVals: UiChrtval[] = [];
    for (let i = 0; i < assetHistValues.HistDates.length; i++) {
      const stockVal = new UiChrtval();
      const dateStr: string = assetHistValues.HistDates[i];
      stockVal.date = new Date(dateStr.substring(0, 4) + '-' + dateStr.substring(4, 6) + '-' + dateStr.substring(6, 8));
      stockVal.sdaClose = assetHistValues.HistSdaCloses[i];
      stockChartVals.push(stockVal);
    }

    // processing Ui With StockChrt
    d3.selectAll('#stockChrt > *').remove();
    const firstEleOfHistDataArr1: number = 100; // used to convert the data into percentage values
    const lineChrtDiv: HTMLElement = document.getElementById('stockChrt') as HTMLElement;
    const yAxisTickformat: string = '';
    const margin: { top: number; right: number; bottom: number; left: number; } = {top: 10, right: 30, bottom: 30, left: 40 };
    const inputWidth: number = 460 - margin.left - margin.right;
    const inputHeight: number = 200 - margin.top - margin.bottom;
    const stckChrtData: UiChrtval[] = stockChartVals.map((r:{ date: Date; sdaClose: number; }) => ({date: new Date(r.date), sdaClose: (r.sdaClose)}));
    // find data range
    const xMin: number = d3.min(stckChrtData, (r:{ date: any; }) => r.date);
    const xMax: number = d3.max(stckChrtData, (r:{ date: any; }) => r.date);
    const yMinAxis: number = d3.min(stckChrtData, (r:{ sdaClose: any; }) => r.sdaClose);
    const yMaxAxis: number = d3.max(stckChrtData, (r:{ sdaClose: any; }) => r.sdaClose);
    const isNavChrt: boolean = false;
    drawHistChartFromData(stckChrtData, null, lineChrtDiv, inputWidth, inputHeight, margin, xMin, xMax, yMinAxis, yMaxAxis, yAxisTickformat, firstEleOfHistDataArr1, isNavChrt);
  }

  tooltipPositioning(event: MouseEvent, tooltipId: HTMLElement) { // tooltip positioning based on the mouseevent and tooltip element Id.
    const scrollLeft = (window.pageXOffset !== undefined) ? window.pageXOffset : ((document.documentElement || document.body.parentNode || document.body) as HTMLElement).scrollLeft; // Get the horizontal scroll position of the window.
    const scrollTop = (window.pageYOffset !== undefined) ? window.pageYOffset : ((document.documentElement || document.body.parentNode || document.body) as HTMLElement).scrollTop; // Get the vertical scroll position of the window.
    // Set the position of the tooltip element relative to the mouse cursor position.
    // The tooltip will be positioned 10 pixels to the right of the cursor's X position and aligned with the cursor's Y position.
    tooltipId.style.left = 10 + event.pageX - scrollLeft + 'px';
    tooltipId.style.top = event.pageY - scrollTop + 'px';
  }
}