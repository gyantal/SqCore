import './../css/main.css';
import * as d3 from 'd3';
import { sqLineChartGenerator } from '../../../../TsLib/sq-common/sqlineChrt';
// export {}; // TS convention: To avoid top level duplicate variables, functions. This file should be treated as a module (and have its own scope). A file without any top-level import or export declarations is treated as a script whose contents are available in the global scope.

// 1. Declare some global variables and hook on DOMContentLoaded() and window.onload()
console.log('SqCore: Script BEGIN');


async function AsyncStartDownloadAndExecuteCbLater(url: string, callback: (json: any) => any) {
  fetch(url)
      .then((response) => { // asynch long running task finishes. Resolves to get the Response object (http header, info), but not the full body (that might be streaming and arriving later)
        console.log('SqCore.AsyncStartDownloadAndExecuteCbLater(): Response object arrived:');
        if (!response.ok)
          return Promise.reject(new Error('Invalid response status'));
        response.json().then((json) => { // asynch long running task finishes. Resolves to the body, converted to json() object or text()
        // const jsonToStr = JSON.stringify(json).substr(0, 60) + '...';
        // console.log('SqCore.AsyncStartDownloadAndExecuteCbLater():: data body arrived:' + jsonToStr);
          callback(json);
        });
      })
      .catch((err) => {
        console.log('SqCore: Download error.');
      });
}

function getDocElementById(id: string): HTMLElement {
  return (document.getElementById(id) as HTMLElement); // type casting assures it is not null for the TS compiler. (it can be null during runtime)
}


document.addEventListener('DOMContentLoaded', (event) => {
  console.log('DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.');
});

window.onload = function onLoadWindow() {
  console.log('SqCore: window.onload() BEGIN. All CSS, and images were downloaded.'); // images are loaded at this time, so their sizes are known

  AsyncStartDownloadAndExecuteCbLater('/StrategyRenewedUber', (json: any) => {
    onReceiveData(json);
  });

  console.log('SqCore: window.onload() END.');
};

function onReceiveData(json: any) {
  if (json == 'Error') {
    const divErrorCont = getDocElementById('idErrorCont');
    divErrorCont.innerHTML = 'Error during downloading data. Please, try again later!';
    getDocElementById('errorMessage').style.visibility='visible';
    getDocElementById('pctChgCharts').style.visibility = 'hidden';
    getDocElementById('vixChrt').style.visibility = 'hidden';

    return;
  }
  getDocElementById('titleCont').innerHTML = '<small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small>';
  getDocElementById('requestTime').innerText = json.requestTime;
  getDocElementById('lastDataTime').innerText = json.lastDataTime;
  getDocElementById('currentPV').innerHTML = 'Current PV: <span class="pv">$' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank" >these current positions</a> updated on ' + json.currentPVDate + ')';
  if (json.dailyProfSig !== 'N/A')
    getDocElementById('dailyProfit').innerHTML = '<b>Daily Profit/Loss: <span class="' + json.dailyProfString + '">' + json.dailyProfSig + json.dailyProfAbs + '</span></b>';
  getDocElementById('idCurrentEvent').innerHTML = 'Next trading day will be <span class="stci"> ' + json.currentEventName + '</span>, <div class="tooltip">used STCI is <span class="stci">' + json.currentSTCI + '</span><span class="tooltiptext">Second (third) month VIX futures divided by front (second) month VIX futures minus 1, with more (less) than 5-days until expiration.</span></div > and used VIX is <span class="stci">' + json.currentVIX + '</span>, thus leverage will be <span class="stci">' + json.currentFinalWeightMultiplier + '.</span >';
  getDocElementById('idPosFut').innerHTML = '<a href=' + json.gSheetRef + '" target="_blank" >Upcoming Events</a>';
  getDocElementById('idVixCont').innerHTML = 'VIX Futures Term Structure';
  getDocElementById('idRules').innerHTML = '<u>Current trading rules:</u> <ul align="left"><li>Play with 100% of PV on FOMC days (as this is the strongest part of the strategy), with 85% on Holiday days, with 70% on VIXFUTEX, OPEX, TotM and TotMM days, while with only 50% of PV on pure bullish STCI days. These deleveraging percentages have to be played both on bullish and bearish days.</li><li><ul><li>All of the FOMC and Holiday signals have to be played, regardless the STCI;</li><li>on weaker bullish days (OPEX, VIXFUTEX, TotM and TotMM) play the UberMix basket if and only if the STCI closed above +2% contango (25th percentile) on previous day (so, stay in cash if contango is not big enough);</li><li>on weaker bearish days (OPEX, VIXFUTEX, TotM and TotMM) play long VXX if and only if the STCI closed below +9% contango (75th percentile) on previous day (so, stay in cash if the contango is too deep).</li></ul></li><li>Bullish STCI threshold on non-event days is +7.5%, which is the 67th percentile of historical value of the STCI.</li><li>VIX Based Leverage Indicator: <ul align="left"><li>If VIX<21:&emsp; leverage = 100%;</li><li>If 21<=VIX<30:&emsp; leverage = 100%-(VIX-21)*10%;</li><li>If 30<=VIX:&emsp; leverage = 10%.</li></ul></li></ul>';

  renewedUberInfoTbls(json);
  // Setting charts visible after getting data.
  getDocElementById('pctChgCharts').style.visibility = 'visible';
  getDocElementById('vixChrt').style.visibility = 'visible';
}

function renewedUberInfoTbls(json) {
// Creating JavaScript data arrays by splitting.
  const assetNames2Array = json.assetNames2.split(', ');
  const currPosNumArray = json.currPosNum.split(', ');
  const currPosValArray = json.currPosVal.split(', ');
  const nextPosNumArray = json.nextPosNum.split(', ');
  const nextPosValArray = json.nextPosVal.split(', ');
  const diffPosNumArray = json.posNumDiff.split(', ');
  const diffPosValArray = json.posValDiff.split(', ');
  // const prevEventNames = json.prevEventNames.split(', ');
  const prevEventColors = json.prevEventColors.split(', ');
  const nextEventColors = json.nextEventColors.split(', ');

  const prevPosMtxTemp = json.pastDataMtxToJS.split('ß ');
  const prevPosMtx: any[] = [];
  for (let i = 0; i < prevPosMtxTemp.length; i++)
    prevPosMtx[i] = prevPosMtxTemp[i].split(',');


  const nextPosMtxTemp = json.nextDataMtxToJS.split('ß ');
  const nextPosMtx: any[] = [];
  for (let i = 0; i < nextPosMtxTemp.length; i++)
    nextPosMtx[i] = nextPosMtxTemp[i].split(',');


  const assChartMtxTemp = json.assetChangesToChartMtx.split('ß ');
  const assChartMtx: any[] = [];
  for (let i = 0; i < assChartMtxTemp.length; i++)
    assChartMtx[i] = assChartMtxTemp[i].split(',');

  // Creating the HTML code of tables.
  let chngInPosTbl = '<table class="currData"><tr align="center"><td bgcolor="#66CCFF"></td>';
  for (let i = 0; i < assetNames2Array.length - 1; i++)
    chngInPosTbl += '<td bgcolor="#66CCFF"><a href=https://finance.yahoo.com/quote/' + assetNames2Array[i] + ' target="_blank">' + assetNames2Array[i] + '</a></td>';

  chngInPosTbl += '<td bgcolor="#66CCFF">' + assetNames2Array[assetNames2Array.length - 1] + '</td>';

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">Tomorrow target:<br>' + json.nextTradingDay + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[0] + '">' + nextPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[0] + '">' + nextPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">Last rebalance:<br>' + json.currPosDate + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[1] + '">' + currPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[1] + '">' + currPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">Change in Positions</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFF00">' + diffPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFF00">' + diffPosNumArray[i] + '</td>';

  chngInPosTbl += '</tr></table>';

  let upcomingEventsTbl = '<table class="currData"><tr align="center"  bgcolor="#66CCFF"><td rowspan="3">Date</td><td colspan="8">Events</td><td rowspan="3">Most Significant Event</td><td rowspan="3">M.S. Event Signal</td><td rowspan="3">M.S. Event Leverage</td></tr><tr align="center" bgcolor="#66CCFF"><td colspan="2">Event 1</td><td colspan="2">Event 2</td><td colspan="2">Event 3</td><td colspan="2">Event 4</td></tr><tr align="center" bgcolor="#66CCFF"><td>Name</td><td>Signal</td><td>Name</td><td>Signal</td><td>Name</td><td>Signal</td><td>Name</td><td>Signal</td></tr><tr align="center">';
  for (let i = 0; i < nextPosMtxTemp.length; i++) {
    for (let j = 0; j < 12; j++)
      upcomingEventsTbl += '<td bgcolor="#' + nextEventColors[i] + '">' + nextPosMtx[i][j] + '</td>';

    upcomingEventsTbl += '</tr>';
  }
  upcomingEventsTbl += '</table>';

  const subStrategiesTbl = '<table class="currData2"><tr><td bgcolor="#32CD32">FOMC Bullish Day</td><td bgcolor="#7CFC00">Holiday Bullish Day</td><td bgcolor="#00FA9A">Other Bullish Event Day</td></tr><tr><td bgcolor="#c24f4f">FOMC Bearish Day</td><td bgcolor="#d46a6a">Holiday Bearish Day</td><td bgcolor="#ed8c8c">Other Bearish Event Day</td></tr><tr><td bgcolor="#C0C0C0">Non-Playable Other Event Day</td><td bgcolor="#00FFFF">STCI Bullish Day</td><td bgcolor="#FFFACD">STCI Neutral Day</td></tr></table > ';

  // "Sending" data to HTML file.
  const chngInPosMtx = getDocElementById('changeInPos');
  chngInPosMtx.innerHTML = chngInPosTbl;

  const upcomingEventsMtx = getDocElementById('upcomingEvents');
  upcomingEventsMtx.innerHTML = upcomingEventsTbl;

  const subStrategiesMtx = getDocElementById('subStrategies');
  subStrategiesMtx.innerHTML = subStrategiesTbl;

  // percentage Change Chart
  const nCurrData = parseInt(json.chartLength) + 1;
  const noAssets = assetNames2Array.length - 1;
  const xLabel: string = 'Dates';
  const yLabel: string = 'Percentage Change';
  const yScaleTickFormat = '%';
  const lineChrtDiv = getDocElementById('pctChgChrt');
  const lineChrtTooltip = getDocElementById('tooltipChart');
  sqLineChartGenerator(noAssets, nCurrData, assetNames2Array, assChartMtx, xLabel, yLabel, yScaleTickFormat, lineChrtDiv, lineChrtTooltip);

  const currDataVixArray = json.currDataVixVec.split(',');
  const currDataDaysVixArray = json.currDataDaysVixVec.split(',');
  const prevDataVixArray = json.prevDataVixVec.split(',');
  const currDataDiffVixArray = json.currDataDiffVixVec.split(',');
  const currDataPercChVixArray = json.currDataPercChVixVec.split(',');
  const spotVixArray = json.spotVixVec.split(',');

  // Creating the HTML code of current table.
  let futurePricesTbl = '<table class="currData"><tr align="center"><td>Future Prices</td><td>F1</td><td>F2</td><td>F3</td><td>F4</td><td>F5</td><td>F6</td><td>F7</td><td>F8</td></tr><tr align="center"><td align="left">Current</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataVixArray[i] == 0)
      futurePricesTbl += '<td>' + '---' + '</td>';
    else
      futurePricesTbl += '<td>' + currDataVixArray[i] + '</td>';
  }

  futurePricesTbl += '</tr><tr align="center"><td align="left">Previous Close</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataVixArray[i] == 0)
      futurePricesTbl += '<td>' + '---' + '</td>';
    else
      futurePricesTbl += '<td>' + prevDataVixArray[i] + '</td>';
  }
  futurePricesTbl += '</tr><tr align="center"><td align="left">Daily Abs. Change</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataVixArray[i] == 0)
      futurePricesTbl += '<td>' + '---' + '</td>';
    else
      futurePricesTbl += '<td>' + currDataDiffVixArray[i] + '</td>';
  }
  futurePricesTbl += '</tr><tr align="center"><td align="left">Daily % Change</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataVixArray[i] == 0)
      futurePricesTbl += '<td>' + '---' + '</td>';
    else
      futurePricesTbl += '<td>' + (currDataPercChVixArray[i] * 100).toFixed(2) + '%</td>';
  }
  futurePricesTbl += '</tr><tr align="center"><td align="left">Cal. Days to Expiration</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataVixArray[i] == 0)
      futurePricesTbl += '<td>' + '---' + '</td>';
    else
      futurePricesTbl += '<td>' + currDataDaysVixArray[i] + '</td>';
  }
  futurePricesTbl += '</tr></table>';

  let contangoTbl = '<table class="currData"><tr align="center"><td>Contango</td><td>F2-F1</td><td>F3-F2</td><td>F4-F3</td><td>F5-F4</td><td>F6-F5</td><td>F7-F6</td><td>F8-F7</td><td>F7-F4</td><td>(F7-F4)/3</td></tr><tr align="center"><td align="left">Monthly Contango %</td><td><strong>' + (currDataVixArray[8] * 100).toFixed(2) + '%</strong></td>';
  for (let i = 20; i < 27; i++) {
    if (currDataVixArray[i] == 0)
      contangoTbl += '<td>' + 0 + '</td>';
    else
      contangoTbl += '<td>' + (currDataVixArray[i] * 100).toFixed(2) + '%</td>';
  }
  contangoTbl += '<td><strong>' + (currDataVixArray[27] * 100).toFixed(2) + '%</strong></td>';
  contangoTbl += '</tr><tr align="center"><td align="left">Difference</td>';
  for (let i = 10; i < 19; i++) {
    if (currDataVixArray[i] == 0)
      contangoTbl += '<td>' + '0%' + '</td>';
    else
      contangoTbl += '<td>' + (currDataVixArray[i] * 100 / 100).toFixed(2) + '</td>';
  }
  contangoTbl += '</tr></table>';

  // "Sending" data to HTML file.
  const futurePricesMtx = getDocElementById('idfuturePricesMtx');
  futurePricesMtx.innerHTML = futurePricesTbl;
  const contangoMtx = getDocElementById('idcontangoMtx');
  contangoMtx.innerHTML = contangoTbl;

  interface PriceData {
    days: number;
    price: number;
  }
  const nCurrDataVix = 7;
  const currDataPrices: PriceData[] = [];
  for (let i = 0; i < nCurrDataVix; i++) {
    const currDataPricesRows: PriceData = {
      days: parseFloat(currDataDaysVixArray[i]),
      price: parseFloat(currDataVixArray[i]),
    };
    currDataPrices.push(currDataPricesRows);
  }

  const prevDataPrices: PriceData[] = [];
  for (let i = 0; i < nCurrDataVix; i++) {
    const prevDataPricesRows: PriceData = {
      days: parseFloat(currDataDaysVixArray[i]),
      price: parseFloat(prevDataVixArray[i]),
    };
    prevDataPrices.push(prevDataPricesRows);
  }

  const spotVixValues: PriceData[] = [];
  for (let i = 0; i < nCurrDataVix; i++) {
    const spotVixValuesRows: PriceData = {
      days: parseFloat(currDataDaysVixArray[i]),
      price: parseFloat(spotVixArray[i]),
    };
    spotVixValues.push(spotVixValuesRows);
  }

  // Development for common chart function that can be used for all multiple charts - Daya

  const vixFutPriceData : any[] = [];
  for (let i = 0; i < nCurrDataVix; i++) {
    vixFutPriceData.push([currDataDaysVixArray[i],
      currDataPrices[i].price,
      prevDataPrices[i].price,
      spotVixValues[i].price]);
  }

  const noAssetsVix = 3;
  const assetNames2ArrayVix: string[] = ['Current', 'LastClose', 'SpotVix'];
  const xLabelVix: string = 'Days until expiration';
  const yLabelVix: string = 'Futures Prices (USD)';
  const yScaleTickFormatVix: string = '$';
  d3.selectAll('#vixChart > *').remove();
  const lineChrtDivVix = getDocElementById('vixChart');
  sqLineChartGenerator(noAssetsVix, nCurrDataVix, assetNames2ArrayVix, vixFutPriceData, xLabelVix, yLabelVix, yScaleTickFormatVix, lineChrtDivVix, lineChrtTooltip);
}
console.log('SqCore: Script END');