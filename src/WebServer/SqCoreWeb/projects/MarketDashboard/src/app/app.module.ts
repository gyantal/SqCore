import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrAccViewerComponent } from './bracc-viewer/bracc-viewer.component';
import { PortfolioManagerComponent } from './portfolio-manager/portfolio-manager.component';
import { MarketHealthComponent } from './market-health/market-health.component';
import { CatalystSnifferComponent } from './catalyst-sniffer/catalyst-sniffer.component';
import { QuickfolioNewsComponent } from './quickfolio-news/quickfolio-news.component';
import { TooltipSandpitComponent } from './tooltip-sandpit/tooltip-sandpit.component';
import { DocsWhatIsNewComponent, DocsGetStartedComponent, DocsTutorialComponent } from './docs/docs.component';
import { ClickOutsideDirective } from './../../../sq-ng-common/src/lib/sq-ng-common.directive.click-outside';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';
import { MatTabsModule } from '@angular/material/tabs'; // used for creating multiple tabs
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SqTreeViewComponent } from './sq-tree-view/sq-tree-view.component';


@NgModule({
  declarations: [
    AppComponent,
    BrAccViewerComponent,
    PortfolioManagerComponent,
    MarketHealthComponent,
    CatalystSnifferComponent,
    QuickfolioNewsComponent,
    TooltipSandpitComponent,
    DocsWhatIsNewComponent,
    DocsGetStartedComponent,
    DocsTutorialComponent,
    ClickOutsideDirective,
    SettingsDialogComponent,
    SqTreeViewComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    FormsModule,
    MatTabsModule,
    BrowserAnimationsModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }