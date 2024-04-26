import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { NanToDashPipe, TypeOfPipe, NumberToTBMKPipe } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_str';

import { AppComponent } from './app.component';

@NgModule({
  declarations: [
    AppComponent,
    NanToDashPipe,
    TypeOfPipe,
    NumberToTBMKPipe
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