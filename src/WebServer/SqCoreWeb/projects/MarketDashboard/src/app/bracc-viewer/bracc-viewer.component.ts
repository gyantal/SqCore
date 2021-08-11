import { Component, OnInit, Input} from '@angular/core';
// Importing d3 library
import * as d3 from 'd3';
import * as d3Scale from 'd3';
import * as d3Shape from 'd3';
import * as d3Array from 'd3';
import * as d3Axis from 'd3';
import { SqNgCommonUtilsTime } from '../../../../sq-ng-common/src/lib/sq-ng-common.utils_time';

type Nullable<T> = T | null;


class AssetJs {
  public assetId = NaN;
  public sqTicker = '';
  public symbol = '';
  public name = '';
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

class UiMktBarItem_old {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public ticker = '';
  public pctChg = 0.01;

   // NonRt stats directly from server
   public periodStartDate = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
   public periodEndDate = '';
   public periodStart = NaN;
   public periodEnd = NaN;
   public periodHigh = NaN;
   public periodLow = NaN;
   public periodMaxDD = NaN;
   public periodMaxDU = NaN;

  // Rt stats directly from server
  public last  = NaN; // last means real-time usually. If there is no real-time price, then the last known.
  public lastUtc = ''; 

  // calculated fields as numbers
  public periodReturn = NaN; // for period: from startDate to endDate
  public periodMaxDrawDown = NaN; // for period: from startDate to endDate
  public rtReturn = NaN;  // comparing last (rt) price to periodEnd price.
  public return = NaN;  // Total return (from startDate to endDate to last realtime): adding period-return and realtime-return together. Every other performance number (cagr, maxDD) is also Total.
  // public cagr = NaN;
  // public drawDown = NaN;
  // public drawUp = NaN;
  // public maxDrawDown = NaN;
  // public maxDrawUp = NaN;

  // fields for the first row
  public rtReturnStr = '';
  public rtReturnSign = 1;
  public returnStr = '';
  public rtReturnClass = '';
  public rtTooltipStr1 = '';
  public rtTooltipStr2 = '';

  public selectedPerfIndSign = 1; // return or cagr or maxDD
  public selectedPerfIndClass = ''; // positive or negative
  public selectedPerfIndStr = ''; // the value of the cell in the table
  public mainTooltipStr1 = '';
  public mainTooltipStr2 = '';
  public mainTooltipStr3 = '';
}
// UiBrAccChrt is for developing the chart
class UiBrAccChrt {
  public assetId = NaN;
  public dt ='';
  public brNAV = 0.01;
  public SPY = 0.01;

}
@Component({
  selector: 'app-bracc-viewer',
  templateUrl: './bracc-viewer.component.html',
  styleUrls: ['./bracc-viewer.component.scss']
})
export class BrAccViewerComponent implements OnInit {
  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  selectedNav = '';
  uiMktBar: UiMktBarItem[] = [];
  BrAccChrt: UiBrAccChrt[] = [];  // rename BrAccChrt

  handShkMsg: Nullable<BrAccVwrHandShk> = null;

  lastRtMsg: Nullable<RtMktSumRtStat[]> = null;
  lastNonRtMsg: Nullable<RtMktSumNonRtStat[]> = null;
  
  handshakeMsgStr = '[Nothing arrived yet]';
  mktBrLstClsStr = '[Nothing arrived yet]';
  brAccountSnapshotStr = '[Nothing arrived yet]';
  histStr = '[Nothing arrived yet]';
  // required for chart
  private margin = {top: 20, right:20, bottom: 30, left: 50};
  private width: number;
  private height: number;
  private x: any;
  private y: any;
  private svg: any;
  public tooltip: any;
  private line!: d3Shape.Line<[number, number]>;
  sqTicker: any;
  ticker: any;
  isNavColumn: any;
  // private pageX: any;
  // private pageY: any;

  constructor() {
    
    // this.sqTicker = sqTckr;
    // this.ticker = tckr;
    // this.isNavColumn = isNavCol;
    // if (!isNavCol) {
    //   this.referenceUrl = 'https://uk.tradingview.com/chart/?symbol=' + tckr;
    // }
    // this.uiMktBar = [
    //   {assetId:1, sqTicker:"S/QQQ",symbol:"QQQ",pctChg:0.001,last:12,lastUtc:'211'},
    //   {assetId:2, sqTicker:"S/SPY",symbol:"SPY",pctChg:-0.00134,last:12,lastUtc:'211'},
    //   {assetId:3, sqTicker:"S/TLT",symbol:"TLT",pctChg:0.001,last:12,lastUtc:'211'},
    //   {assetId:4, sqTicker:"S/VXX",symbol:"VXX",pctChg:0.001,last:12,lastUtc:'211'},
    // ];

    // Creating a line chart dummy data
    this.BrAccChrt = [
      {assetId:1,dt:"2010-01-01",brNAV:310.45,SPY:290},
      {assetId:2,dt:"2010-01-02",brNAV:320.45,SPY:300},
      {assetId:3,dt:"2010-01-03",brNAV:330.45,SPY:310},
      {assetId:4,dt:"2010-01-04",brNAV:320.45,SPY:320},
    ];

    this.width = 960 - this.margin.left - this.margin.right;
    this.height = 500 - this.margin.top - this.margin.bottom;

    // this.sqTicker = sqTckr;
    // this.ticker = tckr;
    // this.isNavColumn = isNavCol;
    // if (!isNavCol) {
    //   this.referenceUrl = 'https://uk.tradingview.com/chart/?symbol=' + tckr;
    // }

   }

  ngOnInit(): void {
  
    // item1 = new UiMktBarItem(1, "S/SPY", "SPY", 0.01);
    // functions for developing charts
    this.buildSvg();
    this.addXandYAxis();
    this.drawLineAndPath();
  }
// Chart functions start
  private buildSvg() {
    this.svg = d3.select('svg#chartNav')
      .attr('width',900 )
      .attr('height',500)
      .append('g')
      .attr('transform', 'translate(' + this.margin.left + ',' + this.margin.top + ')');
    
    this.tooltip = d3.select("body")
    .append('div')
    .classed("chart-tooltip", true)
    .style("display","none")
  }

  private addXandYAxis() {
    // range of data configuring
    this.x = d3Scale.scaleTime().range([0, this.width]);
    this.y = d3Scale.scaleLinear().range([this.height,0]);
    this.x.domain(d3Array.extent(this.BrAccChrt, (d: { dt: any; }) => d.dt ));
    this.y.domain(d3Array.extent(this.BrAccChrt, (d: { brNAV: any; }) => d.brNAV ));
    // this.y.domain(d3Array.extent(this.data, (d: { SPY: any; }) => d.SPY ));
    // Configure the X axis
    this.svg.append('g')
      .attr('transform', 'translate(0,' + this.height + ')')
      .call(d3Axis.axisBottom(this.x).ticks(10))
      // .tickFormat(this.x = > (${this.x.toFixed(1)}));

      

    // text label for x-axis
    this.svg.append("text")
      .attr("x", this.width/2)
      .attr("y", this.height + this.margin.bottom)
      
      .style("text-anchor","middle")
      .text("Date");

    // Configure the Y Axis
    this.svg.append('g')
      .attr('class', 'axis axis--y')
      .call(d3Axis.axisLeft(this.y).ticks(10,"$"));

     // text label for y-axis
    this.svg.append("text")
      .attr("transform", "rotate(-90)")
      .attr("y", 0-this.margin.left)
      .attr("x", 0-(this.height/2))
      .attr("dy","1em")
      .style("text-anchor", "middle")
      .text("brNAV");
  }

  private drawLineAndPath() {
    this.line = d3Shape.line()
      .x( (d: any) => this.x(d.dt))
      .y( (d: any) => this.y(d.brNAV))
      // .y( (d: any) => this.y(d.brNAV))
      // .curve(d3.curveMonotoneX);
    // Configuring line path
    // Append the path, bind the data, and call the line generator
    this.svg.append('path')
    .datum(this.BrAccChrt) // Binds data to the line
    .attr('class', 'line') //Assign a class for styling
    .attr('d', this.line); // Calls the line generator

// Appends a circle for each datapoint
    this.svg.selectAll(".dot")
    .data(this.BrAccChrt)
    .enter().append("circle")
    .attr("class", "dot")
  }
  // Chart functions end

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
        // if (this.handShkMsg != null)
        //   BrAccViewerComponent.updateMktBarUi(this.handShkMsg.marketBarAssets, null, null, this.uiMktBar);

        BrAccViewerComponent.updateMktBarUi((this.handShkMsg == null) ? null : this.handShkMsg.marketBarAssets, null, null, this.uiMktBar);
        return true;
      case 'BrAccViewer.BrAccSnapshot':
        console.log('BrAccViewer.BrAccSnapshot:' + msgObjStr);
        this.brAccountSnapshotStr = msgObjStr;
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
        // if message is too large without spaces, we have problems as there is no horizontal scrollbar in browser. So, shorten the message.
        if (msgObjStr.length < 200)
          this.histStr = msgObjStr;
        else
          this.histStr = msgObjStr.substring(0, 200) + '... [more data arrived]';
        return true;
      case 'BrAccViewer.MktBrLstCls':
        console.log('BrAccViewer.MktBrLstCls:' + msgObjStr);
        this.mktBrLstClsStr = msgObjStr;
        // TODO: create a class for this, but convert string to json object and send it into updateMktBarUi
        //this.mktBrLstClsMsg = JSON.parse(msgObjStr);
        // BrAccViewerComponent.updateMktBarUi(this.handShkMsg.marketBarAssets, null, null, this.uiMktBar);
        BrAccViewerComponent.updateMktBarUi((this.handShkMsg == null) ? null : this.handShkMsg.marketBarAssets, null, null, this.uiMktBar);
        return true;
      case 'BrAccViewer.Handshake':  // this is the least frequent case. Should come last.
        console.log('BrAccViewer.Handshake:' + msgObjStr);
        this.handshakeMsgStr = msgObjStr;
        this.handShkMsg = JSON.parse(msgObjStr);
        console.log(`BrAccViewer.Handshake.SelectableBrAccs: '${(this.handShkMsg == null) ? null : this.handShkMsg.selectableNavAssets}'`);
        this.updateUiSelectableNavs((this.handShkMsg == null) ? null : this.handShkMsg.selectableNavAssets);
        return true;
      default:
        return false;
    }
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

  updateUiWithSnapshot(jsonObjSnap: any)  {
    console.log(`BrAccViewer.updateUiWithSnapshot(). Symbol: '${jsonObjSnap.symbol}'`);
    if (this.selectedNav != jsonObjSnap.symbol) // change UI only if it is a meaningful change
      this.selectedNav = jsonObjSnap.symbol;
  }

  static updateMktBarUi(marketBarAssets: Nullable<AssetJs[]>, lastCloses: Nullable<RtMktSumNonRtStat[]>, lastRt: Nullable<RtMktSumRtStat[]>, uiMktBar: UiMktBarItem[]) {
     // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
     if (!(Array.isArray(marketBarAssets) && marketBarAssets.length > 0 && Array.isArray(lastRt) && lastRt.length > 0 && Array.isArray(lastCloses) && lastCloses.length > 0)) {
      return;
    }
    
    // uiMktBar is visualized in HTML
    // Step 1.

    // for (const uiCol of uiMktBar) {
    //   uiCol.lastClose = NaN;
    //   uiCol.last = 500;
     
    // }
    for (const item of marketBarAssets ){
      let uiCol: UiMktBarItem;
      const existingUiCols = uiMktBar.filter(col => col.sqTicker === item.sqTicker);
      if (existingUiCols.length === 0) {
        console.warn(`Received ticker '${item.sqTicker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
        // uiCol = new UiMktBarItem(stockNonRt.sqTicker, stockNonRt.ticker, false);
        uiCol = new UiMktBarItem();
        uiMktBar.push(uiCol);
      } else if (existingUiCols.length === 1) {
        uiCol = existingUiCols[0];
      } else {
        console.warn(`Received ticker '${item.sqTicker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`, 'background: #222; color: red');
        uiCol = existingUiCols[0];
      }

      uiCol.assetId = item.assetId;
      uiCol.sqTicker = item.sqTicker;
      uiCol.symbol = item.symbol;
      uiCol.name  = item.name;
    // write a code here that goes through marketBarAssets array and fill up uiMktBar.Symbol
    // So, this will be visualized in HTML
    // ignore LastCloses, and RealTime prices at the moment.
    // ...

    // Step 2: use LastCloses data, and write it into uiMktBar array.
    // Step 3: use real-time data (we have to temporary generate it)
    }
  }

  static updateMarketBarUi_old(lastRt: Nullable<RtMktSumRtStat[]>, lastNonRt: Nullable<RtMktSumNonRtStat[]>, lookbackStartDateET: Date, uiColumns: UiMktBarItem_old[]) {
    // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
    if (!(Array.isArray(lastRt) && lastRt.length > 0 && Array.isArray(lastNonRt) && lastNonRt.length > 0)) {
      return;
    }

    // purge out uiCol.last, because user can change between NAVs, and it can happen that the Nav column had a real-time last price for one broker, but not after changing the broker. That case last price is not sent for that asset.
    for (const uiCol of uiColumns) {
      uiCol.last = NaN;
      uiCol.lastUtc = '';
    }

    // we have to prepare if the server sends another ticker that is not expected. In that rare case, the append it to the end of the array. That will cause UI blink. And Warn about it, so this can be fixed.
    // start with the NonRt, because that gives the AssetID to ticker definitions.
    for (const stockNonRt of lastNonRt) {
      let uiCol: UiMktBarItem_old;
      const existingUiCols = uiColumns.filter(col => col.ticker === stockNonRt.ticker);
      if (existingUiCols.length === 0) {
        console.warn(`Received ticker '${stockNonRt.ticker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
        // uiCol = new UiMktBarItem(stockNonRt.sqTicker, stockNonRt.ticker, false);
        uiCol = new UiMktBarItem_old();
        uiColumns.push(uiCol);
      } else if (existingUiCols.length === 1) {
        uiCol = existingUiCols[0];
      } else {
        console.warn(`Received ticker '${stockNonRt.ticker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`, 'background: #222; color: red');
        uiCol = existingUiCols[0];
      }

      uiCol.assetId = stockNonRt.assetId;
      // uiCol.periodStartDate = stockNonRt.periodStartDate;
      // uiCol.periodEndDate = stockNonRt.periodEndDate;
      // uiCol.periodStart = stockNonRt.periodStart;
      // uiCol.periodEnd = stockNonRt.periodEnd;
      // uiCol.periodHigh = stockNonRt.periodHigh;
      // uiCol.periodLow = stockNonRt.periodLow;
      // uiCol.periodMaxDD = stockNonRt.periodMaxDD;
      // uiCol.periodMaxDU = stockNonRt.periodMaxDU;
    }

    for (const stockRt of lastRt) {
      const existingUiCols = uiColumns.filter(col => col.assetId === stockRt.assetId);
      if (existingUiCols.length === 0) {
        // it can easily happen. User changes the BrNav, but the realtime price of the previously selected BrNav is already in the websocket so it is coming here.
        console.warn(`Received assetId '${stockRt.assetId}' is not found in UiArray. Happens if user changes BrNav and old one is already in the way`, 'background: #222; color: red');
        break;
      }
      const uiCol = existingUiCols[0];
      uiCol.last = stockRt.last;
      uiCol.lastUtc = stockRt.lastUtc;
    }

    // const indicatorSelected = (document.getElementById('perfIndicator') as HTMLSelectElement).value;
    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds
    const nf = new Intl.NumberFormat();  // for thousands commas(,) https://stackoverflow.com/questions/2901102/how-to-print-a-number-with-commas-as-thousands-separators-in-javascript

    for (const uiCol of uiColumns) {
      // preparing values
      // uiCol.periodReturn = uiCol.periodEnd / uiCol.periodStart - 1;
      // uiCol.periodMaxDrawDown = uiCol.periodMaxDD;
      uiCol.rtReturn = uiCol.last > 0 ? uiCol.last / uiCol.periodEnd - 1 : 0;
      uiCol.return = uiCol.last > 0 ? uiCol.last / uiCol.periodStart - 1 : uiCol.periodEnd / uiCol.periodStart - 1;
      // const dataStartDateET = new Date(uiCol.periodStartDate);  // '2010-09-29T00:00:00' which was UTC is converted to DateObj interpreted in Local time zone {Tue Sept 29 2010 00:00:00 GMT+0000 (Greenwich Mean Time)}
      // const nDays = SqNgCommonUtilsTime.DateDiffNdays(dataStartDateET, todayET); // 2 weeks = 14 days, 2020 year: 366 days, because it is a leap year.
      // const nYears = nDays / 365.25; // exact number of days in a year in average 365.25 days, because it is 3 times 365 and 1 time 366
      // uiCol.cagr = Math.pow(1 + uiCol.return, 1.0 / nYears) - 1;
      // uiCol.drawDown = uiCol.last > 0 ? uiCol.last / Math.max(uiCol.periodHigh, uiCol.last) - 1 : uiCol.periodEnd / uiCol.periodHigh - 1;
      // uiCol.drawUp = uiCol.last > 0 ? uiCol.last / Math.min(uiCol.periodLow, uiCol.last) - 1 : uiCol.periodEnd / uiCol.periodLow - 1;
      // uiCol.maxDrawDown = Math.min(uiCol.periodMaxDD, uiCol.drawDown);
      // uiCol.maxDrawUp = Math.max(uiCol.periodMaxDU, uiCol.drawUp);

      // // filling first row in table
      uiCol.rtReturnStr = (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';
      uiCol.rtReturnSign = Math.sign(uiCol.rtReturn);
      uiCol.rtReturnClass = (uiCol.rtReturn >= 0 ? 'positivePerf' : 'negativePerf');
      uiCol.rtTooltipStr1 = uiCol.ticker;
      uiCol.rtTooltipStr2 = 'Last value: ' + nf.format(uiCol.last) + ' (at ' + uiCol.lastUtc + ')\n' + 'Rt return: ' + (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';
      // //uiCol.rtTooltipStr2 = 'Period end price: ' + nf.format(uiCol.periodEnd) + '\n'  + 'Last price: ' + uiCol.last.toFixed(2).toString() + '\n' + 'Rt return: ' + (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';

      // // filling second row in table. Tooltip contains all indicators (return, DD, DU, maxDD, maxDU), so we have to compute them
      // // const dataStartDate: Date = new Date(uiCol.periodStart);
      // https://stackoverflow.com/questions/17545708/parse-date-without-timezone-javascript
      // Javascript Date object are timestamps - they merely contain a number of milliseconds since the epoch. There is no timezone info in a Date object
      // uiCol.periodStart = '2010-09-29T00:00:00' comes as a string
      // dataStartDate.toISOString() would convert { Tue Dec 31 2019 00:00:00 GMT+0000 (GMT) } to "2015-09-28T23:00:00.000Z", so we better use the received string from server.
      // const dataStartDateETStr = uiCol.periodStartDate.slice(0, 10); // use what is coming from server '2010-09-29T00:00:00'
      // const dataEndDateETStr = uiCol.periodEndDate.slice(0, 10); // use what is coming from server '2010-09-29T00:00:00'
      // uiCol.periodReturnStr = (uiCol.periodReturn >= 0 ? '+' : '') + (uiCol.periodReturn * 100).toFixed(2).toString() + '%';
      // uiCol.periodMaxDrawDownStr = (uiCol.periodMaxDrawDown >= 0 ? '+' : '') + (uiCol.periodMaxDrawDown * 100).toFixed(2).toString() + '%';
      uiCol.returnStr = (uiCol.return >= 0 ? '+' : '') + (uiCol.return * 100).toFixed(2).toString() + '%';
      // uiCol.cagrStr = (uiCol.cagr >= 0 ? '+' : '') + (uiCol.cagr * 100).toFixed(2).toString() + '%';
      // uiCol.drawDownStr = (uiCol.drawDown >= 0 ? '+' : '') + (uiCol.drawDown * 100).toFixed(2).toString() + '%';
      // uiCol.drawUpStr = (uiCol.drawUp >= 0 ? '+' : '') + (uiCol.drawUp * 100).toFixed(2).toString() + '%';
      // uiCol.maxDrawDownStr = (uiCol.maxDrawDown >= 0 ? '+' : '') + (uiCol.maxDrawDown * 100).toFixed(2).toString() + '%';
      // uiCol.maxDrawUpStr = (uiCol.maxDrawUp >= 0 ? '+' : '') + (uiCol.maxDrawUp * 100).toFixed(2).toString() + '%';

      uiCol.mainTooltipStr1 = uiCol.ticker;
      uiCol.mainTooltipStr2 = 'Period+Rt return: ${uiCol.returnStr}'
      // uiCol.mainTooltipStr2 = `Period only return: ${uiCol.periodReturnStr}\nPeriod only maxDD: ${uiCol.periodMaxDrawDownStr}\n\nPeriod+Rt return: ${uiCol.returnStr}\nTotal CAGR: ${uiCol.cagrStr}\nCurrent drawdown: ${uiCol.drawDownStr}\nCurrent drawup: ${uiCol.drawUpStr}\nMaximum drawdown: ${uiCol.maxDrawDownStr}\nMaximum drawup: ${uiCol.maxDrawUpStr}`;
      // uiCol.mainTooltipStr3 = `\nPeriod [${dataStartDateETStr}...${dataEndDateETStr}]:\nStart: ${nf.format(uiCol.periodStart)}\nEnd: ${nf.format(uiCol.periodEnd)}\nHigh: ${nf.format(uiCol.periodHigh)}\nLow: ${nf.format(uiCol.periodLow)}\n*All returns are TWR, coz of adjustments.`;
      // uiCol.lookbackErrorStr = (dataStartDateET > lookbackStartDateET) ? `! Period data starts on ${dataStartDateETStr}\n instead of the expected ${lookbackStartDateET.toISOString().slice(0, 10)}.\n\n` : '';
      // uiCol.lookbackErrorClass = (dataStartDateET > lookbackStartDateET) ? 'lookbackError' : '';

      // BrAccViewerComponent.updateUiColumnBasedOnSelectedIndicator(uiCol, indicatorSelected);
      uiCol.selectedPerfIndSign = Math.sign(uiCol.return);
      uiCol.selectedPerfIndClass = (uiCol.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
      uiCol.selectedPerfIndStr = uiCol.returnStr;
    } // for
    
  }


  
}
