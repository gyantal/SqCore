import { Component, OnInit, AfterViewInit, Input, ViewChild } from '@angular/core';
import { SqTreeViewComponent } from '../sq-tree-view/sq-tree-view.component';
import { processUiWithPrtfRunResultChrt } from '../../../../sq-ng-common/src/lib/chart/advanced-chart';
import { PrtfRunResultJs, UiChartPointValues, UiPrtfPositions, UiPrtfRunResult } from '../../sq-globals';
import { SqNgCommonUtils } from '../../../../sq-ng-common/src/lib/sq-ng-common.utils';
import * as d3 from 'd3';

type Nullable<T> = T | null;

// Input data classes
enum PrtfItemType { // for differenting the folder and portfolio
  Folder = 'Folder',
  Portfolio = 'Portfolio'
 }

class PortfolioItemJs {
  public id = -1;
  public name = '';
  public parentFolderId = -1;
  public creationTime = '';
  public note = '';
  public ownerUserName = '';
  public prtfItemType: PrtfItemType = PrtfItemType.Folder; // need a default for compilation
}

class FolderJs extends PortfolioItemJs {
}

class PortfolioJs extends PortfolioItemJs {
  public sharedAccess = 'Restricted'; // default access type
  public sharedUserWithMe = '';
  public baseCurrency = 'USD'; // default currrency
  public portfolioType = 'Trades'; // default type
}

export class TreeViewItem { // future work. At the moment, it copies PortfolioFldrJs[] and add the children field. With unnecessary field values. When Portfolios are introduced, this should be rethought.
  public id = -1;
  public name = '';
  public parentFolderId = -1;

  public creationTime = ''; // Folder only. not necessary
  public note = ''; // Folder only. not necessary

  public children: TreeViewItem[] = []; // children are other TreeViewItems
  public isSelected: boolean = false;
  public isExpanded: boolean = false;
  public prtfItemType: PrtfItemType = PrtfItemType.Folder;
  public baseCurrency = '';
  public type = ''; // Trades or Simulation
  public sharedAccess = '';
  public sharedUserWithMe = '';
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

  folders: Nullable<FolderJs[]> = null;
  portfolios: Nullable<PortfolioJs[]> = null;
  uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  isCreateOrEditPortfolioPopupVisible: boolean = false;
  isCreateOrEditFolderPopupVisible: boolean = false;
  isDeleteConfirmPopupVisible: boolean = false;
  isErrorPopupVisible: boolean = false;
  errorMsgToUser: string = '';
  // common for both portfolio and portfolioFolder
  deletePrtfItemName: string = ''; // portfolio or folder name to be deleted
  treeViewState: TreeViewState = new TreeViewState();
  editedFolder: FolderJs = new FolderJs(); // create or edit folder
  parentfolderName: string | undefined = ''; // displaying next to the selected parent folder id on Ui
  editedPortfolio: PortfolioJs = new PortfolioJs(); // create or edit portfolio
  currencyType: string[] = ['USD', 'EUR', 'GBP', 'GBX', 'HUF', 'JPY', 'CAD', 'CNY', 'CHF'];
  portfolioType: string[] = ['Trades', 'Simulation', 'TradesSqClassic'];
  sharedAccess: string[] = ['Restricted', 'OwnerOnly', 'Anyone'];
  // sharedUsers: number[] = [31, 33, 38]; // ignore the feature for now. Leave this empty
  public gPortfolioIdOffset: number = 10000;

  tabPrtfSpecVisibleIdx = 1; // tab buttons for portfolio specification preview of positions and strategy parameters

  prtfRunResult: Nullable<PrtfRunResultJs> = null;
  uiPrtfRunResult: UiPrtfRunResult = new UiPrtfRunResult();
  todayDate: Date = new Date(); // displaying the statistics as of Date on UI

  // the below variables are required for resizing the panels according to users
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
    const panelPrtfTreeId = SqNgCommonUtils.getNonNullDocElementById('panelPrtfTree');
    this.panelPrtfTreeWidth = panelPrtfTreeId.clientWidth as number;
    this.panelPrtfTreeHeight = panelPrtfTreeId.clientHeight as number;

    const panelChartId = SqNgCommonUtils.getNonNullDocElementById('panelChart');
    this.panelPrtfChrtWidth = panelChartId.clientWidth as number;
    this.panelPrtfChrtHeight = panelChartId.clientHeight as number;

    const panelStatsId = SqNgCommonUtils.getNonNullDocElementById('panelStats');
    this.panelStatsWidth = panelStatsId.clientWidth as number;
    this.panelStatsHeight = panelStatsId.clientHeight as number;

    const panelPrtfSpecId = SqNgCommonUtils.getNonNullDocElementById('panelPrtfSpec');
    this.panelPrtfSpecWidth = panelPrtfSpecId.clientWidth as number;
    this.panelPrtfSpecHeight = panelPrtfSpecId.clientHeight as number;

    const approotToolbar = SqNgCommonUtils.getNonNullDocElementById('toolbarId'); // toolbarId is coming from app component
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
      PortfolioManagerComponent.updateUiWithPrtfRunResult(this.prtfRunResult, this.uiPrtfRunResult, this.panelPrtfChrtWidth, this.panelPrtfChrtHeight);
      return resizeBy;
    });
  }

  public ngAfterViewInit(): void { // @ViewChild variables are undefined in ngOnInit(). Only ready in ngAfterViewInit
    this.treeViewState.rootSqTreeViewComponent = this.sqTreeComponent;
  }

  onMouseOverResizer(resizer: string) {
    if (resizer == 'resizer')
      this.makeResizablePrtfTree(resizer);
    if (resizer == 'resizer2')
      this.makeResizablePrtfDetails(resizer);
  }

  makeResizablePrtfTree(resizer: string) {
    const panelPrtfTreeId = SqNgCommonUtils.getNonNullDocElementById('panelPrtfTree');
    const panelPrtfDetailsId = SqNgCommonUtils.getNonNullDocElementById('panelPrtfDetails');
    const resizerDiv = SqNgCommonUtils.getNonNullDocElementById(resizer);

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
    const panelChartId = SqNgCommonUtils.getNonNullDocElementById('panelChart');
    const panelStatsAndPerfSpecId = SqNgCommonUtils.getNonNullDocElementById('panelStatsAndPerfSpec');
    const panelStatsId = SqNgCommonUtils.getNonNullDocElementById('panelStats');
    const panelPrtfSpecId = SqNgCommonUtils.getNonNullDocElementById('panelPrtfSpec');

    const resizerDiv = SqNgCommonUtils.getNonNullDocElementById(resizer2);

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
        this.processFolders(msgObjStr);
        return true;
      case 'PortfMgr.Handshake': // The least frequent message should come last.
        console.log('PortfMgr.Handshake:' + msgObjStr);
        // this.handshakeObj = JSON.parse(msgObjStr);
        return true;
      case 'PortfMgr.PrtfRunResult': // Receives backtest results when user requests
        console.log('PortfMgr.PrtfRunResult:' + msgObjStr);
        this.processPortfolioRunResult(msgObjStr);
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

      if (key === 'n') {
        _this.name = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'p') {
        _this.parentFolderId = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'cTime') {
        _this.creationTime = value;
        return; // if return undefined, original property will be removed
      }

      if (key === 'sAcs') {
        _this.sharedAccess = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'sUsr') {
        _this.sharedUserWithMe = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'bCur') {
        _this.baseCurrency = value;
        return; // if return undefined, original property will be removed
      }
      return value;
    });

    this.portfolios?.forEach((r) => r.prtfItemType = PrtfItemType.Portfolio);
    this.uiNestedPrtfTreeViewItems = PortfolioManagerComponent.createTreeViewData(this.folders, this.portfolios, this.treeViewState); // process folders and portfolios
  }

  processFolders(msgObjStr: string) {
    this.folders = JSON.parse(msgObjStr, function(this: any, key, value) {
      // property names and values are transformed to a shorter ones for decreasing internet traffic.Transform them back to normal for better code reading.

      // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      if (key === 'n') {
        _this.name = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'p') {
        _this.parentFolderId = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'cTime') {
        _this.creationTime = value;
        return; // if return undefined, original property will be removed
      }
      return value;
    });

    this.folders?.forEach((r) => r.prtfItemType = PrtfItemType.Folder);
    this.uiNestedPrtfTreeViewItems = PortfolioManagerComponent.createTreeViewData(this.folders, this.portfolios, this.treeViewState); // process folders and portfolios
  };

  processPortfolioRunResult(msgObjStr: string) {
    this.prtfRunResult = JSON.parse(msgObjStr, function(this: any, key, value) {
      // property names and values are transformed to a shorter ones for decreasing internet traffic.Transform them back to normal for better code reading.

      // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      if (key === 'startPv') {
        _this.startPortfolioValue = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'endPv') {
        _this.endPortfolioValue = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'shrp') {
        _this.sharpeRatio = value == 'NaN' ? NaN : parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'tr') {
        _this.totalReturn = parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'wr') {
        _this.winRate = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'lr') {
        _this.lossingRate = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'srtn') {
        _this.sortino = value == 'NaN' ? NaN : parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'to') {
        _this.turnover = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'ls') {
        _this.longShortRatio = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'bCAGR') {
        _this.benchmarkCAGR = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'bMax') {
        _this.benchmarkMaxDD = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'cwb') {
        _this.correlationWithBenchmark = value;
        return; // if return undefined, original property will be removed
      }
      return value;
    });
    PortfolioManagerComponent.updateUiWithPrtfRunResult(this.prtfRunResult, this.uiPrtfRunResult, this.panelPrtfChrtWidth, this.panelPrtfChrtHeight);
  }

  static createTreeViewData(pFolders: Nullable<FolderJs[]>, pPortfolios: Nullable<PortfolioJs[]>, pTreeViewState: TreeViewState) : TreeViewItem[] {
    if (!(Array.isArray(pFolders) && pFolders.length > 0 ) || !(Array.isArray(pPortfolios) && pPortfolios.length > 0 ))
      return [];

    const treeviewItemsHierarchyResult: TreeViewItem[] = [];
    const tempPrtfItemsDict = {}; // stores the portfolio items temporarly

    for (let i = 0; i < pFolders.length; i++) { // adding folders data to tempPrtfItemsDict
      const fldrItem : FolderJs = pFolders[i];
      tempPrtfItemsDict[fldrItem.id] = fldrItem;
    }

    for (let j = 0; j < pPortfolios.length; j++) { // adding portfolios data to tempPrtfItemsDict
      const prtfItem : PortfolioJs = pPortfolios[j];
      tempPrtfItemsDict[prtfItem.id] = prtfItem;
    }

    for (const id of Object.keys(tempPrtfItemsDict)) // empty the childen array of each item
      tempPrtfItemsDict[id]['children'] = []; // we cannot put this into the main loop, because we should not delete the Children array of an item that comes later.

    for (const id of Object.keys(tempPrtfItemsDict)) {
      const item : TreeViewItem = tempPrtfItemsDict[id];
      item.isSelected = false;

      item.isExpanded = false;
      for (let i = 0; i < pTreeViewState.expandedPrtfFolderIds.length; i++) { // expanded folder Id's check
        if (pTreeViewState.expandedPrtfFolderIds[i] == item.id) {
          item.isExpanded = true;
          break;
        }
      }

      const parentItem: TreeViewItem = tempPrtfItemsDict[item.parentFolderId]; // No Folder has id of -1. If a ParentFolderID == -1, then that item is at the root level, and we say it has no parent, and parentItem is undefined
      if (parentItem != undefined) // if item has a proper parent (so its parentFolderId is not -1)
        parentItem.children.push(item); // add ourselves as a child to the parent object
      else
        treeviewItemsHierarchyResult.push(item); // item is at root level. Add to the result list.
    }

    return treeviewItemsHierarchyResult;
  };

  onPortfoliosRefreshClicked() {
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.RefreshFolders:');
  }

  // Create or Edit Folder
  showCreateOrEditFolderPopup(mode: string) { // mode is create or edit
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (lastSelectedTreeNode == null || lastSelectedTreeNode.prtfItemType != 'Folder') {
      console.log('Cannot Create/Edit, because no folder or portfolio was selected.');
      return;
    }

    console.log('showCreateOrEditFolderPopup(): Mode', mode);

    this.isCreateOrEditFolderPopupVisible = true;
    this.isCreateOrEditPortfolioPopupVisible = false; // close the portfolio popup if it is left open by the user

    if (mode == 'create') {
      this.editedFolder = new FolderJs();
      this.editedFolder.parentFolderId = lastSelectedTreeNode?.id!; // for creating new folder it needs the parentFolderId(i.e. lastSelectedId), so that it can create child folder inside the parent.
      this.parentfolderName = lastSelectedTreeNode?.name!;
    } else {
      if (lastSelectedTreeNode?.id! <= -1) // The user should not be able edit virtual top folders that have negative folder.Id.
        this.isCreateOrEditFolderPopupVisible = false;
      else {
        this.editedFolder.name = lastSelectedTreeNode?.name!;
        this.editedFolder.id = lastSelectedTreeNode?.id!;
        this.editedFolder.parentFolderId = lastSelectedTreeNode?.parentFolderId!;
        this.editedFolder.note = lastSelectedTreeNode?.note!;
        this.parentfolderName = this.folders?.find((r) => r.id == lastSelectedTreeNode?.parentFolderId!)?.name;
      }
    }
  }

  onKeyupParentFolderId(editedFolderParentFolderId: number) { // to get the parentfolder name dynamically based on user entered parentFolderId
    if (!(Array.isArray(this.folders) && this.folders.length > 0 ))
      return;
    console.log('Portfolio Type is:', this.treeViewState.lastSelectedItem?.prtfItemType!);

    this.parentfolderName = 'Not Found';
    for (const fld of this.folders) {
      if (fld.id == editedFolderParentFolderId) {
        this.parentfolderName = fld.name;
        break;
      }
    }
  }

  closeCreateOrEditFolderPopup() {
    this.isCreateOrEditFolderPopupVisible = false;
  }

  onCreateOrEditFolderClicked() {
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Create/Edit, because no Treeitem was selected. Portfolio edit needs a selected Portfolio . Portfolio create needs a selected Folder.');
      return;
    }

    if (!this.editedFolder.name) // the folder name field shouldn't be left empty
      this.isCreateOrEditFolderPopupVisible = true;
    else {
      if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
        this._parentWsConnection.send(`PortfMgr.CreateOrEditFolder:id:${this.editedFolder.id},name:${this.editedFolder.name},prntFId:${this.editedFolder.parentFolderId},note:${this.editedFolder.note}`);
      this.isCreateOrEditFolderPopupVisible = false;
    }
  }

  // Create or Edit Portfolio
  showCreateOrEditPortfolioPopup(mode: string) {
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (lastSelectedTreeNode == null) {
      console.log('Cannot Create/Edit, because no Portfolio was selected.');
      return;
    }

    console.log('showCreateOrEditPortfolioPopup(): Mode', mode);
    if (mode == 'create' && lastSelectedTreeNode?.prtfItemType == PrtfItemType.Portfolio) {
      console.log('Portfolio creation is not allowed under the portfolio');
      return;
    }

    if (mode == 'edit' && lastSelectedTreeNode?.prtfItemType == PrtfItemType.Folder) // simply return , if user clicks on EditPortfolio but the lastSelectedItem is a Folder.
      return;

    this.isCreateOrEditFolderPopupVisible = false; // close the folder popup if it is left open by the user
    this.isCreateOrEditPortfolioPopupVisible = true;
    if (mode == 'create') {
      this.editedPortfolio = new PortfolioJs();
      this.editedPortfolio.parentFolderId = lastSelectedTreeNode?.id!;
      this.parentfolderName = lastSelectedTreeNode?.name!;
    } else {
      this.editedPortfolio.name = lastSelectedTreeNode?.name!;
      this.editedPortfolio.id = lastSelectedTreeNode?.id! - this.gPortfolioIdOffset;
      this.editedPortfolio.parentFolderId = lastSelectedTreeNode?.parentFolderId!;
      this.editedPortfolio.baseCurrency = lastSelectedTreeNode?.baseCurrency!;
      this.editedPortfolio.portfolioType = lastSelectedTreeNode?.type!;
      this.editedPortfolio.sharedAccess = lastSelectedTreeNode?.sharedAccess!;
      this.editedPortfolio.sharedUserWithMe = lastSelectedTreeNode?.sharedUserWithMe!;
      this.editedPortfolio.note = lastSelectedTreeNode?.note!;
      this.parentfolderName = this.folders?.find((r) => r.id == lastSelectedTreeNode.parentFolderId!)?.name;
    }
  }

  closeCreatePortfolioPopup() {
    this.isCreateOrEditPortfolioPopupVisible = false;
  }

  onCreateOrEditPortfolioClicked() {
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Create/Edit, because no Portfolio was selected.');
      return;
    }

    if (!this.editedPortfolio.name) // the portfolio name field shouldn't be left empty
      this.isCreateOrEditPortfolioPopupVisible = true;
    else {
      if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
        this._parentWsConnection.send(`PortfMgr.CreateOrEditPortfolio:id:${this.editedPortfolio.id},name:${this.editedPortfolio.name},prntFId:${this.editedPortfolio.parentFolderId},currency:${this.editedPortfolio.baseCurrency},type:${this.editedPortfolio.portfolioType},access:${this.editedPortfolio.sharedAccess},note:${this.editedPortfolio.note}`);
      this.isCreateOrEditPortfolioPopupVisible = false;
    }
  }

  onOpenPortfolioViewerClicked() {
    if (this.treeViewState.lastSelectedItem == null || this.treeViewState.lastSelectedItem?.prtfItemType != 'Portfolio') {
      console.log('Cannot OpenPortfolioViewer, because no Portfolio was selected.');
      return;
    }
    const prtfId = this.treeViewState.lastSelectedItem.id - this.gPortfolioIdOffset;
    window.open('//sqcore.net/webapps/PortfolioViewer/?id='+ prtfId, '_blank');
  }

  onOpenChartGeneratorClicked() {
    if (this.treeViewState.lastSelectedItem == null || this.treeViewState.lastSelectedItem?.prtfItemType != 'Portfolio') {
      console.log('Cannot OpenChartGenerator, because no Portfolio was selected.');
      return;
    }
    const prtfId = this.treeViewState.lastSelectedItem.id - this.gPortfolioIdOffset;
    window.open('//sqcore.net/webapps/ChartGenerator/?pids='+ prtfId, '_blank');
  }

  // Delete portfolio Item(Folder/Portfolio)
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

  onConfirmDeleteYesClicked() { // this logic deletes a folder or portfolio if it was confirmed by the user
    if (this.treeViewState.lastSelectedItem == null) {
      console.log('Cannot Delete, because no folder or portfolio was selected.');
      return;
    }
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send('PortfMgr.DeletePortfolioItem:id:' + lastSelectedTreeNode.id);
    this.isDeleteConfirmPopupVisible = false;
  }

  onConfirmDeleteNoClicked() { // this logic silently closes the deleteConfirm popup
    this.isDeleteConfirmPopupVisible = false;
  }

  onClickPrtfSpecPreview(tabIdx: number) {
    this.tabPrtfSpecVisibleIdx = tabIdx;
  }

  showPortfolioStats() {
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (lastSelectedTreeNode == null || lastSelectedTreeNode?.prtfItemType != 'Portfolio')
      return;

    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
      this._parentWsConnection.send(`PortfMgr.GetPortfolioRunResult:id:${lastSelectedTreeNode.id - this.gPortfolioIdOffset}`);
  }

  static updateUiWithPrtfRunResult(prtfRunResult: Nullable<PrtfRunResultJs>, uiPrtfRunResult: UiPrtfRunResult, uiChrtWidth: number, uiChrtHeight: number) {
    if (prtfRunResult == null)
      return;

    uiPrtfRunResult.startPortfolioValue = prtfRunResult.pstat.startPortfolioValue;
    uiPrtfRunResult.endPortfolioValue = prtfRunResult.pstat.endPortfolioValue;
    uiPrtfRunResult.totalReturn = prtfRunResult.pstat.totalReturn;
    uiPrtfRunResult.cAGR = parseFloat(prtfRunResult.pstat.cagr);
    uiPrtfRunResult.maxDD = parseFloat(prtfRunResult.pstat.maxDD);
    uiPrtfRunResult.sharpeRatio = prtfRunResult.pstat.sharpeRatio;
    uiPrtfRunResult.stDev = parseFloat(prtfRunResult.pstat.stDev);
    // uiPrtfRunResult.ulcer = parseFloat(prtfRunResult.pstat.ulcer); // yet to calcualte
    uiPrtfRunResult.tradingDays = parseInt(prtfRunResult.pstat.tradingDays);
    uiPrtfRunResult.nTrades = parseInt(prtfRunResult.pstat.nTrades);
    uiPrtfRunResult.winRate = parseFloat(prtfRunResult.pstat.winRate);
    uiPrtfRunResult.lossRate = parseFloat(prtfRunResult.pstat.lossingRate);
    uiPrtfRunResult.sortino = prtfRunResult.pstat.sortino;
    uiPrtfRunResult.turnover = parseFloat(prtfRunResult.pstat.turnover);
    uiPrtfRunResult.longShortRatio = parseFloat(prtfRunResult.pstat.longShortRatio);
    uiPrtfRunResult.fees = parseFloat(prtfRunResult.pstat.fees);
    // uiPrtfRunResult.benchmarkCAGR = parseFloat(prtfRunResult.pstat.benchmarkCAGR); // yet to calcualte
    // uiPrtfRunResult.benchmarkMaxDD = parseFloat(prtfRunResult.pstat.benchmarkMaxDD); // yet to calcualte
    // uiPrtfRunResult.correlationWithBenchmark = parseFloat(prtfRunResult.pstat.correlationWithBenchmark); // yet to calcualte

    uiPrtfRunResult.prtfPosValues.length = 0;
    for (let i = 0; i < prtfRunResult.prtfPoss.length; i++) {
      const posItem = new UiPrtfPositions();
      posItem.sqTicker = prtfRunResult.prtfPoss[i].sqTicker;
      posItem.quantity = prtfRunResult.prtfPoss[i].quantity;
      posItem.avgPrice = prtfRunResult.prtfPoss[i].avgPrice;
      posItem.price = prtfRunResult.prtfPoss[i].lastPrice;
      posItem.holdingCost = posItem.avgPrice * posItem.quantity;
      posItem.holdingValue = posItem.price * posItem.quantity;
      uiPrtfRunResult.prtfPosValues.push(posItem);
    }

    uiPrtfRunResult.chrtValues.length = 0;
    for (let i = 0; i < prtfRunResult.chrtData.dates.length; i++) {
      const chartItem = new UiChartPointValues();
      const mSecSinceUnixEpoch: number = prtfRunResult.chrtData.dates[i] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
      chartItem.date = new Date(mSecSinceUnixEpoch);
      chartItem.value = prtfRunResult.chrtData.values[i];
      uiPrtfRunResult.chrtValues.push(chartItem);
    }

    d3.selectAll('#pfRunResultChrt > *').remove();
    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const margin = {top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PanelChart Width
    const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PanelChart Height
    const chrtData = uiPrtfRunResult.chrtValues.map((r:{ date: Date; value: number; }) => ({date: new Date(r.date), value: r.value}));
    const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
    const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
    const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
    const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

    processUiWithPrtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);
  }
}