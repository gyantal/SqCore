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
  isMouseEnterReziableDiv: boolean = false;
  isMouseOverPrtfTree: boolean = false;
  isShowResizableDiv: boolean = false;

  constructor() { }

  ngOnInit(): void {
    // Notes : Difference btw scrollHeight, clientHeight and offsetHeight
    // ScrollHeight : Entire content & padding (visible & not)
    // ClientHeight : Visible content & padding
    // OffsetHeight : visible content & padding + border + scrollbar

    const panelPrtfTreeId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfTree');
    this.panelPrtfTreeWidth = panelPrtfTreeId.clientWidth as number;
    this.panelPrtfTreeHeight = panelPrtfTreeId.clientHeight as number;

    const panelChartId = PortfolioManagerComponent.getNonNullDocElementById('panelChart');
    this.panelPrtfChrtWidth = panelChartId.clientWidth as number;
    this.panelPrtfChrtHeight = panelChartId.clientHeight as number;

    const panelStatsId = PortfolioManagerComponent.getNonNullDocElementById('panelStats');
    this.panelStatsWidth = panelStatsId.clientWidth as number;
    this.panelStatsHeight = panelStatsId.clientHeight as number;

    const panelPrtfSpecId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfSpec');
    this.panelPrtfSpecWidth = panelPrtfSpecId.clientWidth as number;
    this.panelPrtfSpecHeight = panelPrtfSpecId.clientHeight as number;

    const approotToolbar = PortfolioManagerComponent.getNonNullDocElementById('toolbarId');
    this.dashboardHeaderWidth = approotToolbar.clientWidth;
    this.dashboardHeaderHeight = approotToolbar.clientHeight;

    this.prtfMgrToolWidth = window.innerWidth as number;
    this.prtfMgrToolHeight = window.innerHeight as number;

    // For displaying the width and height - Dynamic values
    window.addEventListener('resize', (resizeBy) => {
      this.panelPrtfTreeWidth = panelPrtfTreeId.clientWidth as number;
      this.panelPrtfTreeHeight = panelPrtfTreeId.clientHeight as number;
      this.panelPrtfChrtWidth = panelChartId.clientWidth as number;
      this.panelPrtfChrtHeight = panelChartId.clientHeight as number;
      this.panelStatsWidth = panelStatsId.clientWidth as number;
      this.panelStatsHeight = panelStatsId.clientHeight as number;
      this.panelPrtfSpecWidth = panelPrtfSpecId.clientWidth as number;
      this.panelPrtfSpecHeight = panelPrtfSpecId.clientHeight as number;
      this.dashboardHeaderWidth = approotToolbar.clientWidth;
      this.dashboardHeaderHeight = approotToolbar.clientHeight;
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

  // onClickPrtfTree(id: string) {
  //   this.makeResizablePrtfTree(id);
  // }

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

  onMouseEnterPrtfTree(id: string) {
    // This is used to make the hidden div visible
    const reziableHiddenDiv: any = document.getElementById(id);
    if (getComputedStyle(reziableHiddenDiv, null).visibility === 'hidden')
      reziableHiddenDiv.style.visibility = 'visible';
  }

  onMouseOutPrtfTree(id: string) {
    const reziableHiddenDiv: any = document.getElementById(id);
    reziableHiddenDiv.style.visibility = 'hidden';
  }

  onMouseOver(selectedPanel: string) {
    this.isShowResizableDiv = true;
    this.resizeSelectedPanel(selectedPanel);
  }

  resizeSelectedPanel(selectedPanel: string) {
    const element = PortfolioManagerComponent.getNonNullDocElementById(selectedPanel);
    const resizableWidthHeight = window.document.getElementById('demo') as HTMLElement;
    const minSize = 20;
    let originalWidth = 0;
    let originalHeight = 0;
    // let originalX = 0;
    // let originalY = 0;
    let originalMouseX = 0;
    let originalMouseY = 0;
    element.addEventListener('mousedown', function(event: any) {
      event.preventDefault();
      originalWidth = parseFloat(getComputedStyle(element, null).getPropertyValue('width').replace('px', ''));
      originalHeight = parseFloat(getComputedStyle(element, null).getPropertyValue('height').replace('px', ''));
      // originalX = element.getBoundingClientRect().left;
      // originalY = element.getBoundingClientRect().top;
      originalMouseX = event.pageX;
      originalMouseY = event.pageY;
      window.addEventListener('mousemove', resizeDiv);
      window.addEventListener('mouseup', stopResize);
    });

    function resizeDiv(event: any) {
      const width = originalWidth + (event.pageX - originalMouseX);
      const height = originalHeight + (event.pageY - originalMouseY);
      resizableWidthHeight.innerText = 'Browser inner window width : ' + width + ', height : ' + height;
      if (width > minSize)
        element.style.width = width + 'px';
      if (height > minSize)
        element.style.height = height + 'px';
    }

    function stopResize() {
      window.removeEventListener('mousemove', resizeDiv);
    }
  }

  // Under Development - Daya
  // onMouseEnterReziableDiv() {
  //   this.isMouseEnterReziableDiv = true;
  //   this.isShowResizableDiv = this.isMouseEnterReziableDiv || this.isMouseOverPrtfTree;
  // }

  // onMouseLeaveReziableDiv() {
  //   this.isMouseEnterReziableDiv = false;
  //   this.isMouseOverPrtfTree = false;
  //   this.isShowResizableDiv = this.isMouseEnterReziableDiv || this.isMouseOverPrtfTree;
  // }
}