import { Component } from '@angular/core';

class Asset {
  // public sqTicker = ''; // “S/TSLA” // sqTicker identifies the asset uniquely in C#, but we don’t need that at the moment. Here we use only the Symbol.
  public symbol = ''; // “TSLA”
  public pctChnValue1: number = 0; // 0% or 100%
  public pctChnValue2: number = 0;
  public pctChnValue3: number = 0;
  public pctChnValue4: number = 0;
  public pctChnValueAggregate: number = 0; // 0% or 25% 50% 75% 100%
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_tickersStr: string | null = null;
  m_tickers: string[] | undefined = [];
  m_assets: Asset[] = [{symbol: 'TSLA', pctChnValue1: 0, pctChnValue2: 0.25, pctChnValue3: 0.55, pctChnValue4: 0.11, pctChnValueAggregate: 0.91 },
    {symbol: 'MSFT', pctChnValue1: 0.1, pctChnValue2: 0.22, pctChnValue3: 0.45, pctChnValue4: 0.65, pctChnValueAggregate: 1.42 },
    {symbol: 'TLT', pctChnValue1: 0, pctChnValue2: 0.25, pctChnValue3: 0.55, pctChnValue4: 0.11, pctChnValueAggregate: 0.91 },
    {symbol: 'SPY', pctChnValue1: 0, pctChnValue2: 0.25, pctChnValue3: 0.55, pctChnValue4: 0.1, pctChnValueAggregate: 0.81 },]; // dummy data

  constructor() {
    // const wsQueryStr = window.location.search;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=TSLA,MSFT
    this.m_tickersStr = url.searchParams.get('tickers');
    this.m_tickers = this.m_tickersStr?.split(',');
    console.log(this.m_tickersStr);
  }

  ngOnInit(): void {
    console.log('ngOnInit()');
  }

  onInputTickers(event: Event) {
    const tickersStr = (event.target as HTMLInputElement).value.trim().toUpperCase();
    this.m_tickers = tickersStr.split(',');
  }
}