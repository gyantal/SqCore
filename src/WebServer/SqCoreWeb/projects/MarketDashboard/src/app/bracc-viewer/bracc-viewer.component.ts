import { Component, OnInit, Input} from '@angular/core';
// Importing d3 library
import * as d3 from 'd3';
import * as d3Scale from 'd3';
import * as d3Shape from 'd3';
import * as d3Array from 'd3';
import * as d3Axis from 'd3';

class UiMktBarItem {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public symbol = '';
  public pctChg = 0.01;
}

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
  mktBrUi: UiMktBarItem[] = []; 
  BrAccChrt: UiBrAccChrt[] = [];  // rename BrAccChrt
  
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
  // private pageX: any;
  // private pageY: any;


  
  constructor() {
    this.mktBrUi = [
      {assetId:1, sqTicker:"S/QQQ",symbol:"QQQ",pctChg:0.001},
      {assetId:2, sqTicker:"S/SPY",symbol:"SPY",pctChg:-0.00134},
      {assetId:3, sqTicker:"S/TLT",symbol:"TLT",pctChg:0.001},
      {assetId:4, sqTicker:"S/VXX",symbol:"VXX",pctChg:0.001},
    ];

    // Creating a line chart dummy data
    this.BrAccChrt = [
      {assetId:1,dt:"2010-01-01",brNAV:310.45,SPY:290},
      {assetId:2,dt:"2010-01-02",brNAV:320.45,SPY:300},
      {assetId:3,dt:"2010-01-03",brNAV:330.45,SPY:310},
      {assetId:4,dt:"2010-01-04",brNAV:320.45,SPY:320},
    ];

    this.width = 960 - this.margin.left - this.margin.right;
    this.height = 500 - this.margin.top - this.margin.bottom;

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
    this.svg = d3.select('svg')
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
      .call(d3Axis.axisBottom(this.x));
    // Configure the Y Axis
    this.svg.append('g')
      .attr('class', 'axis axis--y')
      .call(d3Axis.axisLeft(this.y));
  }

  private drawLineAndPath() {
    this.line = d3Shape.line()
      .x( (d: any) => this.x(d.dt))
      .y( (d: any) => this.y(d.brNAV))
      // .y( (d: any) => this.y(d.brNAV))
      .curve(d3.curveMonotoneX);
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
      case 'BrAccViewer.X':  // this is the most frequent case. Should come first.
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
        return true;
      case 'BrAccViewer.Handshake':  // this is the least frequent case. Should come last.
        console.log('BrAccViewer.Handshake:' + msgObjStr);
        this.handshakeMsgStr = msgObjStr;
        const jsonObjHandshake = JSON.parse(msgObjStr);
        console.log(`BrAccViewer.Handshake.SelectableBrAccs: '${jsonObjHandshake.selectableBrAccs}'`);
        this.updateUiSelectableNavs(jsonObjHandshake.selectableNavAssets);
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

}
