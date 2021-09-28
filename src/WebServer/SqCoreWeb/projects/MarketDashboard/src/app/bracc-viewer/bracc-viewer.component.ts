import { ViewChild, Component, AfterViewInit,ElementRef, Input} from '@angular/core';
import { SqNgCommonUtilsTime } from './../../../../sq-ng-common/src/lib/sq-ng-common.utils_time';   // direct reference, instead of via 'public-api.ts' as an Angular library. No need for 'ng build sq-ng-common'. see https://angular.io/guide/creating-libraries
// Importing d3 library
import * as d3 from 'd3';
import * as d3Shape from 'd3';
import * as d3Axis from 'd3';
import { gDiag, AssetLastJs } from './../../sq-globals';

type Nullable<T> = T | null;

// Input data classes

class AssetJs {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';
}

class AssetPriorCloseJs {
  public assetId = NaN;
  public date = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
  public priorClose = NaN;
}

class BrAccVwrHandShk {
  marketBarAssets: Nullable<AssetJs[]> = null;
  selectableNavAssets: Nullable<AssetJs[]> = null;
}
class AssetSnapPossPosJs {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';
  public pos = NaN;
  public avgCost = NaN;
  public priorClose = NaN;
  public estPrice = NaN;
  public estUndPrice = NaN;
  public accId = ''
}

class AssetSnapPossJs {
  public symbol = '';
  public lastUpdate = '';
  public netLiquidation = NaN;
  public priorCloseNetLiquidation = NaN;
  public grossPositionValue = NaN;
  public totalCashValue = NaN;
  public initMarginReq = NaN;
  public maintMarginReq = NaN;
  public poss : Nullable<AssetSnapPossPosJs[]> = null;
}

class RtMktSumNonRtStat {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public ticker = '';
 
}

class AssetHistValuesJs{
  public assetId = NaN;
  public sqTicker = '';
  public periodStartDate = '';
  public periodEndDate = '';
  public histDates = [];
  public histSdaCloses = [];
}

class AssetHistStatJs{
  public assetId = NaN;
  public sqTicker = '';
  public periodStartDate = '';
  public periodEndDate = '';
  public periodStart = NaN;
  public periodEnd = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;

}

class HistJs {
  public histValues : Nullable<AssetHistValuesJs> = null;
  public histStat :Nullable<AssetHistStatJs> = null;
}

// UI classes
class UiMktBarItem {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';

  public priorClose  = NaN;
  public last  = 500;
  public pctChg  = 0.01;

}

class UiBrAccChrtHistValRaw {
  public assetId = NaN;
  public histDates = [];
  public histSdaCloses = [];

  // Hist stat values
  public periodStartDate = '';
  public periodEndDate = '';
  public periodStart = NaN;
  public periodEnd = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;

}
// // Hist stat Values
class uiHistStatValues {
  public assetId = NaN;
  public priorClose = NaN;

  public periodStartDate = '';
  public periodEndDate = '';
  public periodStart = NaN;
  public periodEnd = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;

  // calculated fields as numbers
  public periodReturn = NaN; // for period: from startDate to endDate
  public periodMaxDrawDown = NaN; // for period: from startDate to endDate
  public rtReturn = NaN;  // comparing last (rt) price to periodEnd price.
  public return = NaN;  // Total return (from startDate to endDate to last realtime): adding period-return and realtime-return together. Every other performance number (cagr, maxDD) is also Total.
  public cagr = NaN;
  public drawDown = NaN;
  public drawUp = NaN;
  public maxDrawDown = NaN;
  public maxDrawUp = NaN;
}

// Hist chart values
class uiBrcAccChrtval {
  public assetId = NaN;
  public dateStr = '';
  public date = new Date('2021-01-01');
  public sdaClose = NaN;
}

class UiSnapTable {
  public symbol = '';
  
  public lastUpdate = '';
  public snapLastUpateTime = new Date();
  public snapLastUpdateTimeAgo = NaN;
  public snapLastUpdateTimeAgoStr = '';
  public netLiquidation = NaN;
  public netLiquidationStr = '';
  public grossPositionValue = NaN;
  public totalCashValue = NaN;
  public initialMarginReq = NaN;
  public maintMarginReq = NaN;
  public poss = [];
  public sumPlTodVal = 0;
  public sumPlTodPct = 0;
  public longStcokValue = 0;
  public shortStockValue = 0;
  public totalMaxRiskedN = 0;
  public totalMaxRiskedLeverage = 0;
  public numOfPoss = 0;

}

class UiAssetSnapPossPos {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';
  public pos = NaN;
  public avgCost = NaN;
  public priorClose = NaN;
  public priorCloseStr = '';
  public estPrice = NaN;
  public pctChgTod = NaN;
  public plTod = NaN;
  public pl = NaN;
  public mktVal = NaN;
  public estUndPrice = NaN;
  public gBeta = 1; // guessed Beta
  public betaDltAdj = 1;
  public accId = '';
  
}


@Component({
  selector: 'app-bracc-viewer',
  templateUrl: './bracc-viewer.component.html',
  styleUrls: ['./bracc-viewer.component.scss'],
  // encapsulation: ViewEncapsulation.None
})
export class BrAccViewerComponent implements AfterViewInit {
  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  @ViewChild('chart') chartRef!:ElementRef;

  // Guessed Beta for HL hedges and companies
  // MarketWatch Beta calculation is quite good. Use that If it is available.  There, Beta of QQQ: 1.18, that is the base.  
  static betaArr: { [id: string] : number; } = 
    {"QQQ": 1.18/1.18, "TQQQ": 3.0, "SQQQ": -3.0, "SPY": 1/1.18, "SPXL": 3*1/1.18, "UPRO": 3*1/1.18, "SPXS": -3*1/1.18, "SPXU": -3*1/1.18, "TWM": -2.07/1.18,            // market ETFs
    "VXX": -3.4/1.18,  "VXZ": -1.82/1.18,  "SVXY": 1.7/1.18, "ZIV": 1.81/1.18,                  // VIX
    "TLT": -0.50/1.18, // https://www.ishares.com/us/products/239454/ishares-20-year-treasury-bond-etf says -0.25, MarketWatch: -0.31, discretionary override from -0.31 to -0.50 (TMF too)
    "TMF": 3*-0.50/1.18, "TMV": -1*3*-0.50/1.18,  "TIP": -0.06/1.18, 
    "USO": 0.83/1.18, "SCO": -2.0*0.83/1.18, "UCO": 1.25/1.18, 
    "UNG": 0.23/1.18,   // discretionary override from 0.03 to 0.23 (UGAZ too)
    "UGAZ": 3*0.23/1.18,     
    "GLD": (-0.24*1.18)/1.18,  // GLD has no Beta on MarketWatch. YF (5Years, monthly): 0.04. But DC's discretionary (logical) override: -0.24 
    "TAIL": -1/1.18,    // compared TAIL vs. SPY and it moves about the same beta, just opposite
    "UUP": (-0.31)/1.18,    // YF Beta calculation; when market panics, the whole world wants to buy safe USA treasuries, therefore USD goes up => negative correlation.
    // companies
    "PM": 0.62/1.18 ,
    };     // it is QQQ Beta, not SPY beta

  handshakeStr = '[Nothing arrived yet]';
  handshakeObj: Nullable<BrAccVwrHandShk> = null;
  mktBrLstClsStr = '[Nothing arrived yet]';
  mktBrLstClsObj: Nullable<AssetPriorCloseJs[]> = null;

  lstValObj: Nullable<AssetLastJs[]> = null;  // realtime or last values

  mktBrLstObj: Nullable<AssetLastJs[]> = null;

  histStr = '[Nothing arrived yet]';
  histObj: Nullable<HistJs[]> = null;

  selectedNav = '';
  uiMktBar: UiMktBarItem[] = [];

  brAccChrtData1: UiBrAccChrtHistValRaw[] = [];
  brAccHistStatVal : uiHistStatValues [] = []; // histstat values can be used in brAccViewer
  brAccChrtActuals : uiBrcAccChrtval [] = [] ; //Combining 2 arrays histdates and histsdaclose

  lastNonRtMsg: Nullable<RtMktSumNonRtStat[]> = null;

  brAccountSnapshotStr = '[Nothing arrived yet]';
  brAccountSnapshotObj : Nullable<AssetSnapPossJs>=null;

  uiSnapTab : UiSnapTable = new UiSnapTable();
  uiSnapPos : AssetSnapPossPosJs[] = [];
  uiSnapPosItem: UiAssetSnapPossPos[] = [];

  tabPageVisibleIdx = 1;

  sortColumn : string = "DailyPL";
  sortDirection : string = "Increase";

  uiNavSel : string[] = [];
  navSelectionSelected = '';

  yrSelectionChoices = ['YTD','1M','1Y','3Y','5Y'];
  yrSelectionSelected = 'YTD';

  lookbackStartET: Date; // set in ctor. We need this in JS client to check that the received data is long enough or not (Expected Date)
  lookbackStartETstr: string; // set in ctor; We need this for sending String instruction to Server. Anyway, a  HTML <input date> is always a 	A DOMString representing a date in YYYY-MM-DD format, or empty. https://developer.mozilla.org/en-US/docs/Web/HTML/Element/input/date
  lookbackEndET: Date;
  lookbackEndETstr: string;

  // required for chart
  private margin = {top: 40, right: 50, bottom: 35, left: 100 };
  private width: number;
  private height: number;
  private x: any;
  private y: any;
  private svg: any;
  public tooltip: any;
  private line!: d3Shape.Line<[number, number]>;
  // private line1!: d3Shape.Line<[number, number]>;
  // sqTicker: any;
  // ticker: any;
  // isNavColumn: any;

  constructor() {

    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds

    this.lookbackStartET = new Date(todayET.getFullYear() - 1, 11, 31);  // set YTD as default
    this.lookbackStartETstr = this.Date2PaddedIsoStr(this.lookbackStartET);

    // https://stackoverflow.com/questions/563406/add-days-to-javascript-date
    const yesterDayET = new Date(todayET);
    yesterDayET.setDate(yesterDayET.getDate() - 1);
    this.lookbackEndET = new Date(yesterDayET.getFullYear(), yesterDayET.getMonth(), yesterDayET.getDate());  // set yesterdayET as default
    this.lookbackEndETstr = this.Date2PaddedIsoStr(this.lookbackEndET);
    
    // Creating a Width and Height data points
    this.width = 800 - this.margin.left - this.margin.right;
    this.height = 400 - this.margin.top - this.margin.bottom;
   }

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'BrAccViewer.RtStat':  // this is the most frequent case. Should come first.

        BrAccViewerComponent.updateMktBarUi((this.handshakeObj == null) ? null : this.handshakeObj.marketBarAssets, this.mktBrLstClsObj, null, this.uiMktBar);
        return true;
      case 'BrAccViewer.BrAccSnapshot':
        console.log('BrAccViewer.BrAccSnapshot:' + msgObjStr);
        this.brAccountSnapshotStr = msgObjStr;
        this.brAccountSnapshotObj = JSON.parse(msgObjStr);
        BrAccViewerComponent.updateSnapshotTable(this.brAccountSnapshotObj, this.sortColumn, this.sortDirection, this.uiSnapTab, this.uiSnapPosItem) 
        const jsonObjSnap = JSON.parse(msgObjStr);
        this.updateUiWithSnapshot(jsonObjSnap);
        return true;
      case 'BrAccViewer.Hist':
        console.log('BrAccViewer.Hist:' + msgObjStr);
        this.histStr = msgObjStr;
        this.histObj = JSON.parse(msgObjStr);
        BrAccViewerComponent.updateChrtUi(this.histObj, this.brAccChrtData1, this.brAccHistStatVal, this.brAccChrtActuals,this.uiSnapPosItem);
        this.fillChartWithData();
      
        // if message is too large without spaces, we have problems as there is no horizontal scrollbar in browser. So, shorten the message.
        if (msgObjStr.length < 200)
          this.histStr = msgObjStr;
        else
          this.histStr = msgObjStr.substring(0, 200) + '... [more data arrived]';
        return true;
      case 'BrAccViewer.MktBrLstCls':
        console.log('BrAccViewer.MktBrLstCls:' + msgObjStr);
        this.mktBrLstClsStr = msgObjStr;
        this.mktBrLstClsObj = JSON.parse(msgObjStr);
        BrAccViewerComponent.updateMktBarUi((this.handshakeObj == null) ? null : this.handshakeObj.marketBarAssets, this.mktBrLstClsObj, null, this.uiMktBar);
       
        return true;
      case 'BrAccViewer.Handshake':  // this is the least frequent case. Should come last.
        console.log('BrAccViewer.Handshake:' + msgObjStr);
        this.handshakeStr = msgObjStr;
        this.handshakeObj = JSON.parse(msgObjStr);
        console.log(`BrAccViewer.Handshake.SelectableBrAccs: '${(this.handshakeObj == null) ? null : this.handshakeObj.selectableNavAssets}'`);
        this.updateUiSelectableNavs((this.handshakeObj == null) ? null : this.handshakeObj.selectableNavAssets);
        return true;
      default:
        return false;
    }
  }

  public webSocketLstValArrived(p_lstValObj: Nullable<AssetLastJs[]>) {
    this.lstValObj = p_lstValObj;
    BrAccViewerComponent.updateMktBarUi((this.handshakeObj == null) ? null : this.handshakeObj.marketBarAssets, this.mktBrLstClsObj, this.lstValObj, this.uiMktBar);
  }
  
  updateUiSelectableNavs(pSelectableNavAssets: Nullable<AssetJs[]>) {  // same in MktHlth and BrAccViewer

    if(pSelectableNavAssets == null) return;
    this.navSelectionSelected = '';
    for (const nav of pSelectableNavAssets) {
      if (this.navSelectionSelected == '') // by default, the selected Nav is the first from the list
        this.navSelectionSelected = nav.symbol;
      this.uiNavSel.push(nav.symbol)
      //navSelectElement.options[navSelectElement.options.length] = new Option(nav.symbol, nav.symbol);
    }
  }
  
  onNavSelectedChangeAng(pEvent: any) {
    // this.navSelectionSelected = this.navSelectionChoices[selectedIndex]
    console.log("The Nav Selected angular way:" + this.navSelectionSelected);
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('BrAccViewer.ChangeNav:' + this.navSelectionSelected);
    }
  }
  onLookbackChangeAng() {
    console.log('Calling server with new lookback. StartDateETstr: ' + this.lookbackStartETstr + ', lookbackStartET: ' + this.lookbackStartET);
    gDiag.wsOnLastRtMktSumLookbackChgStart = new Date();
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('MktHlth.ChangeLookback:Date:' + this.lookbackStartETstr + '...' + this.lookbackEndETstr); // we always send the Date format to server, not the strings of 'YTD/10y'
    }
  }

  onSortingClicked(event, p_sortColumn){
    this.sortColumn = p_sortColumn;
    if (this.sortDirection == "Increasing")
      this.sortDirection = "Decreasing";
    else 
      this.sortDirection = "Increasing";
    BrAccViewerComponent.updateSnapshotTable(this.brAccountSnapshotObj, this.sortColumn, this.sortDirection, this.uiSnapTab, this.uiSnapPosItem) 
  }

  // tabpage 
  tabHeaderClicked (event: any, tabIdx: number) {
    this.tabPageVisibleIdx = tabIdx;
  }

  onDateRefreshClicked (event) {
    // console.log("Refreshing every 3 secs");
    setInterval ( function (self) {
      // console.log("Refreshing every 3 secs")}, 3000);
    // this.uiSnapTab.snapLastUpdateTimeAgo = setInterval( (function(self) {         //Self-executing func which takes 'this' as self
          return function(self) {   //Return a function in the context of 'self'
            self.onMockupRefresh() //Thing you wanted to run as non-window 'this'
          }
      }),
      3000     //normal interval, 'this' scope not impacted here); 
  }
  onMockupRefresh () {
    return this.uiSnapTab.snapLastUpdateTimeAgoStr
  }

  updateUiWithSnapshot(jsonObjSnap: any)  {
    console.log(`BrAccViewer.updateUiWithSnapshot(). Symbol: '${jsonObjSnap.symbol}'`);
    if (this.selectedNav != jsonObjSnap.symbol) // change UI only if it is a meaningful change
      this.selectedNav = jsonObjSnap.symbol;
  }

  static updateMktBarUi(marketBarAssets: Nullable<AssetJs[]>, priorCloses: Nullable<AssetPriorCloseJs[]>, lastRt: Nullable<AssetLastJs[]>, uiMktBar: UiMktBarItem[]) {
     // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
     if (!(Array.isArray(marketBarAssets) && marketBarAssets.length > 0 && Array.isArray(priorCloses) && priorCloses.length > 0  && Array.isArray(lastRt) && lastRt.length > 0)) {
      return;
    }
    for (const item of marketBarAssets) {
      let uiItem: UiMktBarItem;
      const existingUiCols = uiMktBar.filter(
        (r) => r.sqTicker === item.sqTicker
      );
      if (existingUiCols.length === 0) {
        uiItem = new UiMktBarItem();
        uiItem.assetId = item.assetId;
        uiItem.sqTicker = item.sqTicker;
        uiItem.symbol = item.symbol;
        uiItem.name = item.name;
        uiMktBar.push(uiItem);
      } else if (existingUiCols.length === 1) {
        uiItem = existingUiCols[0];
      } else {
        console.warn(`Received ticker '${item.sqTicker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`,"background: #222; color: red");
        uiItem = existingUiCols[0];
      }
    }

    // Step 2: use LastCloses data, and write it into uiMktBar array.
    for (const nonRt of priorCloses) {
      const existingUiCols = uiMktBar.filter((r) => r.assetId === nonRt.assetId);
      if (existingUiCols.length === 0) {
        console.warn(`Received assetId '${nonRt.assetId}' is not found in UiArray.`);
        break;
      }
      const uiItem = existingUiCols[0];
      uiItem.priorClose = nonRt.priorClose;
    }
    // Step 3: use real-time data (we have to temporary generate it)
    for (const rtItem of lastRt) {
      const existingUiItems = uiMktBar.filter((r) => r.assetId === rtItem.assetId);
      if (existingUiItems.length === 0)
        continue;
      const uiItem = existingUiItems[0];
      uiItem.pctChg = (rtItem.last - uiItem.priorClose) / uiItem.priorClose;
      
    }
  }

  static updateSnapshotTable(brAccSnap : Nullable<AssetSnapPossJs>, sortColumn : string, sortDirection : string, uiSnapTab : UiSnapTable, uiSnapPosItem: UiAssetSnapPossPos[])
  {
    if (brAccSnap === null || brAccSnap.poss === null) return;

    uiSnapTab.symbol = brAccSnap.symbol;
    uiSnapTab.lastUpdate = brAccSnap.lastUpdate;
    uiSnapTab.snapLastUpateTime = new Date(brAccSnap.lastUpdate);
    const timestampDate = new Date (brAccSnap.lastUpdate);
    const timeAgoMsec = new Date (Date.now()- timestampDate.getTime());
    const timeAgoMSecStr = timeAgoMsec.toString();     // number of milliseconds
    console.log("the Time Stamp is", timeAgoMSecStr);
    console.log(timeAgoMSecStr.substring(19,21) + 'min' + timeAgoMSecStr.substring(22,24) + 'sec' );
    uiSnapTab.snapLastUpdateTimeAgoStr = timeAgoMSecStr.substring(19,21) + 'min' + timeAgoMSecStr.substring(22,24) + 'sec' ;
    uiSnapTab.snapLastUpdateTimeAgo = Math.round((Date.now() - (new Date (brAccSnap.lastUpdate).getTime()))/ (1000 * 60));
    console.log("The snapLastUpdateTimeAgo: ", uiSnapTab.snapLastUpdateTimeAgo);
    uiSnapTab.totalCashValue = brAccSnap.totalCashValue;
    uiSnapTab.initialMarginReq = brAccSnap.initMarginReq;
    uiSnapTab.maintMarginReq = brAccSnap.maintMarginReq;
    uiSnapTab.grossPositionValue = brAccSnap.grossPositionValue;
    uiSnapTab.netLiquidation = brAccSnap.netLiquidation;
    uiSnapTab.netLiquidationStr = brAccSnap.netLiquidation.toString();
    
    // uiSnapPos = [];
    uiSnapPosItem.length = 0;

    for (const possItem of brAccSnap.poss) {
      console.log("The positions of UiSnapTable are :" + possItem.pos);

      let uiPosItem = new UiAssetSnapPossPos();
      uiPosItem.assetId = possItem.assetId;
      uiPosItem.sqTicker = possItem.sqTicker;
      uiPosItem.symbol = possItem.symbol;
      uiPosItem.name = possItem.name;
      // BrAccViewerComponent.betaArr 
      uiPosItem.gBeta = (uiPosItem.symbol in BrAccViewerComponent.betaArr ) ? BrAccViewerComponent.betaArr [uiPosItem.symbol] : 1.0;
      uiPosItem.pos = possItem.pos;
      uiPosItem.avgCost = possItem.avgCost;
      uiPosItem.priorClose = possItem.priorClose;
      uiPosItem.priorCloseStr = possItem.priorClose.toFixed(2).toString();
      uiPosItem.estPrice = possItem.estPrice;
      uiPosItem.estUndPrice = possItem.estUndPrice;
      uiPosItem.accId = possItem.accId;
      uiPosItem.mktVal = Math.round(possItem.pos * possItem.estPrice);
      uiPosItem.pctChgTod =
        (possItem.estPrice - possItem.priorClose) / possItem.estPrice;
      uiPosItem.plTod = Math.round(
        possItem.pos * (possItem.estPrice - possItem.priorClose)
      );
      uiPosItem.pl = Math.round(possItem.pos * (possItem.estPrice - possItem.avgCost))
      uiPosItem.betaDltAdj = Math.round(uiPosItem.gBeta * uiPosItem.mktVal)
      uiSnapPosItem.push(uiPosItem);
    }

    //var sumPlTod = 0;
    uiSnapTab.sumPlTodVal = 0;
    uiSnapTab.longStcokValue = 0;
    uiSnapTab.shortStockValue = 0;
    uiSnapTab.totalMaxRiskedN = 0;
    for (const item of uiSnapPosItem) {
      uiSnapTab.sumPlTodVal += item.plTod;
      if (item.mktVal > 0){ //Long and Short stock values
        uiSnapTab.longStcokValue += item.mktVal;
      } else if (item.mktVal < 0) {
        uiSnapTab.shortStockValue += item.mktVal;
      }
      uiSnapTab.totalMaxRiskedN += Math.abs(item.mktVal);
    } 
    uiSnapTab.sumPlTodPct = uiSnapTab.sumPlTodVal/uiSnapTab.netLiquidation; // profit & Loss total percent change
    uiSnapTab.totalMaxRiskedLeverage = (uiSnapTab.totalMaxRiskedN/uiSnapTab.netLiquidation);
    uiSnapTab.numOfPoss = (uiSnapPosItem.length) - 1;
  
    // sort by sortColumn

    uiSnapPosItem.sort((n1: UiAssetSnapPossPos, n2: UiAssetSnapPossPos) => {

      let dirMultiplier = (sortDirection === "Increasing") ? 1 : -1;
      switch (sortColumn) {
        case 'Symbol':
          if (n1.symbol < n2.symbol) {
            return 1 * dirMultiplier;
          } else if (n1.symbol > n2.symbol) {
            return -1 * dirMultiplier;
          }
          break;
        case 'Pos':
          if (n1.pos < n2.pos) return 1 * dirMultiplier;
          if (n1.pos > n2.pos) return -1 * dirMultiplier;
          break;
        case 'Cost':
          if (n1.avgCost < n2.avgCost) return 1 * dirMultiplier;
          if (n1.avgCost > n2.avgCost) return -1 * dirMultiplier;
          break;
        case 'PriorClose':
          if (n1.priorClose < n2.priorClose) return 1 * dirMultiplier;
          if (n1.priorClose > n2.priorClose) return -1 * dirMultiplier;
          break;
        case 'EstPrice':
          if (n1.estPrice < n2.estPrice) return 1 * dirMultiplier;
          if (n1.estPrice > n2.estPrice) return -1 * dirMultiplier;
          break;
        case 'DailyPctChg':
          if (n1.pctChgTod < n2.pctChgTod) return 1 * dirMultiplier;
          if (n1.pctChgTod > n2.pctChgTod) return -1 * dirMultiplier;
          break;
        case 'DailyPL':
          if (n1.plTod < n2.plTod) return 1 * dirMultiplier;
          if (n1.plTod > n2.plTod) return -1 * dirMultiplier;
          break;
        case 'ProfLos':
          if (n1.plTod < n2.plTod) return 1 * dirMultiplier;
          if (n1.plTod > n2.plTod) return -1 * dirMultiplier;
          break;
        case 'MktVal':
          if (n1.mktVal < n2.mktVal) return 1 * dirMultiplier;
          if (n1.mktVal > n2.mktVal) return -1 * dirMultiplier;
          break;
        case 'EstUndPrice':
          if (n1.estUndPrice < n2.estUndPrice) return 1 * dirMultiplier;
          if (n1.estUndPrice > n2.estUndPrice) return -1 * dirMultiplier;
          break;
        // case 'IbCompUndPr':
        //   if (n1.ibCompUndr < n2.ibCompUndr) return 1 * dirMultiplier;
        //   if (n1.ibCompUndr > n2.ibCompUndr) return -1 * dirMultiplier;
        //   break;
        case 'gBeta':
          if (n1.gBeta < n2.gBeta) return 1 * dirMultiplier;
          if (n1.gBeta > n2.gBeta) return -1 * dirMultiplier;
          break;
        case 'gBetaDltAdj':
          if (n1.betaDltAdj < n2.betaDltAdj) return 1 * dirMultiplier;
          if (n1.betaDltAdj > n2.betaDltAdj) return -1 * dirMultiplier;
          break;
      
        default:
          console.warn("Urecognized...***");
          break;
      }
      return 0;
    });
  }

  static updateChrtUi(histObj : Nullable<HistJs[]>, brAccChrtData1: UiBrAccChrtHistValRaw[], brAccHistStatVal : uiHistStatValues[], brAccChrtActuals : uiBrcAccChrtval [],uiSnapPosItem: UiAssetSnapPossPos[]) {
    if (histObj == null)
      return;

      for (const histItem of histObj) {
        if (histItem.histStat == null) continue;
        if (histItem.histValues == null) continue;
        console.log(histItem.histStat.sqTicker);
        console.log(histItem.histStat.assetId);

        let chrtItem = new UiBrAccChrtHistValRaw();
        chrtItem.assetId = histItem.histStat.assetId;
        chrtItem.histDates = histItem.histValues.histDates;
        chrtItem.histSdaCloses = histItem.histValues.histSdaCloses;
      }

    let histValues = histObj[0].histValues;
    let histStat = histObj[0].histStat;
    if (histValues == null)
      return;
  
    if (histStat ==  null) {
      return ;
    }
    // uiSnapPosItem.length = 0;

    // brAccHistStatVal.length = 0;
    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds

    // for (const item of uiSnapPosItem) {
    //   let statItem = new uiHistStatValues();
    //   statItem.priorClose = item.priorClose;
    // }

    for (const hisStatItem  of histObj) {
      if (hisStatItem.histStat ==  null) continue;
      let statItem = new uiHistStatValues();

      statItem.assetId = hisStatItem.histStat.assetId;      
      statItem.periodEnd = hisStatItem.histStat.periodEnd;
      statItem.periodEndDate = hisStatItem.histStat.periodEndDate;
      statItem.periodHigh = hisStatItem.histStat.periodHigh;
      statItem.periodLow = hisStatItem.histStat.periodLow;
      statItem.periodMaxDD = hisStatItem.histStat.periodMaxDD;
      statItem.periodMaxDU = hisStatItem.histStat.periodMaxDU;
      statItem.periodStart = hisStatItem.histStat.periodStart
      statItem.periodStartDate = hisStatItem.histStat.periodStartDate;

      // preparing values
      statItem.periodReturn = statItem.periodEnd / statItem.periodStart - 1;
      statItem.periodMaxDrawDown = statItem.periodMaxDD;
      statItem.rtReturn = statItem.priorClose > 0 ? statItem.priorClose / statItem.periodEnd - 1 : 0;
      statItem.return = statItem.priorClose > 0 ? statItem.priorClose / statItem.periodStart - 1 : statItem.periodEnd / statItem.periodStart - 1;
      const dataStartDateET = new Date(statItem.periodStartDate);  // '2010-09-29T00:00:00' which was UTC is converted to DateObj interpreted in Local time zone {Tue Sept 29 2010 00:00:00 GMT+0000 (Greenwich Mean Time)}
      const nDays = SqNgCommonUtilsTime.DateDiffNdays(dataStartDateET, todayET); // 2 weeks = 14 days, 2020 year: 366 days, because it is a leap year.
      const nYears = nDays / 365.25; // exact number of days in a year in average 365.25 days, because it is 3 times 365 and 1 time 366
      statItem.cagr = Math.pow(1 + statItem.return, 1.0 / nYears) - 1;
      statItem.drawDown = statItem.priorClose > 0 ? statItem.priorClose / Math.max(statItem.periodHigh, statItem.priorClose) - 1 : statItem.periodEnd / statItem.periodHigh - 1;
      statItem.drawUp = statItem.priorClose > 0 ? statItem.priorClose / Math.min(statItem.periodLow, statItem.priorClose) - 1 : statItem.periodEnd / statItem.periodLow - 1;
      statItem.maxDrawDown = Math.min(statItem.periodMaxDD, statItem.drawDown);
      statItem.maxDrawUp = Math.max(statItem.periodMaxDU, statItem.drawUp);
      brAccHistStatVal.push(statItem);
    }

    brAccChrtActuals.length = 0;
    for (var i = 0; i < histValues.histDates.length; i++ ) {
      let elem = new uiBrcAccChrtval();
      elem.assetId = histValues.assetId;
      //let shortDateStr = histValues.histDates[i];
      //let longDateStr = shortDateStr.substring(0,4) + '-' + ...
      elem.dateStr = histValues.histDates[i];
      elem.date = new Date (elem.dateStr.substring(0,4) + '-' + elem.dateStr.substring(4,6) + '-' + elem.dateStr.substring(6,8));
      elem.sdaClose = histValues.histSdaCloses[i]
      brAccChrtActuals.push(elem);
      // console.log("The brAccChrtActual legnth is ", brAccChrtActuals.length);
    }
  }
  ngAfterViewInit(): void {
  
    // functions for developing charts
    this.initChart();
        
  }
  // Chart functions start
  private initChart() {
    // 
  }
  // private showToolTip() {

  // }
  private fillChartWithData() {

  this.svg = d3.select('svg#chartNav')
                .attr("width", this.width + this.margin.left + this.margin.right)
                .attr("height", this.height + this.margin.top + this.margin.bottom);
                //  .call(responsivefy)
  this.svg.append('g').attr('transform', 'translate(' + this.margin.left + ',' + this.margin.top + ')');
  // this.svg.reset();
  this.brAccChrtActuals.map(
      (d: {assetId:string | number; date: string | number | Date; sdaClose: string | number; }) => 
      ({assetId: +d.assetId,
        date: new Date(d.date),
        sdaClose: +d.sdaClose,
      }))
      // find data range
  const xMin = d3.min(this.brAccChrtActuals, (d:{ date: any; }) => d.date);
  const xMax = d3.max(this.brAccChrtActuals, (d:{ date: any; }) => d.date);
  const yMin = d3.min(this.brAccChrtActuals, (d: { sdaClose: any; }) => d.sdaClose );
  const yMax = d3.max(this.brAccChrtActuals, (d: { sdaClose: any; }) => d.sdaClose );
  // range of data configuring
  this.x = d3.scaleTime()
            .domain([xMin, xMax])
            .range([0, this.width]);
  this.y = d3.scaleLinear()
              .domain([yMin-5, yMax])
              .range([this.height, 0]);

  // const tooltip = d3.select('#tooltip');
  // const tooltipLine = this.svg.append('line');
  // Configure the X axis
    
  this.svg.append('g')
          .attr('transform', 'translate(0,' + this.height + ')')
          .call(d3Axis.axisBottom(this.x))
    // .tickFormat(d3.format('%Y-%m-%d'));
    // .tickFormat(((d:{ date: any; }) => d.date))
      // .tickFormat(d3.timeFormat("%Y-%m-%d"))
      // .tickValues(tickvalues));
  
    // text label for x-axis
  this.svg.append("text")
          .attr("x", this.width/2)
          .attr("y", this.height + this.margin.bottom) 
          .style("text-anchor","middle")
          .text("Date");
    // Configure the Y Axis
  this.svg.append('g')
          .attr('class', 'axis--y')
          .call(d3Axis.axisLeft(this.y))
  // .tickFormat(d3.format('$.4'));

    // text label for y-axis
  this.svg.append("text")
      .attr("transform", "rotate(-90)")
      .attr("y", 0-this.margin.left)
      .attr("x", 0-(this.height/2))
      .attr("dy","1em")
      .style("text-anchor", "middle")
      .text("sdaClose");

  const coordinates = function (event){
      let coords = d3.pointer(event);
      console.log("The Coordinate values in coordinate function are : ", coords[0], coords[1]);
    }

  // Genereating line - for sdaCloses 
  this.line = d3Shape.line()
                     .x( (d: any) => this.x(d.date))
                     .y( (d: any) => this.y(d.sdaClose))
// Genereating line - for SPY
    // this.line1 = d3Shape.line()
    // .x( (d: any) => this.x(d.dateStr))
    // .y( (d: any) => this.y(d.SPY))

    // Configuring line path
    // Append the path, bind the data, and call the line generator (brNAV)
  this.svg.append('path')
          .datum(this.brAccChrtActuals) // Binds data to the line
          .attr('class', 'line') //Assign a class for styling
          .attr('d', this.line
          .curve(d3.curveCardinal))
          .on("mouseenter", coordinates)
      //  (d: any) => { 
      //   // showToolTip(d.sdaClose);
      //           d3.select('#tooltip')
      //       // .append('text')
      //           .attr('d',this.y(d.sdaClose))
      //           .style("display", "block")
      // })
          .on("mouseleave", (d: any) => { 
                      d3.select('#tooltip')
                      .style("display", "none")
                    })
          .on("mouseover",(d: any) => {
                    d3.select('#tooltip')
                    // .append("text")
                    // .text(d.price)
                    .attr("x", this.x(d.date))
                    .attr("y", this.y(d.sdaClose))
                    .style("display", "block")}) // Calls the line generator
          .on("mousemove",function(event) {
            let coords = d3.pointer(event);
            console.log( "The coordinates are: ",coords[0], coords[1] ) // log the mouse x,y position
          });
    
// Append the path, bind the data, and call the line generator (SPY)
    // this.svg.append('path')
    // .datum(this.brAccChrtData) // Binds data to the line
    // .attr('class', 'line') //Assign a class for styling
    // .attr('d', this.line1
    // .curve(d3.curveCardinal)); 
  }
 // Chart functions end

  // zeroPad = (num, places: number) => String(num).padStart(places, '0');  // https://stackoverflow.com/questions/2998784/how-to-output-numbers-with-leading-zeros-in-javascript
  // ES5 approach: because 2021-02: it works in CLI, but VsCode shows problems: "Property 'padStart' does not exist on type 'string'. Do you need to change your target library? Try changing the `lib` compiler option to 'es2017' or later."
  public zeroPad(num, places) {
    var zero = places - num.toString().length + 1;
    return Array(+(zero > 0 && zero)).join("0") + num;
  }

  public Date2PaddedIsoStr(date: Date): string {  // 2020-9-1 is not acceptable. Should be converted to 2020-09-01
    // don't use UTC versions, because they will convert local time zone dates to UTC first, then we might have bad result.
    // "date = 'Tue Apr 13 2021 00:00:00 GMT+0100 (British Summer Time)'" because local BST is not UTC date.getUTCDate() = 12, while date.getDate()=13 (correct)
    //return this.zeroPad(date.getUTCFullYear(), 4) + '-' + this.zeroPad(date.getUTCMonth() + 1, 2) + '-' + this.zeroPad(date.getUTCDate(), 2);
    return this.zeroPad(date.getFullYear(), 4) + '-' + this.zeroPad(date.getMonth() + 1, 2) + '-' + this.zeroPad(date.getDate(), 2);
  }

  public PaddedIsoStr3Date(dateStr: string): Date {
    const parts = dateStr.split('-');
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);
    const day = parseInt(parts[2], 10);
    return new Date(year, month - 1, day);
  }

}
