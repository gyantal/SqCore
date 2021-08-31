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

class AssetLastCloseJs {
  public assetId = NaN;
  public date = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
  public lastClose = NaN;
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

  public lastClose  = NaN;
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
  public lastClose = NaN;
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
  public initialMarginReq = NaN;
  public mainMarginReq = NaN;
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
  public mainMarginReq = NaN;
  public poss = [];
}


// class UiAssetSnapPossPos {
//   public assetId = NaN;
//   public sqTicker = '';
//   public symbol = '';
//   public pos = NaN;
//   public avgCost = NaN;
//   public lastClose = NaN;
//   public lastCloseStr = '';
//   public estPrice = NaN;
//   public estUndPrice = NaN;
//   public accId = ''
// }


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
  mktBrLstClsObj: Nullable<AssetLastCloseJs[]> = null;

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
        BrAccViewerComponent.updateSnapshotTable(this.brAccountSnapshotObj, this.uiSnapTab, this.uiSnapPos) 
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

  //  tableHeaderClicked() {
  //    console.log('table header click');
  //  }

  // openTab() {
  // }
//   openTab (pEvent: any, tabName:any) {
//     console.log('tab are  clicked');
//    if(tabName === "") {return;}
//     var i, tabcontent, tablinks;
//     tabcontent = document.getElementsByClassName("tabcontent");
//     for (i = 0; i < tabcontent.length; i++){
//         tabcontent[i].style.display = "none";
//     }
//     tablinks = document.getElementsByClassName("tablinks");
//     for (i = 0; i < tablinks.length; i++){
//         tablinks[i].className = tablinks.className.replace(" active", "");
//     }
//     let xxx = document.getElementById(tabName);
//     if (xxx == null)
//       return;
//     xxx.style.display = "block";
//     pEvent.currentTarget.className += " active";
// }

 
  updateUiWithSnapshot(jsonObjSnap: any)  {
    console.log(`BrAccViewer.updateUiWithSnapshot(). Symbol: '${jsonObjSnap.symbol}'`);
    if (this.selectedNav != jsonObjSnap.symbol) // change UI only if it is a meaningful change
      this.selectedNav = jsonObjSnap.symbol;
  }

  static updateMktBarUi(marketBarAssets: Nullable<AssetJs[]>, lastCloses: Nullable<AssetLastCloseJs[]>, lastRt: Nullable<AssetLastJs[]>, uiMktBar: UiMktBarItem[]) {
     // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
     if (!(Array.isArray(marketBarAssets) && marketBarAssets.length > 0 && Array.isArray(lastCloses) && lastCloses.length > 0  && Array.isArray(lastRt) && lastRt.length > 0)) {
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
    for (const nonRt of lastCloses) {
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
      uiItem.lastClose = nonRt.lastClose;
    }
    // Step 3: use real-time data (we have to temporary generate it)
    for (const rtItem of lastRt) {
      const existingUiItems = uiMktBar.filter(
        (r) => r.assetId === rtItem.assetId
      );
      if (existingUiItems.length === 0)
        continue;
      
      const uiItem = existingUiItems[0];
      uiItem.pctChg = (rtItem.last - uiItem.lastClose) / uiItem.lastClose;
      
    }
    

  }

  static updateSnapshotTable(brAccSnap : Nullable<AssetSnapPossJs>, uiSnapTab : UiSnapTable, uiSnapPos: AssetSnapPossPosJs[])
  {
    if (brAccSnap === null || brAccSnap.poss === null) 
      return;

      uiSnapTab.symbol = brAccSnap.symbol;
      uiSnapTab.lastUpdate = brAccSnap.lastUpdate;
      uiSnapTab.netLiquidation = brAccSnap.netLiquidation;
      uiSnapTab.netLiquidationStr = brAccSnap.netLiquidation.toString();

    // let snapShotItems = brAccSnap['']
    for (const uiSnapItem of brAccSnap.poss) {
      

      console.log("The Positions of Snapshot data are :" + uiSnapItem.pos);
      // if (existingUiSanpItems.length === 0)
      //   continue;
      let possItem = new AssetSnapPossPosJs();
      possItem.assetId = uiSnapItem.assetId;
      possItem.sqTicker = uiSnapItem.sqTicker;
      possItem.symbol = uiSnapItem.symbol;
      possItem.pos = uiSnapItem.pos;
      possItem.avgCost = uiSnapItem.avgCost;
      possItem.lastClose = uiSnapItem.lastClose;
      possItem.estPrice = uiSnapItem.estPrice;
      possItem.estUndPrice = uiSnapItem.estUndPrice;
      possItem.accId = uiSnapItem.accId;
      uiSnapPos.push(possItem);

    }
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
