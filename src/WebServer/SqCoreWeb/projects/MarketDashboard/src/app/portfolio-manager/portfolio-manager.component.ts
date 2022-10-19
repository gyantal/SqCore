import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-portfolio-manager',
  templateUrl: './portfolio-manager.component.html',
  styleUrls: ['./portfolio-manager.component.scss']
})
export class PortfolioManagerComponent implements OnInit {
  @Input() _parentWsConnection?: WebSocket = undefined; // this property will be input from above parent container

  // isShowPortfolioView: boolean = true;
  portfolioSelection: string[] = ['Dr. Gyorgy, Antal', 'Didier Charmat']; // PrtFldrs
  portfolioSelectionSelected: string = 'Dr. Gyorgy, Antal';
  tabPageVisibleIdx = 1;

  dashboardHeaderWidth = 0;
  dashboardHeaderHeight = 0;
  prtfMgrToolWidth = 0;
  prtfMgrToolHeight = 0;
  panelPrtfTreeWidth = 0;
  panelPrtfTreeHeight = 0;
  panelPrtfChrtWidth = 0;
  panelPrtfChrtHeight = 0;
  panelStatsWidth = 0;
  panelStatsHeight = 0;
  panelPrtfSpecWidth = 0;
  panelPrtfSpecHeight = 0;

  constructor() { }

  ngOnInit(): void {
    // Notes : Difference btw scrollHeight, clientHeight and offsetHeight
    // ScrollHeight : Entire content & padding (visible & not)
    // ClientHeight : Visible content & padding
    // OffsetHeight : visible content & padding + border + scrollbar

    const panelPrtfTreeId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfTree');
    this.panelPrtfTreeWidth = panelPrtfTreeId.clientWidth as number;
    this.panelPrtfTreeHeight = panelPrtfTreeId.clientHeight as number;

    // For displaying the width and height - Dynamic values
    window.addEventListener('resize', (resizeBy) => {
      this.panelPrtfTreeWidth = panelPrtfTreeId.clientWidth as number;
      this.panelPrtfTreeHeight = panelPrtfTreeId.clientHeight as number;
      return resizeBy;
    });

    const panelChartId = PortfolioManagerComponent.getNonNullDocElementById('panelChart');
    this.panelPrtfChrtWidth = panelChartId.clientWidth as number;
    this.panelPrtfChrtHeight = panelChartId.clientHeight as number;

    window.addEventListener('resize', (resizeBy) => {
      this.panelPrtfChrtWidth = panelChartId.clientWidth as number;
      this.panelPrtfChrtHeight = panelChartId.clientHeight as number;
      return resizeBy;
    });

    const panelStatsId = PortfolioManagerComponent.getNonNullDocElementById('panelStats');
    this.panelStatsWidth = panelStatsId.clientWidth as number;
    this.panelStatsHeight = panelStatsId.clientHeight as number;

    window.addEventListener('resize', (resizeBy) => {
      this.panelStatsWidth = panelStatsId.clientWidth as number;
      this.panelStatsHeight = panelStatsId.clientHeight as number;
      return resizeBy;
    });

    const panelPrtfSpecId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfSpec');
    this.panelPrtfSpecWidth = panelPrtfSpecId.clientWidth as number;
    this.panelPrtfSpecHeight = panelPrtfSpecId.clientHeight as number;

    window.addEventListener('resize', (resizeBy) => {
      this.panelPrtfSpecWidth = panelPrtfSpecId.clientWidth as number;
      this.panelPrtfSpecHeight = panelPrtfSpecId.clientHeight as number;
      return resizeBy;
    });

    const approotToolbar = PortfolioManagerComponent.getNonNullDocElementById('toolbarId');
    this.dashboardHeaderWidth = approotToolbar.clientWidth;
    this.dashboardHeaderHeight = approotToolbar.clientHeight;

    window.addEventListener('resize', (resizeBy) => {
      this.dashboardHeaderWidth = approotToolbar.clientWidth;
      this.dashboardHeaderHeight = approotToolbar.clientHeight;
      return resizeBy;
    });

    this.prtfMgrToolWidth = window.innerWidth as number;
    this.prtfMgrToolHeight = window.innerHeight as number;

    window.addEventListener('resize', (resizeBy) => {
      this.prtfMgrToolWidth = window.innerWidth as number;
      this.prtfMgrToolHeight = window.innerHeight as number;
      return resizeBy;
    });

    this.prtfMgrToolWidth = this.prtfMgrToolWidth;
    this.prtfMgrToolHeight = this.prtfMgrToolHeight - this.dashboardHeaderHeight;
  }

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'PortfMgr.Portfolios': // The most frequent message should come first. Note: LstVal (realtime price) is handled earlier in a unified way.
        console.log('PortfMgr.Portfolios:' + msgObjStr);
        return true;
      case 'PortfMgr.Handshake': // The least frequent message should come last.
        console.log('PortfMgr.Handshake:' + msgObjStr);
        return true;
      default:
        return false;
    }
  }

  // Under development - Daya
  onClickPortfolio(portfolioSelected: string) {
    this.portfolioSelectionSelected = portfolioSelected;
    const panelPrtfTreeId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfTree') as HTMLElement;
    const portfolioView = document.getElementsByClassName('portfolioNestedView');
    console.log('The length of tree view is :', portfolioView.length);
    console.log('The portfolioSelected is :', portfolioSelected);
    for (const portfolio of portfolioView) {
      if (this.portfolioSelectionSelected == portfolio.previousElementSibling?.innerHTML) {
        // toggling between plus and minus signs for nested view
        if (portfolio.previousElementSibling?.classList.contains('portfolioManager')) {
          portfolio.previousElementSibling?.classList.remove('portfolioManager');
          portfolio.previousElementSibling?.classList.add('portfolioManagerMinus');
        } else {
          portfolio.previousElementSibling?.classList.remove('portfolioManagerMinus');
          portfolio.previousElementSibling?.classList.add('portfolioManager');
        }
        portfolio.classList.toggle('active');
        break;
      }
    }
    this.displayPanelWidthAndHieght(panelPrtfTreeId.id);
  }

  onClickPortfolioPreview(tabIdx: number) {
    this.tabPageVisibleIdx = tabIdx;
  }

  displayPanelWidthAndHieght(id: string) {
    this.panelPrtfTreeWidth = PortfolioManagerComponent.getNonNullDocElementById(id).clientWidth as number;
    this.panelPrtfTreeHeight = PortfolioManagerComponent.getNonNullDocElementById(id).clientHeight as number;

    window.addEventListener('resize', (resizeBy) => {
      this.panelPrtfTreeWidth = PortfolioManagerComponent.getNonNullDocElementById(id).clientWidth as number;
      this.panelPrtfTreeHeight = PortfolioManagerComponent.getNonNullDocElementById(id).clientHeight as number;
      return resizeBy;
    });
  }

  static getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
    return document.getElementById(id) as HTMLElement;
  }
}