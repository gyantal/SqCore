import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;
  m_activeTab: string = 'LlmChat';

  constructor(http: HttpClient) {
    this.m_httpClient = http;

    // Angular ctor @Inject('BASE_URL') contains the full path: 'https://sqcore.net/webapps/LlmAssistant', but we have to call our API as 'https://sqcore.net/LlmAssistant/MyApiFunction', so we need the URL without the '/webapps/TechnicalAnalyzer' Path.
    // And anyway, better to go non-Angular for less complexity. And 'window.location' is the fastest, native JS option for getting the URL.
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/'; // window.location.origin (URL without the path) = Local: "https://127.0.0.1:4207", Server: https://sqcore.net"
    console.log('window.location.origin', window.location.origin);
  }

  ngOnInit(): void {}

  onClickActiveTab(activeTab: string) {
    this.m_activeTab = activeTab;
  }
}