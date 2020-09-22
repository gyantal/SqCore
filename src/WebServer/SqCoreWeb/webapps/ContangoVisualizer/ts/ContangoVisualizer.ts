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
      if (!response.ok) {
        return Promise.reject(new Error('Invalid response status'));
      }
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
      // const jsonToStr = JSON.stringify(json).substr(0, 60) + '...';
      onDataReceived(json);
    }
  );
}

function onDataReceived(json: any) {
    console.log(json.dataSource);

    // Creating first row (dates) of webpage.
    const divTitleCont = document.getElementById('idTitleCont') as HTMLElement;
    const divTimeNow = document.getElementById('idTimeNow') as HTMLElement;
    const divLiveDataDate = document.getElementById('idLiveDataDate') as HTMLElement;
    const divLiveDataTime = document.getElementById('idLiveDataTime') as HTMLElement;
    const divMyLink = document.getElementById('myLink') as HTMLElement;
    // const divFirstDataDate = document.getElementById('idFirstDataDate') as HTMLTableElement;
    // const divLastDataDate = document.getElementById('idLastDataDate') as HTMLTableElement;


    divTitleCont.innerText = json.titleCont;
    divTimeNow.innerText = 'Current time: ' + json.timeNow;
    divLiveDataDate.innerText = 'Last data time: ' + json.liveDataDate;
    divLiveDataTime.innerText = json.liveDataTime;
    divMyLink.innerHTML = '<a href="' + json.dataSource + '" target="_blank">Data Source</a>';


    creatingTables(json);

    // // Setting charts visible after getting data.
    // document.getElementById('inviCharts').style.visibility = 'visible';
}

function creatingTables(json) {

  // const monthList = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

  // Creating JavaScript data arrays by splitting.
  const currDataArray = json.currDataVec.split(',');
  const currDataDaysArray = json.currDataDaysVec.split(',');
  const prevDataArray = json.prevDataVec.split(',');
  const currDataDiffArray = json.currDataDiffVec.split(',');
  const currDataPercChArray = json.currDataPercChVec.split(',');
  const spotVixArray = json.spotVixVec.split(',');


  // Creating the HTML code of current table.
  let currTableMtx = '<table class="currData"><tr align="center"><td>Future Prices</td><td>F1</td><td>F2</td><td>F3</td><td>F4</td><td>F5</td><td>F6</td><td>F7</td><td>F8</td></tr><tr align="center"><td align="left">Current</td>';
  for (let i = 0; i < 8; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx += '<td>' + '---' + '</td>';
      } else {
          currTableMtx += '<td>' + currDataArray[i] + '</td>';
      }
  }

  currTableMtx += '</tr><tr align="center"><td align="left">Previous Close</td>';
  for (let i = 0; i < 8; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx += '<td>' + '---' + '</td>';
      } else {
          currTableMtx += '<td>' + prevDataArray[i] + '</td>';
      }
  }
  currTableMtx += '</tr><tr align="center"><td align="left">Daily Abs. Change</td>';
  for (let i = 0; i < 8; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx += '<td>' + '---' + '</td>';
      } else {
          currTableMtx += '<td>' + currDataDiffArray[i] + '</td>';
      }
  }
  currTableMtx += '</tr><tr align="center"><td align="left">Daily % Change</td>';
  for (let i = 0; i < 8; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx += '<td>' + '---' + '</td>';
      } else {
          currTableMtx += '<td>' + (currDataPercChArray[i] * 100).toFixed(2) + '%</td>';
      }
  }
  currTableMtx += '</tr><tr align="center"><td align="left">Cal. Days to Expiration</td>';
  for (let i = 0; i < 8; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx += '<td>' + '---' + '</td>';
      } else {
          currTableMtx += '<td>' + currDataDaysArray[i] + '</td>';
      }
  }
  currTableMtx += '</tr></table>';

  let currTableMtx3 = '<table class="currData"><tr align="center"><td>Contango</td><td>F2-F1</td><td>F3-F2</td><td>F4-F3</td><td>F5-F4</td><td>F6-F5</td><td>F7-F6</td><td>F8-F7</td><td>F7-F4</td><td>(F7-F4)/3</td></tr><tr align="center"><td align="left">Monthly Contango %</td><td><strong>' + (currDataArray[8] * 100).toFixed(2) + '%</strong></td>';
  for (let i = 20; i < 27; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx3 += '<td>' + '---' + '</td>';
      } else {
          currTableMtx3 += '<td>' + (currDataArray[i] * 100).toFixed(2) + '%</td>';
      }
  }
  currTableMtx3 += '<td><strong>' + (currDataArray[27] * 100).toFixed(2) + '%</strong></td>';
  currTableMtx3 += '</tr><tr align="center"><td align="left">Difference</td>';
  for (let i = 10; i < 19; i++) {
      if (currDataArray[i] === 0) {
          currTableMtx3 += '<td>' + '---' + '</td>';
      } else {
          currTableMtx3 += '<td>' + (currDataArray[i] * 100 / 100).toFixed(2) + '</td>';
      }
  }
  currTableMtx3 += '</tr></table>';

  // "Sending" data to HTML file.
  const currTableMtx2 = document.getElementById('idCurrTableMtx') as HTMLTableElement;
  // const currTableMtx2 = document.getElementById('idCurrTableMtx');
  currTableMtx2.innerHTML = currTableMtx;
  const currTableMtx4 = document.getElementById('idCurrTableMtx3') as HTMLTableElement;
  currTableMtx4.innerHTML = currTableMtx3;

  const nCurrData = 7;
  const currDataPrices = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
      const currDataPricesRows = new Array(2);
      currDataPricesRows[0] = currDataDaysArray[i];
      currDataPricesRows[1] = currDataArray[i];
      currDataPrices[i] = currDataPricesRows;
  }

  const prevDataPrices = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
      const prevDataPricesRows = new Array(2);
      prevDataPricesRows[0] = currDataDaysArray[i];
      prevDataPricesRows[1] = prevDataArray[i];
      prevDataPrices[i] = prevDataPricesRows;
  }

  const spotVixValues = new Array(nCurrData);
  for (let i = 0; i < nCurrData; i++) {
      const spotVixValuesRows = new Array(2);
      spotVixValuesRows[0] = currDataDaysArray[i];
      spotVixValuesRows[1] = spotVixArray[i];
      spotVixValues[i] = spotVixValuesRows;
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
