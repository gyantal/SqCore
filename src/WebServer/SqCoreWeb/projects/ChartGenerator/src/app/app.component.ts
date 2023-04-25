import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_http: HttpClient;
  m_portfolioId = -1; // -1 is invalid ID

  constructor(http: HttpClient) {
    this.m_http = http;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/ChartGenerator/?id=1
    const prtfIdStr = url.searchParams.get('id');
    if (prtfIdStr != null)
      this.m_portfolioId = parseInt(prtfIdStr);
  }
}