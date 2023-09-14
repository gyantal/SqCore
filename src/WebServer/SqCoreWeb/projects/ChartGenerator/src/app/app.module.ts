import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms'; // ngModel model is defined in angular/forms
import { AppComponent } from './app.component';
import { SqTreeViewComponent } from '../../../sq-ng-common/src/lib/sq-tree-view/sq-tree-view.component';

@NgModule({
  declarations: [
    AppComponent,
    SqTreeViewComponent
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