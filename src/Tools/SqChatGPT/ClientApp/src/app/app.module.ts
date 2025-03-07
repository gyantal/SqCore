import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import { CounterComponent } from './counter/counter.component';
import { FetchDataComponent } from './fetch-data/fetch-data.component';
import { ChatGptComponent } from './gpt-chat/gpt-chat.component';
import { GptScanComponent } from './gpt-scan/gpt-scan.component';

export function getBaseUrl() {
  return document.getElementsByTagName('base')[0].href;
}

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    CounterComponent,
    FetchDataComponent,
    ChatGptComponent,
    GptScanComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([
      { path: '', component: HomeComponent, pathMatch: 'full' },
      { path: 'counter', component: CounterComponent },
      { path: 'fetch-data', component: FetchDataComponent },
      { path: 'gpt-chat', component: ChatGptComponent },
      { path: 'gpt-scan', component: GptScanComponent }
    ])
  ],
  // Issue: Tool stopped functioning with error "NullInjectorError: No provider for BASE_URL!". see https://stackoverflow.com/questions/58016365/nullinjection-error-in-appmodule-staticinjectorerrorappmodulebase-url
  // FetchData and ChatGpt components are using BaseUrl without the provider. In Angular, when a dependency (like BASE_URL) is injected into a component or service, it must be registered as a provider. Othersiwe it doesnt know how to resolve the dependency and resulting in a NullInjectorError.
  providers: [{ provide: 'BASE_URL', useFactory: getBaseUrl }],
  bootstrap: [AppComponent]
})
export class AppModule { }
