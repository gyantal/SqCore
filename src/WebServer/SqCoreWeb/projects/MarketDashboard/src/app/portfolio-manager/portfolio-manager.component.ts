import { Component, OnInit, Input, ViewChild } from '@angular/core';
import { SqTreeViewComponent } from '../sq-tree-view/sq-tree-view.component';

type Nullable<T> = T | null;

// Input data classes
// class PrtfMgrVwrHandShk {
//   portfolioFldrJs: Nullable<PortfolioFldrJs[]> = null;
// }

class PortfolioFldrJs {
  public id = -1;
  public name = '';
  public parentFolderId = -1;
}

// class PortfolioJs {
//   public sharedAccess = '';
//   public sharedUserWithMe = '';
//   public baseCurrency = '';
// }

// class TreeViewItemSelectionHolder {
//   public selected : PortfolioFldrJs;
//  public selectedId : number;
// }

@Component({
  selector: 'app-portfolio-manager',
  templateUrl: './portfolio-manager.component.html',
  styleUrls: ['./portfolio-manager.component.scss']
})
export class PortfolioManagerComponent implements OnInit {
  @Input() _parentWsConnection?: WebSocket = undefined; // this property will be input from above parent container
  @ViewChild(SqTreeViewComponent) public sqTreeComponent!: SqTreeViewComponent; // allows accessing the data from child to parent

  // handshakeObj: Nullable<PrtfMgrVwrHandShk> = null;
  portfoliosFldrsObj: Nullable<PortfolioFldrJs> = null;
  portfolioFolders: PortfolioFldrJs[] = [];
  // portfoliosObj: Nullable<PortfolioJs> = null;
  // portfolios: PortfolioJs[] = [];
  uiPortfolioFoldersNested: any[] = [];
  treeviewPortItemOpenPathIds: number[] = []; // [-31, 1, 8]
  isPortfolioDialogVisible: boolean = false;
  isFldrHasChildren: boolean = false;
  isFldrHasChildrenDialogVisible: boolean = false;
  // virtualUsrId: number = -1;
  pfName: string = ''; // common for both portfolio and portfolioFolder
  // parentFldrId: number = -1;

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
        // this.processPortfolios(msgObjStr);
        return true;
      case 'PortfMgr.PortfoliosFldrs': // The most frequent message should come first. Note: LstVal (realtime price) is handled earlier in a unified way.
        console.log('PortfMgr.PortfoliosFldrs:' + msgObjStr);
        this.processPortfolioFldrsTree(msgObjStr);
        return true;
      case 'PortfMgr.Handshake': // The least frequent message should come last.
        console.log('PortfMgr.Handshake:' + msgObjStr);
        // this.handshakeObj = JSON.parse(msgObjStr);
        return true;
      case 'PortfMgr.PortfoliosFldrsChldrn': // Folders has children
        console.log('PortfMgr.PortfoliosFldrsChldrn:' + msgObjStr);
        this.isFldrHasChildren = String(JSON.parse(msgObjStr)).toLowerCase() === 'true';
        if (this.isFldrHasChildren) {
          this.isFldrHasChildrenDialogVisible = true;
          const dialogAnimate = document.getElementById('hasChildrenDialog') as HTMLElement;
          dialogAnimate.style.animationName = 'dialogFadein';
          dialogAnimate.style.animationDuration = '3s';
          dialogAnimate.style.animationTimingFunction = 'linear'; // default would be ‘ease’, which is a slow start, then fast, before it ends slowly. We prefer the linear.
          // dialogAnimate.style.animationDelay = '0s';
          dialogAnimate.style.animationIterationCount = '1'; // only once
          dialogAnimate.style.animationFillMode = 'forwards';
        }
        return true;
      default:
        return false;
    }
  }

  // processPortfolios(msgObjStr: string) {
  //   this.portfoliosObj = JSON.parse(msgObjStr, function(this: any, key, value) {
  //     // eslint-disable-next-line no-invalid-this
  //     const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

  //     if (key === 'sAcs') {
  //       _this.sharedAccess = value;
  //       return; // if return undefined, orignal property will be removed
  //     }
  //     if (key === 'sUsr') {
  //       _this.sharedUserWithMe = value;
  //       return; // if return undefined, orignal property will be removed
  //     }
  //     if (key === 'bCur') {
  //       _this.baseCurrency = value;
  //       return; // if return undefined, orignal property will be removed
  //     }
  //     return value;
  //   });
  //   this.updateUiPortfolios(this.portfoliosObj, this.portfolios);
  // }

  processPortfolioFldrsTree(msgObjStr: string) {
    this.portfoliosFldrsObj = JSON.parse(msgObjStr, function(this: any, key, value) {
      // property names and values are transformed to a shorter ones for decreasing internet traffic.Transform them back to normal for better code reading.

      // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      if (key === 'n') {
        _this.name = value;
        return; // if return undefined, orignal property will be removed
      }
      if (key === 'p') {
        _this.parentFolderId = value;
        return; // if return undefined, orignal property will be removed
      }
      return value;
    });
    this.updateUiPortfolioFolders(this.portfoliosFldrsObj, this.portfolioFolders);
  };

  onPortfoliosRefreshClicked() {
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.RefreshPortfolioFldrs:');
  }

  onClickPortfolioPreview(tabIdx: number) {
    this.tabPageVisibleIdx = tabIdx;
  }

  static getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
    return document.getElementById(id) as HTMLElement;
  }

  onMouseOver(resizer: string) {
    if (resizer == 'resizer')
      this.makeResizablePrtfTree(resizer);
    if (resizer == 'resizer2')
      this.makeResizablePrtfDetails(resizer);
  }

  makeResizablePrtfTree(resizer: string) {
    const panelPrtfTreeId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfTree');
    const panelPrtfDetailsId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfDetails');
    const resizerDiv = PortfolioManagerComponent.getNonNullDocElementById(resizer);

    resizerDiv.addEventListener('mousedown', resizingDiv);
    function resizingDiv(event: any) {
      window.addEventListener('mousemove', mousemove);
      window.addEventListener('mouseup', stopResize);
      const originalMouseX = event.pageX;
      const panelPrtfTree = panelPrtfTreeId.getBoundingClientRect();

      function mousemove(event: any) {
        const width = window.innerWidth || document.documentElement.clientWidth || document.documentElement.getElementsByTagName('body')[0].clientWidth; // required for pixels to viewport width conversion.
        const calculatedWidth = 100 * (panelPrtfTree.width - (originalMouseX - event.pageX)) / width;
        panelPrtfTreeId.style.width = calculatedWidth + 'vw';
        panelPrtfDetailsId.style.width = (100 - calculatedWidth) + 'vw'; // 100vw is the whole window width as we know the prtfTree width, based on that we are calculating the prtfDetails width in vw
      }

      function stopResize() {
        window.removeEventListener('mousemove', mousemove);
      }
    }
  }

  makeResizablePrtfDetails(resizer2: string) {
    const panelChartId = PortfolioManagerComponent.getNonNullDocElementById('panelChart');
    const panelStatsAndPerfSpecId = PortfolioManagerComponent.getNonNullDocElementById('panelStatsAndPerfSpec');
    const panelStatsId = PortfolioManagerComponent.getNonNullDocElementById('panelStats');
    const panelPrtfSpecId = PortfolioManagerComponent.getNonNullDocElementById('panelPrtfSpec');

    const resizerDiv = PortfolioManagerComponent.getNonNullDocElementById(resizer2);

    resizerDiv.addEventListener('mousedown', resizingDiv);
    function resizingDiv(event: any) {
      window.addEventListener('mousemove', mousemove);
      window.addEventListener('mouseup', stopResize);
      const originalMouseY = event.pageY;
      const panelChart = panelChartId.getBoundingClientRect();

      function mousemove(event: any) {
        const height = window.innerHeight || document.documentElement.clientHeight || document.documentElement.getElementsByTagName('body')[0].clientHeight; // required for pixels to viewport height conversion.
        const calculatedHeight = 100 * (panelChart.height - (originalMouseY - event.pageY)) / height;
        panelChartId.style.height = calculatedHeight + 'vh';
        panelStatsAndPerfSpecId.style.height = (95.5 - calculatedHeight) + 'vh'; // 95.5vh is the total veiwport heigh of pancelchart and panelStatsAndPerfSpecId
        panelStatsId.style.height = (95.5 - calculatedHeight) + 'vh';
        panelPrtfSpecId.style.height = (95.5 - calculatedHeight) + 'vh';
      }

      function stopResize() {
        window.removeEventListener('mousemove', mousemove);
      }
    }
  }
  // under Development -- Daya
  // updateUiPortfolios(portfoliosObj: Nullable<PortfolioJs>, portfolios: PortfolioJs[]) {
  //   if (!(Array.isArray(portfoliosObj) && portfoliosObj.length > 0 ))
  //     return;
  //   for (const prtf of portfoliosObj) {
  //     const pf = new PortfolioJs();
  //     const prtfItem = new PortfolioFldrJs();
  //     prtfItem.id = prtf.id;
  //     prtfItem.name = prtf.name;
  //     pf.portf.push(prtfItem);
  //     pf.baseCurrency = prtf.baseCurrency,
  //     pf.sharedAccess = prtf.sharedAccess,
  //     pf.sharedUserWithMe = prtf.sharedUserWithMe,
  //     portfolios.push(pf);
  //   }
  // }

  updateUiPortfolioFolders(portfoliosFldrsObj: Nullable<PortfolioFldrJs>, portfolioFolders: PortfolioFldrJs[]) {
    if (!(Array.isArray(portfoliosFldrsObj) && portfoliosFldrsObj.length > 0 ))
      return;
    for (const prtfFldr of portfoliosFldrsObj) {
      const prFld = new PortfolioFldrJs();
      prFld.id = prtfFldr.id;
      prFld.name = prtfFldr.name;
      prFld.parentFolderId = prtfFldr.parentFolderId;
      portfolioFolders.push(prFld);
    }
    this.createTreeViewData(portfolioFolders);
  }

  createTreeViewData(portfolioFolders: PortfolioFldrJs[]) {
    this.uiPortfolioFoldersNested.length = 0;
    const treeData = {};
    let parent: any;
    let child: any;

    for (let i = 0; i < portfolioFolders.length; i++) {
      parent = portfolioFolders[i];
      treeData[parent.id] = parent;
      treeData[parent.id]['children'] = [];
    }

    for (const id in treeData) {
      if (treeData.hasOwnProperty(id)) {
        child = treeData[id];
        if (child.parentFolderId && treeData[child['parentFolderId']])
          treeData[child['parentFolderId']]['children'].push(child);
        else
          this.uiPortfolioFoldersNested.push(child);
      }
    }
  };

  onCreateClicked() {
    this.isPortfolioDialogVisible = true;
  }

  onCloseClicked() {
    this.isPortfolioDialogVisible = false;
  }

  onCreatePortfolioClicked(pfName: string) {
    const lastSelectedTreeNode = SqTreeViewComponent.gLastSelectedItem as PortfolioFldrJs;
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.CreatePortfFldr:' + this.pfName + ',prntFId:' + lastSelectedTreeNode.id);
    this.isPortfolioDialogVisible = false;
  }

  onDeletePortfolioClicked() {
    const lastSelectedTreeNode = SqTreeViewComponent.gLastSelectedItem as PortfolioFldrJs;
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.DeletePortfFldr:' + lastSelectedTreeNode.name + ',prntFId:' + lastSelectedTreeNode.id + ',chldrn:' + this.sqTreeComponent.isFolderHasChildren);
  }

  onFldrHasChildrenContinueClicked() {
    this.isFldrHasChildrenDialogVisible = false;
  }

  onFldrHasChildrenCloseClicked() {
    window.close();
  }
}