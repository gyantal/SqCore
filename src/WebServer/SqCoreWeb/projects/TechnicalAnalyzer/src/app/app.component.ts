import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  public m_tickersStr: string | null = null;
  constructor() {
    // const wsQueryStr = window.location.search;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=TSLA,MSFT
    this.m_tickersStr = url.searchParams.get('tickers');
    console.log(this.m_tickersStr);
  }

  ngOnInit(): void {
    console.log('ngOnInit()');
  }
}