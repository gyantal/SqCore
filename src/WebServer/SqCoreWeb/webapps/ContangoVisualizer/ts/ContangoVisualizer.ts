import './../css/main.css';
import * as d3 from 'd3';
// import { NGXLogger } from '../../../ts/sq-ngx-logger/logger.service.js';
// import { NgxLoggerLevel } from '../../../ts/sq-ngx-logger/types/logger-level.enum.js';

// export {}; // TS convention: To avoid top level duplicate variables, functions. This file should be treated as a module (and have its own scope). A file without any top-level import or export declarations is treated as a script whose contents are available in the global scope.

// 1. Declare some global variables and hook on DOMContentLoaded() and window.onload()
console.log('SqCore: Script BEGIN4');

async function AsyncStartDownloadAndExecuteCbLater(
    url: string,
    callback: (json: any) => any
) {
  fetch(url)
      .then((response) => {
      // asynch long running task finishes. Resolves to get the Response object (http header, info), but not the full body (that might be streaming and arriving later)
        console.log(
            'SqCore.AsyncStartDownloadAndExecuteCbLater(): Response object arrived:'
        );
        if (!response.ok)
          return Promise.reject(new Error('Invalid response status'));

        response.json().then((json) => {
        // asynch long running task finishes. Resolves to the body, converted to json() object or text()
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

function onImageClickVIX() {
  onImageClick(1);
}

function onImageClickOIL() {
  onImageClick(2);
}

function onImageClickGAS() {
  onImageClick(3);
}

function onImageClick(index: number) {
  console.log('OnClick received.' + index);
  AsyncStartDownloadAndExecuteCbLater(
      '/ContangoVisualizerData?commo=' + index,
      (json: any) => {
        onDataReceived(json);
      }
  );
}

function onDataReceived(json: any) {
  // Creating first row (dates) of webpage.
  const divTitleCont = document.getElementById('idTitleCont') as HTMLElement;
  const divTimeNow = document.getElementById('idTimeNow') as HTMLElement;
  const divLiveDataDate = document.getElementById(
      'idLiveDataDate'
  ) as HTMLElement;
  const divLiveDataTime = document.getElementById(
      'idLiveDataTime'
  ) as HTMLElement;
  const divMyLink = document.getElementById('myLink') as HTMLElement;
  const divChart = document.getElementById('inviCharts') as HTMLElement;

  divTitleCont.innerText = json.titleCont;
  divTimeNow.innerText = 'Current time: ' + json.timeNow;
  divLiveDataDate.innerText = 'Last data time: ' + json.liveDataDate;
  divLiveDataTime.innerText = json.liveDataTime;
  divMyLink.innerHTML =
    '<a href="' + json.dataSource + '" target="_blank">Data Source</a>';

  creatingTables(json);

  // Setting charts visible after getting data.
  divChart.style.visibility = 'visible';
}

function creatingTables(json) {
  // Creating JavaScript data arrays by splitting.
  const currDataArray = json.currDataVec.split(',').map(Number);
  const currDataDaysArray = json.currDataDaysVec.split(',').map(Number);
  const prevDataArray = json.prevDataVec.split(',').map(Number);
  const currDataDiffArray = json.currDataDiffVec.split(',');
  const currDataPercChArray = json.currDataPercChVec.split(',');
  const spotVixArray = json.spotVixVec.split(',').map(Number);

  // Creating the HTML code of current table.
  let currTableMtx =
    '<table class="currData"><tr align="center"><td>Futures Prices</td><td>F1</td><td>F2</td><td>F3</td><td>F4</td><td>F5</td><td>F6</td><td>F7</td><td>F8</td></tr><tr align="center"><td align="left">Current</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataArray[i] === 0)
      currTableMtx += '<td>' + '---' + '</td>';
    else
      currTableMtx += '<td>' + currDataArray[i] + '</td>';
  }

  currTableMtx +=
    '</tr><tr align="center"><td align="left">Previous Close</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataArray[i] === 0)
      currTableMtx += '<td>' + '---' + '</td>';
    else
      currTableMtx += '<td>' + prevDataArray[i] + '</td>';
  }
  currTableMtx +=
    '</tr><tr align="center"><td align="left">Daily Abs. Change</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataArray[i] === 0)
      currTableMtx += '<td>' + '---' + '</td>';
    else
      currTableMtx += '<td>' + currDataDiffArray[i] + '</td>';
  }
  currTableMtx +=
    '</tr><tr align="center"><td align="left">Daily % Change</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataArray[i] === 0)
      currTableMtx += '<td>' + '---' + '</td>';
    else {
      currTableMtx +=
        '<td>' + (currDataPercChArray[i] * 100).toFixed(2) + '%</td>';
    }
  }
  currTableMtx +=
    '</tr><tr align="center"><td align="left">Cal. Days to Expiration</td>';
  for (let i = 0; i < 8; i++) {
    if (currDataArray[i] === 0)
      currTableMtx += '<td>' + '---' + '</td>';
    else
      currTableMtx += '<td>' + currDataDaysArray[i] + '</td>';
  }
  currTableMtx += '</tr></table>';

  let currTableMtx3 =
    '<table class="currData"><tr align="center"><td>Contango</td><td>F2-F1</td><td>F3-F2</td><td>F4-F3</td><td>F5-F4</td><td>F6-F5</td><td>F7-F6</td><td>F8-F7</td><td>F7-F4</td><td>(F7-F4)/3</td></tr><tr align="center"><td align="left">Monthly Contango %</td><td><strong>' +
    (currDataArray[8] * 100).toFixed(2) +
    '%</strong></td>';
  for (let i = 20; i < 27; i++) {
    if (currDataArray[i] === 0)
      currTableMtx3 += '<td>' + '---' + '</td>';
    else
      currTableMtx3 += '<td>' + (currDataArray[i] * 100).toFixed(2) + '%</td>';
  }
  currTableMtx3 +=
    '<td><strong>' + (currDataArray[27] * 100).toFixed(2) + '%</strong></td>';
  currTableMtx3 += '</tr><tr align="center"><td align="left">Difference</td>';
  for (let i = 10; i < 19; i++) {
    if (currDataArray[i] === 0)
      currTableMtx3 += '<td>' + '---' + '</td>';
    else {
      currTableMtx3 +=
        '<td>' + ((currDataArray[i] * 100) / 100).toFixed(2) + '</td>';
    }
  }
  currTableMtx3 += '</tr></table>';

  // "Sending" data to HTML file.
  const currTableMtx2 = document.getElementById(
      'idCurrTableMtx'
  ) as HTMLTableElement;
  currTableMtx2.innerHTML = currTableMtx;
  const currTableMtx4 = document.getElementById(
      'idCurrTableMtx3'
  ) as HTMLTableElement;
  currTableMtx4.innerHTML = currTableMtx3;

  interface PriceData {
    days: number;
    price: number;
  }
  const nCurrData = 7;
  const currDataPrices: PriceData[] = [];
  for (let i = 0; i < nCurrData; i++) {
    const currDataPricesRows: PriceData = {
      days: currDataDaysArray[i],
      price: currDataArray[i],
    };
    currDataPrices.push(currDataPricesRows);
  }

  const prevDataPrices: PriceData[] = [];
  for (let i = 0; i < nCurrData; i++) {
    const prevDataPricesRows: PriceData = {
      days: currDataDaysArray[i],
      price: prevDataArray[i],
    };
    prevDataPrices.push(prevDataPricesRows);
  }

  const spotVixValues: PriceData[] = [];
  for (let i = 0; i < nCurrData; i++) {
    const spotVixValuesRows: PriceData = {
      days: currDataDaysArray[i],
      price: spotVixArray[i],
    };
    spotVixValues.push(spotVixValuesRows);
  }

  // Declaring data sets to charts.

  interface DataSet {
    name: string;
    history: PriceData[];
    show: boolean;
    color: string;
  }

  const current: DataSet = {
    name: 'Current',
    history: currDataPrices,
    show: true,
    color: 'blue',
  };

  const previous: DataSet = {
    name: 'Last Close',
    history: prevDataPrices,
    show: true,
    color: 'green',
  };

  const dataset1: DataSet[] = [];
  dataset1.push(current);
  dataset1.push(previous);

  if (spotVixArray[0] > 0) {
    const spot: DataSet = {
      name: 'Spot VIX',
      history: spotVixValues,
      show: true,
      color: 'red',
    };
    dataset1.push(spot);
  }

  let minPrice = 100000;
  let maxPrice = 0;
  dataset1.forEach((series) => {
    const minPriceI = d3.min(series.history, (d) => d.price) ?? 100000;
    const maxPriceI = d3.max(series.history, (d) => d.price) ?? 0;
    if (minPriceI < minPrice)
      minPrice = minPriceI;
    if (maxPriceI > maxPrice)
      maxPrice = maxPriceI;
  });
  const maxDays = currDataDaysArray[nCurrData - 1];

  creatingChart(dataset1, json.titleCont, minPrice, maxPrice, maxDays);
}

function creatingChart(data, titleCont, minPrice, maxPrice, maxDays) {
  const svg = d3.select('#chart1');

  // Define margins, dimensions, and some line colors
  const margin = { top: 40, right: 50, bottom: 35, left: 100 };
  const width = 800 - margin.left - margin.right;
  const height = 400 - margin.top - margin.bottom;

  // Define the scales and tell D3 how to draw the line
  const x = d3
      .scaleLinear()
      .domain([0, maxDays + 10])
      .range([0, width]);
  const y = d3
      .scaleLinear()
      .domain([minPrice * 0.87, maxPrice * 1.13])
      .range([height, 0]);
  const line = d3
      .line()
      .x((d : any) => x(d.days))
      .y((d : any) => y(d.price));
  svg.selectAll('*').remove();
  const chart = d3
      .select('svg')
      .append('g')
      .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

  const tooltip = d3.select('#tooltip');
  const tooltipLine = chart.append('line');

  // Add the axes and a title
  const xAxis = d3.axisBottom(x).tickFormat(d3.format('.4'));
  const yAxis = d3.axisLeft(y).tickFormat(d3.format('$.4'));
  chart.append('g').call(yAxis);
  chart
      .append('g')
      .attr('transform', 'translate(0,' + height + ')')
      .call(xAxis);
  chart.append('text').html(titleCont).attr('x', 200);

  // text label for the x axis
  chart
      .append('text')
      .attr('transform', 'translate(' + width / 2 + ' ,' + (height + 30) + ')')
      .style('text-anchor', 'middle')
      .style('font-size', '1.2rem')
      .text('Days until expiration');

  // Load the data and draw a chart
  let numSeries = 0;
  let series;
  data.forEach((d) => {
    series = d;

    chart
        .append('path')
        .attr('fill', 'none')
        .attr('stroke', d.color)
        .attr('stroke-width', 2)
        .datum(d.history)
        .attr('d', line);

    chart
        .append('text')
        .html(d.name)
        .style('font-size', '1.4rem')
        .attr('fill', d.color)
        .attr('alignment-baseline', 'middle')
        .attr('x', width - 100)
        .attr('dx', '.5em')
        .attr('y', 30 + 20 * numSeries);

    chart
        .selectAll('myCircles')
        .data(d.history)
        .enter()
        .append('circle')
        .attr('fill', d.color)
        .attr('stroke', 'none')
        .attr('cx', (e: any) => x(e.days))
        .attr('cy', (e: any) => y(e.price))
        .attr('r', 4);

    numSeries = numSeries + 1;
  });

  chart
      .append('rect')
      .attr('width', width)
      .attr('height', height)
      .attr('opacity', 0)
      .on('mousemove', drawTooltip)
      .on('mouseout', removeTooltip);

  function removeTooltip() {
    if (tooltip)
      tooltip.style('display', 'none');
    if (tooltipLine)
      tooltipLine.attr('stroke', 'none');
  }

  function drawTooltip(event: any) {
    const daysArray = new Array();
    series.history.forEach((element) => {
      daysArray.push(element.days);
    });
    const mousePos = d3.pointer(event);
    const xCCL = event.clientX;
    const yCCL = event.clientY;
    const xCoord = x.invert(mousePos[0]);
    const yCoord = mousePos[1];

    const closestXCoord = daysArray.sort(
        (a, b) => Math.abs(xCoord - a) - Math.abs(xCoord - b)
    )[0];
    const closestYCoord = data[0].history.find((h) => h.days === closestXCoord)
        .price;
    const closestInvX = (closestXCoord / (maxDays + 10)) * width;
    const ttX = xCCL - mousePos[0] + closestInvX;
    const ttY = yCCL - yCoord + y(closestYCoord);

    const ttTextArray = new Array();
    ttTextArray.push(
        '<i>Number of days till expiration: ' + closestXCoord + '</i><br>'
    );
    data.forEach((d) => {
      const seriesText =
        d.name +
        ': $' +
        d.history.find((h) => h.days === closestXCoord).price +
        '<br>';
      ttTextArray.push(seriesText);
    });

    tooltipLine
        .attr('stroke', 'black')
        .attr('x1', x(closestXCoord))
        .attr('x2', x(closestXCoord))
        .attr('y1', 0 + 10)
        .attr('y2', height);

    tooltip
        .html(ttTextArray.join(''))
        .style('display', 'block')
        .style('left', ttX + 10)
        .style('top', ttY + 25)
        .selectAll()
        .data(series)
        .enter()
        .append('div')
        .style('color', (d : any) => d.color);
  }
}

document.addEventListener('DOMContentLoaded', (event) => {
  console.log(
      'DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.'
  );
});

window.onload = function onLoadWindow() {
  console.log(
      'SqCore: window.onload() BEGIN. All CSS, and images were downloaded.'
  ); // images are loaded at this time, so their sizes are known

  getDocElementById('VIXimage').onclick = onImageClickVIX;
  getDocElementById('OILimage').onclick = onImageClickOIL;
  getDocElementById('GASimage').onclick = onImageClickGAS;

  // const logger: NGXLogger = new NGXLogger({
  //   level: NgxLoggerLevel.INFO,
  //   serverLogLevel: NgxLoggerLevel.ERROR,
  //   serverLoggingUrl: '/JsLog',
  // });
  // logger.trace('A simple trace() test message to NGXLogger');
  // logger.log('A simple log() test message to NGXLogger');

  console.log('SqCore: window.onload() END.');
};

console.log('SqCore: Script END');