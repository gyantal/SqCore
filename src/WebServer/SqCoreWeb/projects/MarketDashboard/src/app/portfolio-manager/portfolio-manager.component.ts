import { Component, OnInit, AfterViewInit, Input, ViewChild } from '@angular/core';
import { SqTreeViewComponent } from '../sq-tree-view/sq-tree-view.component';

type Nullable<T> = T | null;

// Input data classes

class FolderJs {
  public id = -1;
  public name = '';
  public parentFolderId = -1;
  public creationTime = '';
  public note = '';
  public ownerUserName = '';
}

class PortfolioJs {
  public sharedAccess = '';
  public sharedUserWithMe = '';
  public baseCurrency = '';
  public id = -1;
}

export class TreeViewItem { // future work. At the moment, it copies PortfolioFldrJs[] and add the children field. With unnecessary field values. When Portfolios are introduced, this should be rethought.
  public id = -1;
  public name = '';
  public parentFolderId = -1;

  public creationTime = ''; // Folder only. not necessary
  public note = ''; // Folder only. not necessary

  public children : TreeViewItem[] = []; // children are other TreeViewItems
  public isSelected = false;
  public isExpanded = false;
  public portfolio : any;
}

export class TreeViewState {
  public lastSelectedItem : Nullable<TreeViewItem> = null;
  public expandedPrtfFolderIds: number[] = [];
  public rootSqTreeViewComponent: Nullable<SqTreeViewComponent> = null;
}

@Component({
  selector: 'app-portfolio-manager',
  templateUrl: './portfolio-manager.component.html',
  styleUrls: ['./portfolio-manager.component.scss']
})
export class PortfolioManagerComponent implements OnInit, AfterViewInit {
  @Input() _parentWsConnection?: WebSocket = undefined; // this property will be input from above parent container
  @ViewChild(SqTreeViewComponent) public sqTreeComponent!: SqTreeViewComponent; // allows accessing the data from child to parent

  portfMgrFldrs: Nullable<FolderJs[]> = null;
  portfolios: Nullable<PortfolioJs[]> = null;
  uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  isCreatePortfolioPopupVisible: boolean = false;
  isDeleteConfirmPopupVisible: boolean = false;
  isErrorPopupVisible: boolean = false;
  errorMsgToUser: string = '';
  // common for both portfolio and portfolioFolder
  deletePrtfItemName: string = ''; // portfolio or folder name to be deleted
  createPrtfItemName: string = ''; // portfolio or folder name to be created
  treeViewState: TreeViewState = new TreeViewState();

  tabPrtfSpecVisibleIdx = 1; // tab buttons for portfolio specification preview of positions and strategy parameters

  // the below vaiables are required for resizing the panels according to users
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

    const approotToolbar = PortfolioManagerComponent.getNonNullDocElementById('toolbarId'); // toolbarId is coming from app component
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

  public ngAfterViewInit(): void { // @ViewChild variables are undefined in ngOnInit(). Only ready in ngAfterViewInit
    this.treeViewState.rootSqTreeViewComponent = this.sqTreeComponent;
  }

  static getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
    return document.getElementById(id) as HTMLElement;
  }

  onMouseOverResizer(resizer: string) {
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

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'PortfMgr.Portfolios': // The most frequent message should come first. Note: LstVal (realtime price) is handled earlier in a unified way.
        console.log('PortfMgr.Portfolios:' + msgObjStr);
        this.processPortfolios(msgObjStr);
        return true;
      case 'PortfMgr.Folders': // The most frequent message should come first. Note: LstVal (realtime price) is handled earlier in a unified way.
        console.log('PortfMgr.Folders:' + msgObjStr);
        this.processPortfMgrFldrs(msgObjStr);
        return true;
      case 'PortfMgr.Handshake': // The least frequent message should come last.
        console.log('PortfMgr.Handshake:' + msgObjStr);
        // this.handshakeObj = JSON.parse(msgObjStr);
        return true;
      case 'PortfMgr.ErrorToUser': // Folders has children
        console.log('PortfMgr.ErrorToUser:' + msgObjStr);
        this.errorMsgToUser = msgObjStr;
        this.isErrorPopupVisible = true;
        return true;
      default:
        return false;
    }
  }

  processPortfolios(msgObjStr: string) {
    this.portfolios = JSON.parse(msgObjStr, function(this: any, key, value) {
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      if (key === 'sAcs') {
        _this.sharedAccess = value;
        return; // if return undefined, orignal property will be removed
      }
      if (key === 'sUsr') {
        _this.sharedUserWithMe = value;
        return; // if return undefined, orignal property will be removed
      }
      if (key === 'bCur') {
        _this.baseCurrency = value;
        return; // if return undefined, orignal property will be removed
      }
      return value;
    });
  }

  processPortfMgrFldrs(msgObjStr: string) {
    this.portfMgrFldrs = JSON.parse(msgObjStr, function(this: any, key, value) {
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
      if (key === 'cTime') {
        _this.creationTime = value;
        return; // if return undefined, orignal property will be removed
      }
      // if (key === 'oUsr') {
      //   _this.ownerUserName = value;
      //   return; // if return undefined, orignal property will be removed
      // }
      return value;
    });
    this.createTreeViewData(this.portfMgrFldrs, this.portfolios); // send both portfolio Manager folders and portfolios
  };

  createTreeViewData(pFolders: Nullable<FolderJs[]>, pPrtfs: Nullable<PortfolioJs[]>) {
    if (!(Array.isArray(pFolders) && pFolders.length > 0 ) || !(Array.isArray(pPrtfs) && pPrtfs.length > 0 ))
      return;

    this.uiNestedPrtfTreeViewItems.length = 0;
    const tempPrtfItemsObject = {}; // stores the portfolio items temporarly
    let fldrItem: FolderJs;
    let child: TreeViewItem;

    for (let i = 0; i < pFolders.length; i++) {
      fldrItem = pFolders[i];
      tempPrtfItemsObject[fldrItem.id] = fldrItem;
      tempPrtfItemsObject[fldrItem.id]['children'] = [];
    }
    for (const id in tempPrtfItemsObject) {
      if (!tempPrtfItemsObject.hasOwnProperty(id))
        continue;

      child = tempPrtfItemsObject[id];
      child.isSelected = false;
      // the below gets executed, when the user clicks force reload/refresh or if there is an update in the data
      for (let i = 0; i < this.treeViewState.expandedPrtfFolderIds.length; i++) {
        if (this.treeViewState.expandedPrtfFolderIds[i] == child.id) {
          child.isExpanded = true;
          break;
        }
      }
      // processing the portfolios - Just for debugging purposes to see whether the data is processed or not? - Daya
      for (let j = 0; j < pPrtfs.length; j++) {
        if (pPrtfs[j].id == child.id) {
          child.portfolio = pPrtfs[j];
          break;
        }
      }
      const childTreeViewItem: TreeViewItem = tempPrtfItemsObject[child['parentFolderId']]; // assigning treeview item
      if (child.parentFolderId != undefined && childTreeViewItem != undefined)
        childTreeViewItem['children'].push(child);
      else
        this.uiNestedPrtfTreeViewItems.push(child);
    }

    // Yet to process the portfolios - Daya
    console.log('portfolio items are:', pPrtfs.length); // For debugging purpose
  };

  onPortfoliosRefreshClicked() {
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.RefreshFolders:');
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

  onCreatePrtfItemClicked() { // this logic makes the Popup visible of creating a portfolio
    this.isCreatePortfolioPopupVisible = true;
  }

  onClosePortfolioClicked() { // this logic makes the create portfolio Popup invisible
    this.isCreatePortfolioPopupVisible = false;
  }

  onCreatePortfolioClicked(pfName: string) { // this logic create's a portfolio item if everything is passed
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Create, because no folder was selected.');
      return;
    }
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.CreateFolder:' + this.createPrtfItemName + ',prntFId:' + lastSelectedTreeNode.id);
    this.isCreatePortfolioPopupVisible = false;
  }

  onDeletePrtfItemClicked() { // this logic makes the Delete Confirm Popup visible and displays the selected prtf name
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Delete, because no folder was selected.');
      return;
    }
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    this.isDeleteConfirmPopupVisible = true;
    this.deletePrtfItemName = lastSelectedTreeNode.name;
  }

  onErrorOkClicked() { // this is to close the ErrorPopup when there is a error message from server
    this.isErrorPopupVisible = false;
  }

  onConfirmDeleteYesClicked() { // this logic delete's a portfolio item if everything is passed
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Delete, because no folder was selected.');
      return;
    }
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.DeleteFolder:' + 'fldId:' + lastSelectedTreeNode.id);
    this.isDeleteConfirmPopupVisible = false;
  }

  onConfirmDeleteNoClicked() { // this logic silently closes the deleteConfirm popup
    this.isDeleteConfirmPopupVisible = false;
  }

  onClickPrtfSpecPreview(tabIdx: number) {
    this.tabPrtfSpecVisibleIdx = tabIdx;
  }
}