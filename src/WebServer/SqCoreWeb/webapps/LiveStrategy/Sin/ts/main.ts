import './../css/main.css';
import * as d3 from 'd3';
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

  AsyncStartDownloadAndExecuteCbLater('/StrategySin', (json: any) => {
    onReceiveData(json);
    processChartWithMockup();
    // processPctChngStckPriceChrt();
    // Server Diagnostics message when mouseenter the wifi image
    getDocElementById('sinDiagnostics').onmouseenter = function sqDiagnosticsModal() {
      const dialog = getDocElementById('sinDiagnosticsDialog');
      dialog.style.display = 'block';
    };
  });
  console.log('SqCore: window.onload() END.');
};

function pctgToColor(perc, min, max) {
  const base = (max - min);
  if (base == 0 || perc > max)
    perc = 100;
  else if (perc < min)
    perc = 0;
  else
    perc = (perc - min) / base * 100;
  let r; let g; let b = 0;
  if (perc < 50) {
    r = 255;
    b = 110;
    g = Math.round(127+2.55 * perc);
  } else {
    g = 255;
    r = Math.round(382 - 2.55 * perc);
    b = 110;
  }
  const h = r * 0x10000 + g * 0x100 + b * 0x1;
  return '#' + ('000000' + h.toString(16)).slice(-6);
}

function onReceiveData(json: any) {
  if (json == 'Error') {
    const divErrorCont = getDocElementById('idErrorCont');
    divErrorCont.innerHTML = 'Error during downloading data. Please, try again later!';
    getDocElementById('errorMessage').style.visibility='visible';
    // getDocElementById('inviCharts').style.visibility = 'hidden';

    return;
  }

  getDocElementById('errorMessage').style.display = 'none';

  // getDocElementById('DebugDataArrivesHere').innerText = json.titleCont + ' <sup><small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small></sup>';
  // Creating first rows of webpage.
  getDocElementById('titleCont').innerHTML = json.titleCont + ' <sup><small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small></sup>';
  getDocElementById('requestTime').innerText = json.requestTime;
  getDocElementById('lastDataTime').innerText = json.lastDataTime;
  getDocElementById('currentPV').innerHTML = 'Current PV: <span class="pv">$' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank" >these current positions</a> updated on ' + json.currentPVDate + ')';
  if (json.dailyProfSig !== 'N/A')
    getDocElementById('dailyProfit').innerHTML = '<b>Daily Profit/Loss: <span class="' + json.dailyProfString + '">' + json.dailyProfSig + json.dailyProfAbs + ' ('+json.dailyProfPerc+'%)</span></b>&emsp;';
  if (json.monthlyProfSig !== 'N/A')
    getDocElementById('monthlyProfit').innerHTML = '<b>MTD Profit/Loss: <span class="' + json.monthlyProfString + '">' + json.monthlyProfSig + json.monthlyProfAbs + ' (' + json.monthlyProfPerc +'%)</span></b>&emsp;';
  if (json.yearlyProfSig !== 'N/A')
    getDocElementById('yearlyProfit').innerHTML = '<b>YTD Profit/Loss: <span class="' + json.yearlyProfString + '">' + json.yearlyProfSig + json.yearlyProfAbs + ' (' + json.yearlyProfPerc + '%)</span></b>&emsp;';
  getDocElementById('bondPerc').innerHTML = '<span class="notDaily">Current / Required Bond Percentage: ' + json.currBondPerc + ' / ' + json.nextBondPerc + '.&emsp;&emsp; Used Leverage: ' + json.leverage +'.&emsp;Used Maximum Bond Percentage: '+json.maxBondPerc+'.</span>';

  sinAddictionInfoTbls(json);
  // getDocElementById('inviCharts').style.visibility = 'visible';
}

function sinAddictionInfoTbls(json) {
  // Creating JavaScript data arrays by splitting.
  const assetNames2Array = json.assetNames2.split(', ');
  const currPosNumArray = json.currPosNum.split(', ');
  const currPosValArray = json.currPosVal.split(', ');
  const nextPosNumArray = json.nextPosNum.split(', ');
  const nextPosValArray = json.nextPosVal.split(', ');
  const diffPosNumArray = json.posNumDiff.split(', ');
  const diffPosValArray = json.posValDiff.split(', ');

  const assChartMtxTemp = json.assetChangesToChartMtx.split('ß ');
  const assChartMtx: any[] = [];
  for (let i = 0; i < assChartMtxTemp.length; i++)
    assChartMtx[i] = assChartMtxTemp[i].split(',');

  const assScoresMtxTemp = json.assetScoresMtx.split('ß ');
  const assScoresMtx: any[] = [];
  for (let i = 0; i < assScoresMtxTemp.length; i++)
    assScoresMtx[i] = assScoresMtxTemp[i].split(',');

  // Creating the HTML code of tables.
  let chngInPosTbl = '<table class="currData"><tr align="center"><td bgcolor="#66CCFF"></td>';
  for (let i = 0; i < assetNames2Array.length - 1; i++)
    chngInPosTbl += '<td bgcolor="#66CCFF"><a href=https://finance.yahoo.com/quote/' + assetNames2Array[i] + ' target="_blank">' + assetNames2Array[i] + '</a></td>';

  chngInPosTbl += '<td bgcolor="#66CCFF">' + assetNames2Array[assetNames2Array.length - 1] + '</td>';

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FFABAB">' + json.nextTradingDay + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFFD1">' + nextPosValArray[i] + '</td>';

  chngInPosTbl += '</tr > <tr align="center">';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#FFFFD1">' + nextPosNumArray[i] + '</td>';

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FFABAB">' + json.currPosDate + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#E7FFAC">' + currPosValArray[i] + '</td>';

  chngInPosTbl += '</tr > <tr align="center">';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#E7FFAC">' + currPosNumArray[i] + '</td>';

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FFABAB">Change in Positions</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#C4FAF8">' + diffPosValArray[i] + '</td>';

  chngInPosTbl += '</tr > <tr align="center">';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#C4FAF8">' + diffPosNumArray[i] + '</td>';

  chngInPosTbl += '</tr></table>';

  let pctChngStckPriceTbl = '<table class="currData"><tr align="center"  bgcolor="#1E90FF"><td rowspan="2"></td><td rowspan="2">TAA Percentile Channel Score</td><td rowspan="2">Required Asset Weight</td><td colspan="7">Percentage Change of Stock Price</td></tr><tr align="center" bgcolor="#6EB5FF"><td>1-Day</td><td>1-Week</td><td>2-Weeks</td><td>1-Month</td><td>3-Months</td><td>6-Months</td><td>1-Year</td></tr>';
  for (let i = 0; i < assetNames2Array.length-2; i++) {
    pctChngStckPriceTbl += '<tr align="center">';
    pctChngStckPriceTbl += '<td bgcolor="#66CCFF"><a href=https://finance.yahoo.com/quote/' + assetNames2Array[i] + ' target="_blank">' + assetNames2Array[i] + '</a></td>';
    pctChngStckPriceTbl += '<td bgcolor="' + pctgToColor(parseFloat(assScoresMtx[i][0]), -100, 100) + '">' + assScoresMtx[i][0] + '</td>';
    pctChngStckPriceTbl += '<td bgcolor="' + pctgToColor(parseFloat(assScoresMtx[i][1]), -20, 20) + '">' + assScoresMtx[i][1] + '</td>';
    for (let j = 0; j < 7; j++)
      pctChngStckPriceTbl += '<td bgcolor="' + pctgToColor(parseFloat(assChartMtx[i][j]), -40, 40) + '">' + assChartMtx[i][j] + '</td>';

    pctChngStckPriceTbl += '</tr>';
  }
  pctChngStckPriceTbl += '<tr align="center">';
  pctChngStckPriceTbl += '<td bgcolor="#66CCFF"><a href=https://finance.yahoo.com/quote/' + assetNames2Array[assetNames2Array.length - 2] + ' target="_blank">' + assetNames2Array[assetNames2Array.length - 2] + '</a></td>';
  pctChngStckPriceTbl += '<td bgcolor="#FFF5BA">' + assScoresMtx[assetNames2Array.length - 2][0] + '</td>';
  pctChngStckPriceTbl += '<td bgcolor="' + pctgToColor(parseFloat(assScoresMtx[assetNames2Array.length - 2][1]), -20, 20) + '">' + assScoresMtx[assetNames2Array.length - 2][1] + '</td>';
  for (let j = 0; j < 7; j++)
    pctChngStckPriceTbl += '<td bgcolor="' + pctgToColor(parseFloat(assChartMtx[assetNames2Array.length - 2][j]), -40, 40) + '">' + assChartMtx[assetNames2Array.length - 2][j] + '</td>';

  pctChngStckPriceTbl += '</tr>';
  pctChngStckPriceTbl += '</table>';
  // "Sending" data to HTML file.
  const currTableMtx2 = getDocElementById('changeInPos');
  currTableMtx2.innerHTML = chngInPosTbl;
  const currTableMtx4 = getDocElementById('percentageChange');
  currTableMtx4.innerHTML = pctChngStckPriceTbl;

  // Declaring data sets to charts.
  // const retHistLBPeriods = json.pastPerfDaysName.split(', ');
  const retHistLBPeriodsNoS = json.pastPerfDaysNum.split(', ');
  const retHistLBPeriodsNo: any[] = [];
  for (let i = 0; i < retHistLBPeriodsNoS.length; i++)
    retHistLBPeriodsNo[i] = parseInt(retHistLBPeriodsNoS[i]);

  const lengthOfChart = 21;
  const indOfLength = retHistLBPeriodsNo.indexOf(lengthOfChart);
  // const divChartLength = document.getElementById('idChartLength');
  // divChartLength.innerHTML = '<div class="DDM"><strong>in the Last &emsp;<select class="DDM" id="limit2"><option value="1">1 Day</option><option value="5">1 Week</option><option value="10">2 Weeks</option><option value="21" selected>1 Month</option><option value="63">3 Months</option><option value="126">6 Months</option><option value="252">1 Year</option>' + retHistLBPeriods[indOfLength] + '</select></strong ></div>';
  // chart data preparation
  const nCurrData = 1;
  const noAssets = assetNames2Array.length - 1;

  const yTicksH = new Array(noAssets);
  for (let i = 0; i < noAssets; i++) {
    const yTicksHRows = new Array(2);
    yTicksHRows[0] = i;
    yTicksHRows[1] = assetNames2Array[i];
    yTicksH[i] = yTicksHRows;
  }

  const listH: any[] = [];
  for (let j = 0; j < noAssets; j++) {
    const assChartPerc1 = new Array(nCurrData);
    for (let i = 0; i < nCurrData; i++) {
      const assChartPerc1Rows = new Array(2);
      assChartPerc1Rows[1] = j;
      assChartPerc1Rows[0] = parseFloat(assChartMtx[j][indOfLength]);
      assChartPerc1[i] = assChartPerc1Rows;
    }
    listH.push({ label: assetNames2Array[j], data: assChartPerc1, bars: { show: true } });
  }
  // const datasets1 = listH;
  // processPctChngStckPriceChrt(datasets1, noAssets, yTicksH, nCurrData, retHistLBPeriods[indOfLength]);
}

// Bar chart with mockup data - under development Daya.

function processChartWithMockup() {
  // set the dimensions and margins of the graph
  const margin = {top: 30, right: 30, bottom: 70, left: 60};
  const width = 460 - margin.left - margin.right;
  const height = 400 - margin.top - margin.bottom;

  // append the svg object to the body of the page
  const svg = d3.select('#SinAddictionChart')
      .append('svg')
      .attr('width', width + margin.left + margin.right)
      .attr('height', height + margin.top + margin.bottom)
      .append('g')
      .attr('transform',
          'translate(' + margin.left + ',' + margin.top + ')');
  const data: any[] = [{name: 'A', value: 90},
    {name: 'B', value: 40},
    {name: 'C', value: 10},
    {name: 'D', value: 50},
    {name: 'E', value: 30},
    {name: 'F', value: 20},
    {name: 'G', value: 70}];

  // Add X axis
  const xScale = d3.scaleBand()
      .range([0, width])
      .domain(data.map((d) => d.name))
      .padding(0.5);
  svg.append('g')
      .attr('transform', 'translate(0,' + height + ')')
      .call(d3.axisBottom(xScale))
      .selectAll('text')
      .attr('transform', 'translate(-10,0)rotate(-45)')
      .style('text-anchor', 'end');

  // Add Y axis
  const yScale = d3.scaleLinear()
      .domain([0, 100])
      .range([height, 0]);
  svg.append('g')
      .call(d3.axisLeft(yScale));


  const chrt = svg.selectAll('rect')
      .data(data)
      // .enter()
      .join('rect')
      .attr('x', (d) => yScale(d.value))
      .attr('y', (d) => yScale(d.value))
      // .attr('width', xScale.bandwidth())
      // .attr('height', (d) => (height - yScale(d.value)))
      .attr('fill', 'blue');
  chrt.join('rect')
      .attr('y', (d) => yScale(d.value))
      .attr('width', xScale.bandwidth())
      .attr('height', (d) => (height - yScale(d.value)));
}

console.log('SqCore: Script END');