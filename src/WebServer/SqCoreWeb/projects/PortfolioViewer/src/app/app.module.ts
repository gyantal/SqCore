import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { provideHttpClient, withInterceptorsFromDi, withXhr } from '@angular/common/http';
import { NanToDashPipe, TypeOfPipe, NumberToTBMKPipe, NanToDashPctPipe } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_str';

import { AppComponent } from './app.component';

@NgModule({
  declarations: [AppComponent, NanToDashPipe, NanToDashPctPipe,TypeOfPipe,NumberToTBMKPipe],
  bootstrap: [AppComponent],
  imports: [BrowserModule, FormsModule],
  providers: [provideHttpClient(withXhr(), withInterceptorsFromDi())] })
export class AppModule { }