import { ViewChild, Component, AfterViewInit,ElementRef, Input} from '@angular/core';
// Importing d3 library
// import * as d3 from 'd3';
// import * as d3Scale from 'd3';
// import * as d3Shape from 'd3';
// import * as d3Array from 'd3';
// import * as d3Axis from 'd3';
// import { text } from 'd3';
// import { SqNgCommonUtilsTime } from '../../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { AssetLastJs } from './../../sq-globals';

type Nullable<T> = T | null;


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

class UiMktBarItem {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';

  public priorClose  = NaN;
  public last  = 500;
  public pctChg  = 0.01;

}


class RtMktSumRtStat {
  public assetId = NaN;
  public last  = NaN;
  public lastUtc = '';
}

class RtMktSumNonRtStat {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public ticker = '';
  // public periodStartDate = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
  // public periodEndDate = '';
  // public periodStart = NaN;
  // public periodEnd = NaN;   // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
  // public periodHigh = NaN;
  // public periodLow = NaN;
  // public periodMaxDD = NaN;
  // public periodMaxDU = NaN;
}

// 
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

class BrAccVwrChrtDataRaw {
  histValues : Nullable<AssetHistValuesJs[]>=null;
  histStat : Nullable<AssetHistStatJs[]>=null;
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

class AssetSnapPossPosJs {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
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
  public grossPositionValue = NaN;
  public totalCashValue = NaN;
  public initMarginReq = NaN;
  public maintMarginReq = NaN;
  public poss : Nullable<AssetSnapPossPosJs[]> = null;
}

class UiSnapTable {
  public symbol = '';
  public lastUpdate = '';
  public netLiquidation = NaN;
  public netLiquidationStr = '';
  public grossPositionValue = NaN;
  public totalCashValue = NaN;
  public initialMarginReq = NaN;
  public maintMarginReq = NaN;
  public poss = [];
  public sumPlTod = 0;

}


class UiAssetSnapPossPos {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public pos = NaN;
  public avgCost = NaN;
  public priorClose = NaN;
  public priorCloseStr = '';
  public estPrice = NaN;
  public estUndPrice = NaN;
  public accId = ''
  public mktVal = NaN;
  public pctChgTod = NaN;
  public plTod = NaN;
  public pl = NaN;
  public gBeta = 1; // guessed Beta
  public betaDltAdj = 1;
  
}


// UiBrAccChrt is for developing the chart
class UiBrAccChrtDataRaw {
  public assetId = NaN;
  public dateStr ='';
  public brNAV = 0.01;
  public SPY = 0.01;
}

class UiBrAccChrtData {
  public assetId = NaN;
  public dateStr = new Date('2021-01-01');
  public brNAV = 0.01;
  public SPY = 0.01;
}

class UiBrAccChrtDataRaw1 {
  public assetId = NaN;
  public sqTicker = "";
  public histDateStr ='';
  public histSdaClose = 0.01;

}

// class uiTabBetaArr {
//   public QQQ
// }

@Component({
  selector: 'app-bracc-viewer',
  templateUrl: './bracc-viewer.component.html',
  styleUrls: ['./bracc-viewer.component.scss'],
  // encapsulation: ViewEncapsulation.None
})
export class BrAccViewerComponent implements AfterViewInit {
  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  @ViewChild('chart') chartRef!:ElementRef;
  handshakeStr = '[Nothing arrived yet]';
  handshakeObj: Nullable<BrAccVwrHandShk> = null;
  mktBrLstClsStr = '[Nothing arrived yet]';
  mktBrLstClsObj: Nullable<AssetPriorCloseJs[]> = null;

  lstValObj: Nullable<AssetLastJs[]> = null;  // realtime or last values

  mktBrLstObj: Nullable<AssetLastJs[]> = null;

  histStr = '[Nothing arrived yet]';
  histObj: Nullable<HistJs[]> = null;

  histStatStr = '[Nothing arrived yet]';
  histStatObj: Nullable<BrAccVwrChrtDataRaw>=null;
  // histStatObj: Nullable<AssetHistStatJs[]>=null;

  selectedNav = '';
  uiMktBar: UiMktBarItem[] = [];
  brAccChrtDataRaw: UiBrAccChrtDataRaw[] = [];
  brAccChrtData: UiBrAccChrtData[] = [];

  brAccChrtDataRaw1: UiBrAccChrtDataRaw1[] = [];
  brAccChrtData1: UiBrAccChrtHistValRaw[] = [];

  lastRtMsg: Nullable<RtMktSumRtStat[]> = null;
  lastNonRtMsg: Nullable<RtMktSumNonRtStat[]> = null;

  brAccountSnapshotStr = '[Nothing arrived yet]';
  brAccountSnapshotObj : Nullable<AssetSnapPossJs>=null;

  uiSnapTab : UiSnapTable = new UiSnapTable();
  uiSnapPos : AssetSnapPossPosJs[] = [];
  uiSnapPosItem: UiAssetSnapPossPos[] = [];

  tabPageVisibleIdx = 1;

  sortColumn : string = "DailyPL";
  sortDirection : string = "Increase";

  static betaArr: { [id: string] : number; } = {};

  // required for chart
  // private margin = {top:20, right:20, bottom:30, left:50};
  // private width: number;
  // private height: number;
  // private x: any;
  // private y: any;
  // private svg: any;
  // public tooltip: any;
  // private line!: d3Shape.Line<[number, number]>;
  // private line1!: d3Shape.Line<[number, number]>;
  // sqTicker: any;
  // ticker: any;
  // isNavColumn: any;
  // private pageX: any;
  // private pageY: any;
  
  constructor() {
    
    // Creating a line chart dummy data
    // this.brAccChrtDataRaw = [
    //   {assetId:1, dateStr:"2010-01-01", brNAV:310.45, SPY:309},
    //   {assetId:2, dateStr:"2010-01-02", brNAV:320.45, SPY:317},
    //   {assetId:3, dateStr:"2010-01-03", brNAV:350.45, SPY:360},
    //   {assetId:4, dateStr:"2010-01-04", brNAV:340.45, SPY:315},
    // ];

    // this.width = 1000 - this.margin.left - this.margin.right;
    // this.height = 550 - this.margin.top - this.margin.bottom;

// guessed Beta for HL hedges and companies 
 
  //   // MarketWatch Beta calculation is quite good. Use that If it is available.  There, Beta of QQQ: 1.18, that is the base.  
    BrAccViewerComponent.betaArr = {"QQQ": 1.18/1.18, "TQQQ": 3.0, "SQQQ": -3.0, "SPY": 1/1.18, "SPXL": 3*1/1.18, "UPRO": 3*1/1.18, "SPXS": -3*1/1.18, "SPXU": -3*1/1.18, "TWM": -2.07/1.18,            // market ETFs
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
    

   }

  ngAfterViewInit(): void {
  
    // functions for developing charts
    // this.initChart();
    
    
  }


  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'BrAccViewer.RtStat':  // this is the most frequent case. Should come first.
        // if (gDiag.wsOnFirstRtMktSumRtStatTime === minDate) {
        //   gDiag.wsOnFirstRtMktSumRtStatTime = new Date();
        // }
        // gDiag.wsOnLastRtMktSumRtStatTime = new Date();
        // gDiag.wsNumRtMktSumRtStat++;

        // this.nRtStatArrived++;
        // const jsonArrayObjRt = JSON.parse(msgObjStr);
        // // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        // jsonArrayObjRt.forEach(element => {
        //   element.last = this.ChangeNaNstringToNaNnumber(element.last);
        // });
        // const msgStrRt = jsonArrayObjRt.map(s => s.assetId + '=>' + s.last.toFixed(2).toString()).join(', ');  // %Chg: Bloomberg, MarketWatch, TradingView doesn't put "+" sign if it is positive, IB, CNBC, YahooFinance does. Go as IB.
        // console.log('ws: RtMktSumRtStat arrived: ' + msgStrRt);
        // this.lastRtMsgStr = msgStrRt;
        // this.lastRtMsg = jsonArrayObjRt;
        // MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStartET, this.uiTableColumns);

        BrAccViewerComponent.updateMktBarUi((this.handshakeObj == null) ? null : this.handshakeObj.marketBarAssets, this.mktBrLstClsObj, null, this.uiMktBar);
        return true;
      case 'BrAccViewer.BrAccSnapshot':
        console.log('BrAccViewer.BrAccSnapshot:' + msgObjStr);
        this.brAccountSnapshotStr = msgObjStr;
        this.brAccountSnapshotObj = JSON.parse(msgObjStr);
        BrAccViewerComponent.updateSnapshotTable(this.brAccountSnapshotObj, this.sortColumn, this.sortDirection, this.uiSnapTab, this.uiSnapPosItem) 
        const jsonObjSnap = JSON.parse(msgObjStr);
        this.updateUiWithSnapshot(jsonObjSnap);
    

        // if (gDiag.wsOnFirstRtMktSumNonRtStatTime === minDate) {
        //   gDiag.wsOnFirstRtMktSumNonRtStatTime = new Date();
        // }
        // gDiag.wsOnLastRtMktSumNonRtStatTime = new Date();
        // this.nNonRtStatArrived++;
        // const jsonArrayObjNonRt = JSON.parse(msgObjStr);
        // // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        // jsonArrayObjNonRt.forEach(element => {
        //   if (element.sqTicker.startsWith("S/"))
        //     element.ticker = element.sqTicker.substring(2); // "sqTicker":"S/QQQ"
        //   else
        //     element.ticker = element.sqTicker;  // "sqTicker":"BrNAV"
        //   element.periodStart = this.ChangeNaNstringToNaNnumber(element.periodStart);
        //   element.periodEnd = this.ChangeNaNstringToNaNnumber(element.periodEnd);
        //   element.periodHigh = this.ChangeNaNstringToNaNnumber(element.periodHigh);
        //   element.periodLow = this.ChangeNaNstringToNaNnumber(element.periodLow);
        //   element.periodMaxDD = this.ChangeNaNstringToNaNnumber(element.periodMaxDD);
        //   element.periodMaxDU = this.ChangeNaNstringToNaNnumber(element.periodMaxDU);
        // });
        // const msgStrNonRt = jsonArrayObjNonRt.map(s => s.assetId + '|' + s.ticker + '|periodEnd:' + s.periodEnd.toFixed(2).toString() + '|periodStart:' + s.periodStart.toString() + '|open:' + s.periodStart.toFixed(2).toString() + '|high:' + s.periodHigh.toFixed(2).toString() + '|low:' + s.periodLow.toFixed(2).toString() + '|mdd:' + s.periodMaxDD.toFixed(2).toString() + '|mdu:' + s.periodMaxDU.toFixed(2).toString()).join(', ');
        // // console.log('ws: RtMktSumNonRtStat arrived: ' + msgStrNonRt);
        // this.lastNonRtMsgStr = msgStrNonRt;
        // this.lastNonRtMsg = jsonArrayObjNonRt;
        // MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStartET, this.uiTableColumns);
        return true;
      case 'BrAccViewer.Hist':
        console.log('BrAccViewer.Hist:' + msgObjStr);
        this.histStr = msgObjStr;
        this.histObj = JSON.parse(msgObjStr);
        BrAccViewerComponent.updateChrtUi(this.histObj, this.brAccChrtData1);

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


  updateUiSelectableNavs(pSelectableNavAssets: any) {  // same in MktHlth and BrAccViewer
    const navSelectElement = document.getElementById('braccViewerNavSelect') as HTMLSelectElement;
    this.selectedNav = '';
    for (const nav of pSelectableNavAssets) {
      if (this.selectedNav == '') // by default, the selected Nav is the first from the list
        this.selectedNav = nav.symbol;
      navSelectElement.options[navSelectElement.options.length] = new Option(nav.symbol, nav.symbol);
    }
    navSelectElement.selectedIndex = 0; // select the first item
  }

  onSelectedNavClicked(pEvent: any) {   // same in MktHlth and BrAccViewer
    // https://www.w3schools.com/howto/howto_js_popup.asp
    // When the user clicks on header, open the popup
    // https://stackoverflow.com/questions/10554446/no-onclick-when-child-is-clicked
    // part of the event object is the target member. This will tell you which element triggered the event to begin with.
    console.log('onSelectedNavClicked()');
    const popupSpan = document.getElementById('braccViewerNavSelectionPopupId') as HTMLSpanElement;
    if (!(pEvent.target === popupSpan)) { // if not child popup, but the header
      popupSpan.classList.toggle('show');
    }
  }

  onNavSelectionPopupClicked(pEvent: any) { // same in MktHlth and BrAccViewer
    console.log('onNavSelectionPopupClicked()');
    pEvent.stopPropagation();
  }

  onNavSelectChange(pEvent: any) {  // same in MktHlth and BrAccViewer
    const navSelectTicker = (document.getElementById('braccViewerNavSelect') as HTMLSelectElement).value;
    console.log(navSelectTicker);
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('BrAccViewer.ChangeNav:' + navSelectTicker);
    }
  }

  isAscendic = true;

  // send(){
  //   this.isAscendic?this.ascendic():this.descendic()
  // }

  onSortingClicked(event, p_sortColumn, p_sortDirection){
    this.sortColumn = p_sortColumn;
    this.sortDirection = p_sortDirection;
    BrAccViewerComponent.updateSnapshotTable(this.brAccountSnapshotObj, this.sortColumn, this.sortDirection, this.uiSnapTab, this.uiSnapPosItem) 

  }

  // tabpage 
  tabHeaderClicked (event: any, tabIdx: number) {
    this.tabPageVisibleIdx = tabIdx;

  }

  updateUiWithSnapshot(jsonObjSnap: any)  {
    console.log(`BrAccViewer.updateUiWithSnapshot(). Symbol: '${jsonObjSnap.symbol}'`);
    if (this.selectedNav != jsonObjSnap.symbol) // change UI only if it is a meaningful change
      this.selectedNav = jsonObjSnap.symbol;
  }

  static updateMktBarUi(marketBarAssets: Nullable<AssetJs[]>, priorCloses: Nullable<AssetPriorCloseJs[]>, lastRt: Nullable<AssetLastJs[]>, uiMktBar: UiMktBarItem[]) {
     // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
     if (!(Array.isArray(marketBarAssets) && marketBarAssets.length > 0 && Array.isArray(priorCloses) && priorCloses.length > 0  && Array.isArray(lastRt) && lastRt.length > 0)) {
    //  && Array.isArray(lastRt) && lastRt.length > 0 && Array.isArray(lastCloses) && lastCloses.length > 0)
     
      return;
    }

    // uiMktBar is visualized in HTML
    // Step 1.
    // write a code here that goes through marketBarAssets array and fill up uiMktBar.Symbol
    // So, this will be visualized in HTML

    for (const item of marketBarAssets) {
      let uiItem: UiMktBarItem;
      const existingUiCols = uiMktBar.filter(
        (r) => r.sqTicker === item.sqTicker
      );
      if (existingUiCols.length === 0) {
        // console.warn(`Received ticker '${item.sqTicker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
        // uiCol = new UiMktBarItem(stockNonRt.sqTicker, stockNonRt.ticker, false);
        uiItem = new UiMktBarItem();
        uiItem.assetId = item.assetId;
        uiItem.sqTicker = item.sqTicker;
        uiItem.symbol = item.symbol;
        uiItem.name = item.name;
        uiMktBar.push(uiItem);
      } else if (existingUiCols.length === 1) {
        uiItem = existingUiCols[0];
      } else {
        console.warn(
          `Received ticker '${item.sqTicker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`,
          "background: #222; color: red"
        );
        uiItem = existingUiCols[0];
      }
    }

    // Step 2: use LastCloses data, and write it into uiMktBar array.
    // in HTML visualize the LastClose prices temporarily, instead of the real time PercentChange
    for (const nonRt of priorCloses) {
      const existingUiCols = uiMktBar.filter(
        (r) => r.assetId === nonRt.assetId
      );
      if (existingUiCols.length === 0) {
        console.warn(
          `Received assetId '${nonRt.assetId}' is not found in UiArray.`
        );
        break;
      }
      const uiItem = existingUiCols[0];
      uiItem.priorClose = nonRt.priorClose;
    }
    // Step 3: use real-time data (we have to temporary generate it)
    for (const rtItem of lastRt) {
      const existingUiItems = uiMktBar.filter(
        (r) => r.assetId === rtItem.assetId
      );
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
      // BrAccViewerComponent.betaArr 
      uiPosItem.gBeta = (uiPosItem.symbol in BrAccViewerComponent.betaArr ) ? BrAccViewerComponent.betaArr [uiPosItem.symbol] : 1.0;
      uiPosItem.pos = possItem.pos;
      uiPosItem.avgCost = possItem.avgCost;
      uiPosItem.priorClose = possItem.priorClose;
      uiPosItem.priorCloseStr = possItem.priorClose.toString();
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
    uiSnapTab.sumPlTod = 0;
    for (const item of uiSnapPosItem) {
      uiSnapTab.sumPlTod += item.plTod;
    }

  
    // sort by sortColumn

    uiSnapPosItem.sort((n1: UiAssetSnapPossPos, n2: UiAssetSnapPossPos) => {

      let dirMultiplier = (sortDirection == "Increasing") ? 1 : -1;

      switch (sortColumn) {
        case 'Symbol':
          if (n1.symbol < n2.symbol) return 1 * dirMultiplier;
          if (n1.symbol > n2.symbol) return -1 * dirMultiplier;
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

  static updateChrtUi(histObj : Nullable<HistJs[]>, brAccChrtData1: UiBrAccChrtHistValRaw[]) {
    // (this.histStatObj == null) ? null : this.histStatObj[0].histValues, 
    // (this.histStatObj == null) ? null :this.histStatObj[1].histStat
    // if (!(Array.isArray(histValues) && histValues.length > 0 && Array.isArray(histStat) && histStat.length > 0)){
    //   return true
    // }

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

        // create chartDataArray = UiBrAccChrtData[]
        // for on histItem.histValues.histSdaCloses or histDates
        // chartDataArray.push (new UiBrAccChrtData())
      }

    let histValues = histObj[0].histValues;
    // let histStat = histObj[0].histStat;
    if (histValues == null)
      return;
    // for (const item of histValues) {
    //   let chrtItem : UiBrAccChrtHistValRaw;
    //   const existingUiChrtCols = brAccChrtData1.filter(
    //     (r) => r.assetId === item.assetId );
    //   if (existingUiChrtCols.length === 0) {
    //       // console.warn(`Received ticker '${item.sqTicker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
    //       // uiCol = new UiMktBarItem(stockNonRt.sqTicker, stockNonRt.ticker, false);
    //       chrtItem = new UiBrAccChrtHistValRaw();
    //       chrtItem.assetId = item.assetId;
    //       chrtItem.histDates = item.histDates;
    //       chrtItem.histSdaCloses = item.histSdaCloses;
    //       brAccChrtData1.push(chrtItem);
    //     } else if (existingUiChrtCols.length === 1) {
    //       chrtItem = existingUiChrtCols[0];
    //     } else {
    //       // console.warn(
    //       //   `Received ticker '${ciItem.assetId}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`,
    //       //   "background: #222; color: red"
    //       // );
    //       chrtItem = existingUiChrtCols[0];
    //     }

        
      
    // }
    // for (const stItem of histStat ) {
    //   const existingUiChrtCols = brAccChrtData1.filter(
    //     (r) => r.assetId === item.assetId );
    //   if (existingUiChrtCols.length === 0) {
    //     console.warn(
    //       `Received assetId '${stItem.assetId}' is not found in UiArray.`
    //     );
    //     break;
    //   }
    //   chrtItem = existingUiChrtCols[1];
    //   chrtItem.periodStartDate = stItem.periodStartDate;
    //   chrtItem.periodEndDate = stItem.periodEndDate;
    //   chrtItem.periodStart = stItem.periodStart;
    //   chrtItem.periodEnd = stItem.periodEnd;
    //   chrtItem.periodHigh = stItem.periodHigh;
    //   chrtItem.periodLow = stItem.periodLow;
    //   chrtItem.periodMaxDD = stItem.periodMaxDD;
    //   chrtItem.periodMaxDU = stItem.periodMaxDU;


    //}

  }
    
}
