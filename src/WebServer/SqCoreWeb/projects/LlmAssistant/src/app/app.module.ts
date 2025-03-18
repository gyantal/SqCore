import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';

import { AppComponent } from './app.component';
import { LlmScanComponent } from './llm-scan/llm-scan.component';
import { LlmChatComponent } from './llm-chat/llm-chat.component';

@NgModule({
  declarations: [
    AppComponent,
    LlmScanComponent,
    LlmChatComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }