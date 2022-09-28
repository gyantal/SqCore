import './../css/main.css';
import { sqLineChartGenerator } from '../../../../TsLib/sq-common/sqlineChrt';
import * as d3 from 'd3';

// export {}; // TS convention: To avoid top level duplicate variables, functions. This file should be treated as a module (and have its own scope). A file without any top-level import or export declarations is treated as a script whose contents are available in the global scope.

// 1. Declare some global variables and hook on DOMContentLoaded() and window.onload()
console.log('SqCore: Script BEGIN');

function getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
  return document.getElementById(id) as HTMLElement;
}

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

function onClickGameChanger() {
  AsyncStartDownloadAndExecuteCbLater('/StrategyUberTaa?universe=1&winnerRun=1', (json: any) => {
    onReceiveData(json);
  });
}

function onClickGlobalAssets() {
  AsyncStartDownloadAndExecuteCbLater('/StrategyUberTaa?universe=2&winnerRun=0', (json: any) => {
    onReceiveData(json);
  });
}

function onReceiveData(json: any) {
  if (json == 'Error') {
    const divErrorCont = getNonNullDocElementById('idErrorCont');
    divErrorCont.innerHTML = 'Error during downloading data. Please, try again later!';
    getNonNullDocElementById('errorMessage').style.visibility='visible';
    getNonNullDocElementById('pctChgCharts').style.visibility = 'hidden';
    getNonNullDocElementById('xluChart').style.visibility = 'hidden';
    getNonNullDocElementById('spyChart').style.visibility = 'hidden';

    return;
  }
  getNonNullDocElementById('idTitleCont').innerHTML = json.titleCont + ' <sup><small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small></sup>';
  getNonNullDocElementById('idWarningCont').innerHTML = json.warningCont;
  getNonNullDocElementById('idTimeNow').innerHTML = json.requestTime;
  getNonNullDocElementById('idLiveDataTime').innerHTML = json.lastDataTime;
  getNonNullDocElementById('idCurrentPV').innerHTML = 'Current PV: <span class="pv">$ ' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank">these current positions</a> updated for ' + json.currentPVDate + ')';
  getNonNullDocElementById('idCLMTString').innerHTML = 'Current Combined Leverage Market Timer signal is <span class="clmt">' + json.clmtSign + '</span> (SPX 50/200-day MA: ' + json.spxMASign + ', XLU/VTI: ' + json.xluVtiSign + ').';
  getNonNullDocElementById('idPosLast').innerHTML = 'Position weights in the last 20 days:';
  getNonNullDocElementById('idPosFut').innerHTML = 'Future events:';

  const warnLength = json.warningCont.length;
  if (warnLength>0)
    getNonNullDocElementById('idWarningCont').innerHTML = json.warningCont + '<br> <a href="https://docs.google.com/spreadsheets/d/1fmvGBi2Q6MxnB_8AjUedy1QVTOlWE7Ck1rICjYSSxyY" target="_blank">Google sheet with current positions</a> and <a href="https://docs.google.com/document/d/1_m3MMGag7uBZSdvc4IgXKMvj3d4kzLxwvnW14RkCyco" target="_blank">the latest study in connection with the strategy</a>';
  uberTaaTbls(json);
  // Setting charts visible after getting data.
  getNonNullDocElementById('pctChgCharts').style.visibility = 'visible';
  getNonNullDocElementById('xluChart').style.visibility = 'visible';
  getNonNullDocElementById('spyChart').style.visibility = 'visible';
}

function uberTaaTbls(json: any) {
  // Creating JavaScript data arrays by splitting.
  const assetNames2Array = json.assetNames2.split(', ');
  const currPosNumArray = json.currPosNum.split(', ');
  const currPosValArray = json.currPosVal.split(', ');
  const nextPosNumArray = json.nextPosNum.split(', ');
  const nextPosValArray = json.nextPosVal.split(', ');
  const diffPosNumArray = json.posNumDiff.split(', ');
  const diffPosValArray = json.posValDiff.split(', ');

  const prevPosMtxTemp = json.prevPositionsMtx.split('ß ');
  const prevPosMtx: any[] = [];
  for (let i = 0; i < prevPosMtxTemp.length; i++)
    prevPosMtx[i] = prevPosMtxTemp[i].split(',');


  const prevAssetEventMtxTemp = json.prevAssEventMtx.split('ß ');
  const prevAssetEventMtx: any[] = [];
  for (let i = 0; i < prevAssetEventMtxTemp.length; i++)
    prevAssetEventMtx[i] = prevAssetEventMtxTemp[i].split(',');


  const futPosMtxTemp = json.futPositionsMtx.split('ß ');
  const futPosMtx: any[] = [];
  for (let i = 0; i < futPosMtxTemp.length; i++)
    futPosMtx[i] = futPosMtxTemp[i].split(',');


  const futAssetEventMtxTemp = json.futAssEventMtx.split('ß ');
  const futAssetEventMtx: any[] = [];
  for (let i = 0; i < futAssetEventMtxTemp.length; i++)
    futAssetEventMtx[i] = futAssetEventMtxTemp[i].split(',');


  const assChartMtxTemp = json.assetChangesToChartMtx.split('ß ');
  const assChartMtx: any[] = [];
  for (let i = 0; i < assChartMtxTemp.length; i++)
    assChartMtx[i] = assChartMtxTemp[i].split(',');


  const rsiChartMtxTemp = json.xluVtiPercToChartMtx.split('ß ');
  const rsiChartMtx: any[] = [];
  for (let i = 0; i < rsiChartMtxTemp.length; i++)
    rsiChartMtx[i] = rsiChartMtxTemp[i].split(',');


  const spxChartMtxTemp = json.spxMAToChartMtx.split('ß ');
  const spxChartMtx: any[] = [];
  for (let i = 0; i < spxChartMtxTemp.length; i++)
    spxChartMtx[i] = spxChartMtxTemp[i].split(',');


  // Creating the HTML code of tables.
  let chngInPosTbl = '<table class="currData"><tr align="center"><td bgcolor="#66CCFF"></td>';
  for (let i = 0; i < assetNames2Array.length - 1; i++)
    chngInPosTbl += '<td bgcolor="#66CCFF"><a href=https://finance.yahoo.com/quote/' + assetNames2Array[i] + ' target="_blank">' + assetNames2Array[i] + '</a></td>';

  chngInPosTbl += '<td bgcolor="#66CCFF">' + assetNames2Array[assetNames2Array.length - 1] + '</td>';

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">' + json.nextTradingDay + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevAssetEventMtx[1][i + 1] + '">' + nextPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevAssetEventMtx[1][i + 1] + '">' + nextPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">' + json.currPosDate + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevAssetEventMtx[2][i + 1] + '">' + currPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevAssetEventMtx[2][i + 1] + '">' + currPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">Change in Positions</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFF00">' + diffPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFF00">' + diffPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr></table>';

  let prevPositionsTbl = '<table class="currData"><tr align="center">';
  for (let i = 0; i < prevPosMtxTemp.length; i++) {
    for (let j = 0; j < assetNames2Array.length + 2; j++)
      prevPositionsTbl += '<td bgcolor="#' + prevAssetEventMtx[i][j] + '">' + prevPosMtx[i][j] + '</td>';

    prevPositionsTbl += '</tr>';
  }
  prevPositionsTbl += '</table>';

  let futPositionsTbl = '<table class="currData"><tr align="center">';
  for (let i = 0; i < futPosMtxTemp.length; i++) {
    for (let j = 0; j < assetNames2Array.length; j++)
      futPositionsTbl += '<td bgcolor="#' + futAssetEventMtx[i][j] + '">' + futPosMtx[i][j] + '</td>';

    futPositionsTbl += '</tr>';
  }
  futPositionsTbl += '</table>';

  const subStrategiesTbl = '<table class="currData2"><tr><td bgcolor="#1E90FF">Earnings Day</td><td bgcolor="#228B22">FOMC Bullish Day</td><td bgcolor="#FF0000">FOMC Bearish Day</td></tr><tr><td bgcolor="#7B68EE">Pre-Earnings Day</td><td bgcolor="#7CFC00">Holiday Bullish Day</td><td bgcolor="#DC143C">Holiday Bearish Day</td></tr><tr><td bgcolor="#00FFFF">CLMT Bullish Day</td><td bgcolor="#A9A9A9">CLMT Neutral Day</td><td bgcolor="#FF8C00">CLMT Bearish Day</td></tr></table > ';

  // "Sending" data to HTML file.
  const chngInPosMtx = getNonNullDocElementById('changeInPos');
  chngInPosMtx.innerHTML = chngInPosTbl;
  const prevPositionsMtx = getNonNullDocElementById('prevPositions');
  prevPositionsMtx.innerHTML = prevPositionsTbl;
  const futPositionsMtx = getNonNullDocElementById('futPositions');
  futPositionsMtx.innerHTML = futPositionsTbl;
  const subStrategiesMtx = getNonNullDocElementById('subStrategies');
  subStrategiesMtx.innerHTML = subStrategiesTbl;


  // Declaring data sets to charts.

  const nCurrData = parseInt(json.chartLength) + 1;
  const noAssets = assetNames2Array.length - 2;
  const xLabel: string = 'Dates';
  const yLabel: string = 'Percentage Change';
  const yScaleTickFormat: string = '%';
  const isDrawCricles: boolean = true;
  d3.selectAll('#pctChgChrt > *').remove();
  const lineChrtDiv = getNonNullDocElementById('pctChgChrt');
  const lineChrtTooltip = getNonNullDocElementById('tooltipChart');
  sqLineChartGenerator(noAssets, nCurrData, assetNames2Array, assChartMtx, xLabel, yLabel, yScaleTickFormat, lineChrtDiv, lineChrtTooltip, isDrawCricles);

  // Xlu Timer Chart
  const noAssetsXlu = 2;
  const assetNames2ArrayXlu: string[] = ['XLU', ' VTI'];
  const xLabelXlu: string = 'Dates';
  const yLabelXlu: string = 'RSI';
  const yScaleTickFormatXlu: string = '';
  d3.selectAll('#xluChrt > *').remove();
  const lineChrtDivXlu = getNonNullDocElementById('xluChrt');
  sqLineChartGenerator(noAssetsXlu, nCurrData, assetNames2ArrayXlu, rsiChartMtx, xLabelXlu, yLabelXlu, yScaleTickFormatXlu, lineChrtDivXlu, lineChrtTooltip, isDrawCricles);

  // Spx Timer Chart
  const noAssetsSpx = 3;
  const assetNames2ArraySpx: string[] = ['spotSPY', 'ma50SPY', 'ma200SPY'];
  const xLabelSpx: string = 'Dates';
  const yLabelSpx: string = 'Index Value';
  const yScaleTickFormatSpx: string = '';
  d3.selectAll('#spyChrt > *').remove();
  const lineChrtDivSpx = getNonNullDocElementById('spyChrt');
  sqLineChartGenerator(noAssetsSpx, nCurrData, assetNames2ArraySpx, spxChartMtx, xLabelSpx, yLabelSpx, yScaleTickFormatSpx, lineChrtDivSpx, lineChrtTooltip, isDrawCricles);
}

getNonNullDocElementById('gameChanger').onclick = onClickGameChanger;
getNonNullDocElementById('globalAssets').onclick = onClickGlobalAssets;

document.addEventListener('DOMContentLoaded', (event) => {
  console.log('DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.');
});
console.log('SqCore: Script END');