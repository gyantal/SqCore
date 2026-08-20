import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

import { AppComponent } from './app.component';
import { LlmScanComponent } from './llm-scan/llm-scan.component';
import { LlmChatComponent } from './llm-chat/llm-chat.component';
import { LlmBasicChatComponent } from './llm-basic-chat/llm-basic-chat.component';
import { LlmPromptComponent } from './llm-prompt/llm-prompt.component';

@NgModule({
  declarations: [AppComponent,LlmScanComponent,LlmChatComponent,LlmBasicChatComponent,LlmPromptComponent],
  bootstrap: [AppComponent],
  imports: [BrowserModule,FormsModule],
  providers: [provideHttpClient(withInterceptorsFromDi())] })
export class AppModule { }