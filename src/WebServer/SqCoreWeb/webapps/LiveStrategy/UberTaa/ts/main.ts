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
  return document.getElementById(id) as HTMLElement; // type casting assures it is not null for the TS compiler. (it can be null during runtime)
}

function onClickGameChanger() {
  onImageClick(1);
}

function onClickGlobalAssets() {
  onImageClick(2);
}

function onImageClick(index: number) {
  console.log('OnClick received.' + index);
  AsyncStartDownloadAndExecuteCbLater('/StrategyUberTaa?commo=' + index, (json: any) => {
    onReceiveData(json);
  });
}

function onReceiveData(json: any) {
  if (json == 'Error') {
    const divErrorCont = getDocElementById('idErrorCont');
    divErrorCont.innerHTML = 'Error during downloading data. Please, try again later!';
    getDocElementById('errorMessage').style.visibility='visible';
    getDocElementById('pctChgCharts').style.visibility = 'hidden';
    getDocElementById('inviCharts').style.visibility = 'visible';

    return;
  }
  getDocElementById('idTitleCont').innerHTML = json.titleCont + ' <sup><small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small></sup>';
  getDocElementById('idWarningCont').innerHTML = json.warningCont;
  getDocElementById('idTimeNow').innerHTML = json.requestTime;
  getDocElementById('idLiveDataTime').innerHTML = json.lastDataTime;
  getDocElementById('idCurrentPV').innerHTML = 'Current PV: <span class="pv">$ ' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank">these current positions</a> updated for ' + json.currentPVDate + ')';
  getDocElementById('idCLMTString').innerHTML = 'Current Combined Leverage Market Timer signal is <span class="clmt">' + json.clmtSign + '</span> (SPX 50/200-day MA: ' + json.spxMASign + ', XLU/VTI: ' + json.xluVtiSign + ').';
  getDocElementById('idPosLast').innerHTML = 'Position weights in the last 20 days:';
  getDocElementById('idPosFut').innerHTML = 'Future events:';

  // const warnLength = json.warningCont.length;
  // if (warnLength>0)
  //   getDocElementById('idWarningCont').innerHTML = json.warningCont + '<br> <a href="https://docs.google.com/spreadsheets/d/1fmvGBi2Q6MxnB_8AjUedy1QVTOlWE7Ck1rICjYSSxyY" target="_blank">Google sheet with current positions</a> and <a href="https://docs.google.com/document/d/1_m3MMGag7uBZSdvc4IgXKMvj3d4kzLxwvnW14RkCyco" target="_blank">the latest study in connection with the strategy</a>';
  uberTaaTbls(json);
  // Setting charts visible after getting data.
  getDocElementById('pctChgCharts').style.visibility = 'visible';
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
  const chngInPosMtx = getDocElementById('changeInPos');
  chngInPosMtx.innerHTML = chngInPosTbl;
  const prevPositionsMtx = getDocElementById('prevPositions');
  prevPositionsMtx.innerHTML = prevPositionsTbl;
  const futPositionsMtx = getDocElementById('futPositions');
  futPositionsMtx.innerHTML = futPositionsTbl;
  const subStrategiesMtx = getDocElementById('subStrategies');
  subStrategiesMtx.innerHTML = subStrategiesTbl;


  // Declaring data sets to charts.

  const nCurrData = parseInt(json.chartLength) + 1;

  const xTicksH = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
    const xTicksHRows = new Array(2);
    xTicksHRows[0] = i;
    xTicksHRows[1] = assChartMtx[i][0];
    xTicksH[i] = xTicksHRows;
  }

  const noAssets = assetNames2Array.length - 2;
  const listH: any[] = [];
  for (let j = 0; j < noAssets; j++) {
    const assChartPerc1 = new Array(nCurrData);
    for (let i = 0; i < nCurrData; i++) {
      const assChartPerc1Rows = new Array(2);
      assChartPerc1Rows[0] = i;
      assChartPerc1Rows[1] = parseFloat(assChartMtx[i][j+1]);
      assChartPerc1[i] = assChartPerc1Rows;
    }
    listH.push({ label: assetNames2Array[j], data: assChartPerc1, points: { show: true }, lines: { show: true } });
  }
  const datasets1 = listH;

  interface pctChngStckPriceData {
    stckName: string;
    date: Date;
    price: number;
  }

  const pctChngChrtData: pctChngStckPriceData[] = [];
  for (let j = 0; j < noAssets; j++) {
    for (let i = 0; i < nCurrData; i++) {
      const chrtData: pctChngStckPriceData = {
        stckName: assetNames2Array[j],
        date: assChartMtx[i][0],
        price: parseFloat(assChartMtx[i][j + 1]),
      };
      pctChngChrtData.push(chrtData);
    }
  }

  renewedUberPctChgChart(pctChngChrtData);

  const rsiXlu = new Array(nCurrData);
  const rsiVti = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
    const rsiXluRows = new Array(2);
    const rsiVtiRows = new Array(2);
    rsiXluRows[0] = i;
    rsiXluRows[1] = parseFloat(rsiChartMtx[i][1]);
    rsiXlu[i] = rsiXluRows;
    rsiVtiRows[0] = i;
    rsiVtiRows[1] = parseFloat(rsiChartMtx[i][2]);
    rsiVti[i] = rsiVtiRows;
  }

  const spxSpot = new Array(nCurrData);
  const spx50MA = new Array(nCurrData);
  const spx200MA = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
    const spxSpotRows = new Array(2);
    const spx50MARows = new Array(2);
    const spx200MARows = new Array(2);
    spxSpotRows[0] = i;
    spxSpotRows[1] = parseFloat(spxChartMtx[i][1]);
    spxSpot[i] = spxSpotRows;
    spx50MARows[0] = i;
    spx50MARows[1] = parseFloat(spxChartMtx[i][2]);
    spx50MA[i] = spx50MARows;
    spx200MARows[0] = i;
    spx200MARows[1] = parseFloat(spxChartMtx[i][3]);
    spx200MA[i] = spx200MARows;
  }

  const datasets2 = {
    'spotSPX': {
      label: 'SPX Spot',
      data: spxSpot,
      points: { show: true },
      lines: { show: true }
    },
    'ma50SPX': {
      label: 'SPX 50-Day MA',
      data: spx50MA,
      points: { show: true },
      lines: { show: true }
    },
    'ma200SPX': {
      label: 'SPX 200-Day MA',
      data: spx200MA,
      points: { show: true },
      lines: { show: true }
    }
  };

  const datasets3 = {
    'XLUdata': {
      label: 'XLU',
      data: rsiXlu,
      points: { show: true },
      lines: { show: true }
    },
    'VTIdata': {
      label: 'VTI',
      data: rsiVti,
      points: { show: true },
      lines: { show: true }
    }
  };
  console.log(datasets1, datasets2, datasets3);
}

getDocElementById('gameChanger').onclick = onClickGameChanger;
getDocElementById('globalAssets').onclick = onClickGlobalAssets;

document.addEventListener('DOMContentLoaded', (event) => {
  console.log('DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.');
});

function shortMonthFormat(date: any) : string {
  const formatMillisec = d3.timeFormat('.%L');
  const formatShortMonth = d3.timeFormat('%Y-%m-%d');
  const formatYear = d3.timeFormat('%Y');
  return (d3.timeSecond(date) < date ? formatMillisec :
    d3.timeYear(date) < date ? formatShortMonth :
    formatYear)(date);
}

function renewedUberPctChgChart(uberStckChrtData) { // renewedUberPctChgChart
  const margin = {top: 10, right: 30, bottom: 50, left: 60};
  const width = 760 - margin.left - margin.right;
  const height = 450 - margin.top - margin.bottom;

  const stckChrtData = uberStckChrtData.map((r:{stckName: string, date: Date; price: number; }) =>
    ({stckName: (r.stckName), date: new Date(r.date), price: (r.price)}));

  const xMin = d3.min(stckChrtData, (r:{ date: any; }) => r.date as Date);
  const xMax = d3.max(stckChrtData, (r:{ date: any; }) => r.date as Date);
  const yMin = d3.min(stckChrtData, (r:{ price: any; }) => r.price as number);
  const yMax = d3.max(stckChrtData, (r:{ price: any; }) => r.price as number);

  // Add X axis --> it is a date format
  const xScale = d3.scaleTime()
      .domain([xMin as Date, xMax as Date])
      .range([0, width]);

  // Add Y axis
  const yScale = d3.scaleLinear()
      .domain([(yMin as number) - 5, (yMax as number) + 5])
      .range([height, 0]);

  const pctChrt = d3.select('#pctChgChrt')
      .append('svg')
      .style('background', 'white')
      .attr('width', width + margin.left + margin.right)
      .attr('height', height + margin.top + margin.bottom)
      .append('g')
      .attr('transform',
          'translate(' + margin.left + ',' + margin.top + ')');

  pctChrt.append('g')
      .attr('class', 'grid')
      .attr('transform', 'translate(0,' + height +')')
      .call(d3.axisBottom(xScale).tickSize(-height).tickFormat(shortMonthFormat))
      .selectAll('text')
      .style('text-anchor', 'end')
      .attr('transform', 'rotate(-25)');

  pctChrt.append('g')
      .attr('class', 'grid')
      .call(d3.axisLeft(yScale)
          .tickSize(-width)
          .tickFormat((d: any) => d + '%'));

  // grouping the data
  const stckDataGroups: any[] = [];
  stckChrtData.forEach(function(this: any, a) {
    if (!this[a.stckName]) {
      this[a.stckName] = { key: a.stckName, values: [] };
      stckDataGroups.push(this[a.stckName]);
    }
    this[a.stckName].values.push({ date: a.date, price: a.price });
  }, Object.create(null));
  console.log(stckDataGroups);

  const stckKey = stckDataGroups.map(function(d: any) { return d.key; }); // list of group names
  // adding colors for keys
  const color = d3.scaleOrdinal()
      .domain(stckKey)
      .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#999999', '#ffff33', '#a65628']);

  // Draw the line
  pctChrt.selectAll('.line')
      .data(stckDataGroups)
      .enter()
      .append('path')
      .attr('fill', 'none')
      .attr('stroke', (d: any) => color(d.key) as any)
      .attr('stroke-width', 1.5)
      .attr('d', (d:any) => (d3.line()
          .x((d: any) => xScale(d.date) as number)
          .y((d: any) => yScale(d.price) as number))(d.values) as any);

  // // Add the Legend
  pctChrt.selectAll('rect')
      .data(stckDataGroups)
      .enter().append('text')
      .attr('x', (d: any, i: any) => ( 25 + i * 60))
      .attr('y', 30)
      .attr('class', 'pctChgLabel') // style the legend
      .style('fill', (d: any) => color(d.key) as any)
      .text((d: any) => (d.key));
  // x axis label
  pctChrt
      .append('text')
      .attr('class', 'pctChgLabel')
      .attr('transform', 'translate(' + width / 2 + ' ,' + (height + 42) + ')')
      .text('Date');
  // y axis label
  pctChrt
      .append('text')
      .attr('class', 'pctChgLabel')
      .attr('transform', 'rotate(-90)')
      .attr('y', 0 - (margin.left))
      .attr('x', 0 - (height / 2))
      .attr('dy', '2em')
      .text('Percentage Change');

  pctChrt
      .selectAll('myCircles')
      .data(stckChrtData)
      .enter()
      .append('circle')
      .style('fill', 'none')
      .attr('stroke', (d: any) => color(d.stckName) as any)
      .attr('stroke-width', 0.8)
      .attr('cx', ((d: any) => xScale(d.date) as number))
      .attr('cy', (d: any) => yScale(d.price) as number)
      .attr('r', 2.5);

  const tooltipPctChg = d3.select('#tooltipChart');
  const tooltipLine = pctChrt.append('line');
  pctChrt.append('rect')
      .attr('width', width)
      .attr('height', height)
      .attr('opacity', 0)
      .on('mousemove', onMouseMove)
      .on('mouseout', onMouseOut);

  function onMouseOut() {
    if (tooltipPctChg)
      tooltipPctChg.style('display', 'none');
    if (tooltipLine)
      tooltipLine.attr('stroke', 'none');
  }

  function onMouseMove(event: any) {
    const daysArray: any[] = [];
    stckChrtData.forEach((element) => {
      daysArray.push(element.date);
    });

    const xCoord = xScale.invert(d3.pointer(event)[0]);
    const yCoord = d3.pointer(event)[1];
    const closestXCoord = daysArray.sort(
        (a, b) => Math.abs(xCoord.getTime() as any - a.getTime()) - Math.abs(xCoord.getTime() as any- b.getTime()))[0];
    const closestYCoord = stckDataGroups[0].values.find((h: any) => h.date as any === closestXCoord).price;

    // console.log(ttTextArray1.length);
    tooltipLine
        .attr('stroke', 'black')
        .attr('x1', xScale(closestXCoord))
        .attr('x2', xScale(closestXCoord))
        .attr('y1', 0 + 10)
        .attr('y2', height);

    tooltipPctChg
        .html('Percentage Changes :' + '<br>')
        .style('display', 'block')
        .style('left', event.pageX + 10)
        .style('top', (event.pageY - yCoord + yScale(closestYCoord)) + 15)
        .selectAll()
        .data(stckDataGroups)
        .enter()
        .append('div')
        .style('color', (d: any) => color(d.key) as any)
        .html((d: any) => d.key + ': ' + d.values.find((h: any) => h.date.getTime() as any === closestXCoord.getTime() as any).price + '%');
  }
}
console.log('SqCore: Script END');