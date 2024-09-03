import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms'; // ngModel model is defined in angular/forms
import { AppComponent } from './app.component';
import { SqTreeViewComponent } from '../../../sq-ng-common/src/lib/sq-tree-view/sq-tree-view.component';
import { NanToDashPctPipe } from '../../../sq-ng-common/src/lib/sq-ng-common.utils_str';

@NgModule({
  declarations: [
    AppComponent,
    SqTreeViewComponent,
    NanToDashPctPipe,
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }