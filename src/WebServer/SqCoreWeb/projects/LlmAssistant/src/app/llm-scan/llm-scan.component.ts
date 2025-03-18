import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-llm-scan',
  templateUrl: './llm-scan.component.html',
  styleUrls: ['./llm-scan.component.scss']
})

export class LlmScanComponent implements OnInit {
  m_httpClient: HttpClient;
  m_controllerBaseUrl: string;

  constructor(http: HttpClient) {
    this.m_httpClient = http;
    this.m_controllerBaseUrl = window.location.origin + '/LlmAssistant/';
    console.log('window.location.origin', window.location.origin);
  }

  ngOnInit(): void {}
}