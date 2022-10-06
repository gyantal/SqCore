import './../css/main.css';
import { sqLineChartGenerator } from '../../../TsLib/sq-common/sqlineChrt';
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

function getQueryVariable(variable) {
  const query = window.location.search.substring(1);
  const vars = query.split('&');
  for (let i = 0; i < vars.length; i++) {
    const pair = vars[i].split('=');
    if (pair[0] == variable) return pair[1];
  }
  return ('JUVE');
}

function choseall(nameS) {
  const x = document.getElementsByName(nameS) as any;
  let y = 0;
  for (let i = 0; i < x.length; i++) {
    if ((document.getElementsByName(nameS) as any)[i].checked)
      y += 1;
  }
  for (let i = 0; i < x.length; i++) {
    if (y == x.length) {
      (document.getElementsByName(nameS) as any)[i].checked = true;
      (document.getElementsByName(nameS) as any)[i].click();
    } else {
      if ((document.getElementsByName(nameS) as any)[i].checked != true) {
        (document.getElementsByName(nameS) as any)[i].checked = false;
        (document.getElementsByName(nameS) as any)[i].click();
      }
    }
  }
}

function checkAll(ele) {
  const x = document.getElementsByClassName('szpari') as any;
  let y = 0;
  for (let i = 0; i < x.length; i++) {
    if ((x as any)[i].checked)
      y += 1;
  }
  for (let i = 0; i < x.length; i++) {
    if (y == x.length) {
      (x as any)[i].checked = true;
      (document.getElementsByClassName('szpari')as any)[i].click();
    } else {
      if ((x as any)[i].checked != true) {
        (x as any)[i].checked = false;
        (document.getElementsByClassName('szpari')as any)[i].click();
      }
    }
  }
}

const selectedTickers : string[] = ['SPY', 'QQQ', 'TLT'];
// Under development - Daya
function onImageClick(json: any) {
  getDocElementById('vixBtn').onclick = () => choseall('volA');
  getDocElementById('impEtpBtn').onclick = () => choseall('etpA');
  getDocElementById('gameChngBtn').onclick = () => choseall('gchA');
  getDocElementById('globalAssetsBtn').onclick = () => choseall('gmA');
  getDocElementById('selectAllBtn').onclick = checkAll;
  getDocElementById('updateAllBtn').onclick = function() {
    const selectedTickers: string[] = Array.from(document.querySelectorAll('input[type=checkbox]:checked') as NodeListOf<Element>).map((r: any) => r.id);
    processingTables(json, selectedTickers);
  };
}

function getDocElementById(id: string): HTMLElement {
  return (document.getElementById(id) as HTMLElement); // type casting assures it is not null for the TS compiler. (it can be null during runtime)
}


document.addEventListener('DOMContentLoaded', (event) => {
  console.log('DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.');
});

window.onload = function onLoadWindow() {
  console.log('SqCore: window.onload() BEGIN. All CSS, and images were downloaded.'); // images are loaded at this time, so their sizes are known

  const commo = getQueryVariable('lbp');
  AsyncStartDownloadAndExecuteCbLater('/VolatilityDragVisualizer?commo=' + commo, (json: any) => {
    onReceiveData(json);
    onImageClick(json);
    processingTables(json, selectedTickers);
  });

  function onReceiveData(json: any) {
    if (json == 'Error') {
      const divErrorCont = getDocElementById('idErrorCont');
      divErrorCont.innerHTML = 'Error during downloading data. Please, try again later!';
      getDocElementById('errorMessage').style.visibility='visible';
      getDocElementById('volDragChrtVisibility').style.visibility = 'hidden';
      getDocElementById('pctChgLookbackChrtVisibility').style.visibility = 'hidden';
      return;
    }
    getDocElementById('titleCont').innerHTML = '<small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small>';
    getDocElementById('requestTime').innerText = json.requestTime;
    getDocElementById('lastDataTime').innerText = json.lastDataTime;

    // processingVolDragData(json);
    processingVolDragData(json);
    // Setting charts visible after getting data.
    getDocElementById('volDragChrtVisibility').style.visibility = 'visible';
    getDocElementById('pctChgLookbackChrtVisibility').style.visibility = 'visible';
  }


  function processingVolDragData(json: any): void {
    const volAssetNamesArray = json.volAssetNames.split(', ');
    const etpAssetNamesArray = json.etpAssetNames.split(', ');
    const gchAssetNamesArray = json.gchAssetNames.split(', ');
    const gmAssetNamesArray = json.gmAssetNames.split(', ');
    // const defCheckedListArray = json.defCheckedList.split(', ');

    let chBxs = '<p class="left"><button id="vixBtn" class="volBtn" title="Volatility ETPs"/></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < volAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="volA" id="' + volAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + volAssetNamesArray[iAssets].split('_')[0] + '">' + volAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button id="impEtpBtn" class="volBtn" title="Important ETPs"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < etpAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="etpA" id="' + etpAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + etpAssetNamesArray[iAssets].split('_')[0] + '">' + etpAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button id="gameChngBtn" class="volBtn" title="GameChanger Stocks"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < gchAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="gchA" id="' + gchAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + gchAssetNamesArray[iAssets].split('_')[0] + '">' + gchAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button id="globalAssetsBtn" class="volBtn" title="Global Assets"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < gmAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="gmA" id="' + gmAssetNamesArray[iAssets] + '" /><a target="_blank" href="https://finance.yahoo.com/quote/' + gmAssetNamesArray[iAssets].split('_')[0] + '">' + gmAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '</p ><p class="center"><button id="selectAllBtn" class="volBtn" title="Select/Deselect All"/></button>&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;<button id="updateAllBtn" class="volBtn" title="Update Charts and Tables" id=\'update_all\'></button></p> ';

    const checkBoxes = getDocElementById('idChBxs');
    checkBoxes.innerHTML = chBxs;

    // default checkbox method
    const inputCheck = document.getElementsByTagName('input') as HTMLCollectionOf<HTMLInputElement>;
    for (let i = 0; i < inputCheck.length; i++) {
      for (let j = 0; j < selectedTickers.length; j++) {
        if (inputCheck[i].id === selectedTickers[j])
          inputCheck[i].checked = true;
      }
      inputCheck[i].checked;
    }
  }
  console.log('SqCore: window.onload() END.');
};


function processingTables(json: any, selectedTickers: string[]) {
  //  Creating data for tables
  const assetNamesArray = json.assetNames.split(', ');
  const dailyDatesArray = json.quotesDateVector.split(', ');

  const volLBPeriod = json.volLBPeri;

  const dailyVolDragsTemp = json.dailyVolDrags.split('ß ');
  const dailyVolDragsMtx: any[] = [];
  for (let i = 0; i < dailyVolDragsTemp.length; i++)
    dailyVolDragsMtx[i] = dailyVolDragsTemp[i].split(',');

  const dailyVIXMasArray = json.dailyVIXMas.split(', ');
  const yearListArray = json.yearList.split(', ');
  const yearMonthListArray = json.yearMonthList.split(', ');

  const yearlyAvgsTemp = json.yearlyAvgs.split('ß ');
  const yearlyAvgsMtx: any[] = [];
  for (let i = 0; i < yearlyAvgsTemp.length; i++)
    yearlyAvgsMtx[i] = yearlyAvgsTemp[i].split(',');

  for (let i = 0; i < yearlyAvgsTemp.length; i++) {
    for (let j = 0; j < yearlyAvgsMtx[0].length; j++) {
      if (yearlyAvgsMtx[i][j] == ' 0%')
        yearlyAvgsMtx[i][j] = '---';
    }
  }

  const monthlyAvgsTemp = json.monthlyAvgs.split('ß ');
  const monthlyAvgsMtx: any[] = [];
  for (let i = 0; i < monthlyAvgsTemp.length; i++)
    monthlyAvgsMtx[i] = monthlyAvgsTemp[i].split(',');

  for (let i = 0; i < monthlyAvgsTemp.length; i++) {
    for (let j = 0; j < monthlyAvgsMtx[0].length; j++) {
      if (monthlyAvgsMtx[i][j] == ' 0%')
        monthlyAvgsMtx[i][j] = '---';
    }
  }

  const yearlyVIXAvgsArray = json.yearlyVIXAvgs.split(', ');
  const monthlyVIXAvgsArray = json.monthlyVIXAvgs.split(', ');
  const yearlyCountsArray = json.yearlyCounts.split(', ');
  const monthlyCountsArray = json.monthlyCounts.split(', ');
  const totDays = json.noTotalDays;
  const vixAvgTot = json.vixAvgTotal;
  const volDragsAvgsTotalArray = json.volDragsAvgsTotalVec.split(', ');
  const noColumns = assetNamesArray.length + 3;

  const noInnerYears = yearListArray.length - 2;
  const noLastYearMonths = yearMonthListArray.length - 10 - noInnerYears * 12;

  const retHistLBPeriods = json.retLBPeris.split(', ');
  const retHistLBPeriodsNoS = json.retLBPerisNo.split(', ');
  const retHistLBPeriodsNo: any[] = [];
  for (let i = 0; i < retHistLBPeriodsNoS.length; i++) retHistLBPeriodsNo[i] = parseInt(retHistLBPeriodsNoS[i]);
  // const retHistChartLength = json.retHistLBPeri;

  const histRetsTemp = json.histRetMtx.split('ß ');
  const histRetsMtx: any[] = [];
  for (let i = 0; i < histRetsTemp.length; i++)
    histRetsMtx[i] = histRetsTemp[i].split(',');

  const histRets2ChartsTemp = json.histRet2Chart.split('ß ');
  const histRets2ChartsMtx: any[] = [];
  for (let i = 0; i < histRets2ChartsTemp.length; i++)
    histRets2ChartsMtx[i] = histRets2ChartsTemp[i].split(',');

  // Creating the HTML code of tables.

  let currMonthlyVolatilityTbl = '<table class="volHistData"><tr align="center"><td colspan="' + (noColumns - 1) + '" bgcolor="#66CCFF"><b>Current Monthly Volatility Drag</b></td></tr><tr align="center"><td bgcolor="#66CCFF">Date</td><td class="first_name" bgcolor="#66CCFF">VIX MA(' + volLBPeriod + ')</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        currMonthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '" bgcolor="#66CCFF">' + assetNamesArray[i] + '</td>';
    }
  }

  currMonthlyVolatilityTbl += '</tr>';
  currMonthlyVolatilityTbl += '<tr align="center"><td>' + dailyDatesArray[dailyDatesArray.length - 1] + '</td>';
  currMonthlyVolatilityTbl += '<td class="first_name">' + dailyVIXMasArray[dailyVIXMasArray.length - 1] + '</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        currMonthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '">' + dailyVolDragsMtx[dailyVolDragsMtx.length - 1][i] + '</td>';
    }
  }

  currMonthlyVolatilityTbl += '</tr></table>';

  let monthlyVolatilityTbl = '<table class="volDataYearsNMonths"><thead><tr align="center" ><td colspan="' + noColumns + '" bgcolor="#66CCFF"><b>Monthly Volatility Drag by Years and Months</b></td></tr><tr align="center"><td bgcolor="#66CCFF"><span class="years">Only Years</span> / <span class="years">Years+Months</span></td><td bgcolor="#66CCFF">No. Days</td><td bgcolor="#66CCFF">VIX MA(' + volLBPeriod + ')</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        monthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '" bgcolor="#66CCFF">' + assetNamesArray[i] + '</td>';
    }
  }
  monthlyVolatilityTbl += '</tr></thead>';
  monthlyVolatilityTbl += '<tbody><tr class="parent"><td><span class="years">' + yearListArray[0] + '</span></td><td>' + yearlyCountsArray[0] + '</td><td>' + yearlyVIXAvgsArray[0] + '</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        monthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '">' + yearlyAvgsMtx[0][i] + '</td>';
    }
  }
  for (let i = 1; i < 10; i++) {
    monthlyVolatilityTbl += '<tr class="child"><td align="right"><i>' + yearMonthListArray[i] + '&emsp;</i></td><td><i>' + monthlyCountsArray[i] + '</i></td><td><i>' + monthlyVIXAvgsArray[i] + '</i></td>';
    for (let j = 0; j < assetNamesArray.length; j++) {
      for (let k = 0; k < selectedTickers.length; k++) {
        if (assetNamesArray[j] === selectedTickers[k])
          monthlyVolatilityTbl += '<td class="' + assetNamesArray[j] + '"><i>' + monthlyAvgsMtx[i][j] + '</i></td>';
      }
    }

    monthlyVolatilityTbl += '</tr>';
  }
  for (let k = 0; k < noInnerYears; k++) {
    monthlyVolatilityTbl += '<tr class="parent"><td><span class="years">' + yearListArray[k + 1] + '</span></td><td>' + yearlyCountsArray[k + 1] + '</td><td>' + yearlyVIXAvgsArray[k + 1] + '</td>';
    for (let i = 0; i < assetNamesArray.length; i++) {
      for (let j = 0; j < selectedTickers.length; j++) {
        if (assetNamesArray[i] === selectedTickers[j])
          monthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '">' + yearlyAvgsMtx[k + 1][i] + '</td>';
      }
    }
    for (let i = 0; i < 12; i++) {
      monthlyVolatilityTbl += '<tr class="child"><td align="right"><i>' + yearMonthListArray[10 + k * 12 + i] + '&emsp;</i></td><td><i>' + monthlyCountsArray[10 + k * 12 + i] + '</i></td><td><i>' + monthlyVIXAvgsArray[10 + k * 12 + i] + '</i></td>';
      for (let j = 0; j < assetNamesArray.length; j++) {
        for (let l = 0; l < selectedTickers.length; l++) {
          if (assetNamesArray[j] === selectedTickers[l])
            monthlyVolatilityTbl += '<td class="' + assetNamesArray[j] + '"><i>' + monthlyAvgsMtx[10 + k * 12 + i][j] + '</i></td>';
        }
      }
      monthlyVolatilityTbl += '</tr>';
    }
  }
  monthlyVolatilityTbl += '<tr class="parent" id="lastYearT"><td><span class="years">' + yearListArray[yearListArray.length - 1] + '</span></td><td>' + yearlyCountsArray[yearListArray.length - 1] + '</td><td>' + yearlyVIXAvgsArray[yearListArray.length - 1] + '</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        monthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '">' + yearlyAvgsMtx[yearListArray.length - 1][i] + '</td>';
    }
  }
  for (let i = 0; i < noLastYearMonths; i++) {
    monthlyVolatilityTbl += '<tr class="child"><td align="right"><i>' + yearMonthListArray[10 + noInnerYears * 12 + i] + '&emsp;</i></td><td><i>' + monthlyCountsArray[10 + noInnerYears * 12 + i] + '</i></td><td><i>' + monthlyVIXAvgsArray[10 + noInnerYears * 12 + i] + '</i></td>';
    for (let j = 0; j < assetNamesArray.length; j++) {
      for (let k = 0; k < selectedTickers.length; k++) {
        if (assetNamesArray[j] === selectedTickers[k])
          monthlyVolatilityTbl += '<td class="' + assetNamesArray[j] + '"><i>' + monthlyAvgsMtx[10 + noInnerYears * 12 + i][j] + '</i></td>';
      }
    }
    monthlyVolatilityTbl += '</tr>';
  }
  monthlyVolatilityTbl += '<tr class="parent" style="cursor: text"><td><span class="total">Total 2004-' + yearListArray[yearListArray.length - 1] + '</span></td><td>' + totDays + '</td><td>' + vixAvgTot + '</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        monthlyVolatilityTbl += '<td class="' + assetNamesArray[i] + '">' + volDragsAvgsTotalArray[i] + '</td>';
    }
  }
  monthlyVolatilityTbl += '</tr></tbody></table>';

  let recentperformanceTbl = '<table class="volHistData"><tr align="center"><td colspan="' + (noColumns - 2) + '" bgcolor="#66CCFF"><b>Recent Performance of Stocks - Percent Changes of Prices</b></td></tr><tr align="center"><td bgcolor="#66CCFF"></td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        recentperformanceTbl += '<td class="' + assetNamesArray[i] + '" bgcolor="#66CCFF">' + assetNamesArray[i] + '</td>';
    }
  }
  recentperformanceTbl += '</tr>';
  for (let j = 0; j < retHistLBPeriods.length; j++) {
    recentperformanceTbl += '<tr align="center"><td>' + retHistLBPeriods[j] + '</td>';
    for (let i = 0; i < assetNamesArray.length; i++) {
      for (let k = 0; k < selectedTickers.length; k++) {
        if (assetNamesArray[i] === selectedTickers[k])
          recentperformanceTbl += '<td class="' + assetNamesArray[i] + '">' + histRetsMtx[j][i] + '</td>';
      }
    }
    recentperformanceTbl += '</tr>';
  }
  recentperformanceTbl += '</table>';


  let volatilityHistoryTbl = '<table id="volHistTbl" class="volHistData"><thead><tr align="center"><td colspan="' + (noColumns - 1) + '" bgcolor="#66CCFF"><b>Monthly Volatility Drag History</b></td></tr><tr align="center"><td bgcolor="#66CCFF"><select id="lookbackHistTbl"><option value="5">1-Week</option><option value="21" selected>1-Month</option><option value="63">3-Month</option><option value="126">6-Month</option><option value="252">1-Year</option><option value="' + dailyDatesArray.length + '">All</option></select ></td><td bgcolor="#66CCFF">VIX MA(' + volLBPeriod + ')</td>';
  for (let i = 0; i < assetNamesArray.length; i++) {
    for (let j = 0; j < selectedTickers.length; j++) {
      if (assetNamesArray[i] === selectedTickers[j])
        volatilityHistoryTbl += '<td class="' + assetNamesArray[i] + '" bgcolor="#66CCFF">' + assetNamesArray[i] + '</td>';
    }
  }

  volatilityHistoryTbl += '</tr></thead><tbody>';
  for (let j = dailyVolDragsMtx.length - 1; j >= 0; j--) {
    volatilityHistoryTbl += '<tr align="center"><td>' + dailyDatesArray[j] + '</td>';
    volatilityHistoryTbl += '<td>' + dailyVIXMasArray[j] + '</td>';

    for (let i = 0; i < assetNamesArray.length; i++) {
      for (let k = 0; k < selectedTickers.length; k++) {
        if (assetNamesArray[i] === selectedTickers[k])
          volatilityHistoryTbl += '<td class="' + assetNamesArray[i] + '">' + dailyVolDragsMtx[j][i] + '</td>';
      }
    }
    volatilityHistoryTbl += '</tr>';
  }
  volatilityHistoryTbl += '</tbody></table>';

  // "Sending" data to HTML file.
  const CurrentMonthlyMtx = getDocElementById('idCurrMonthly');
  CurrentMonthlyMtx.innerHTML = currMonthlyVolatilityTbl;
  const monthlyVolatilityMtx = getDocElementById('idMonthlyVolatility');
  monthlyVolatilityMtx.innerHTML = monthlyVolatilityTbl;
  const recentPerformanceMtx = getDocElementById('idRecentPerformance');
  recentPerformanceMtx.innerHTML = recentperformanceTbl;
  const volatilityHistoryMtx = getDocElementById('idVolatilityHistory');
  volatilityHistoryMtx.innerHTML = volatilityHistoryTbl;

  // under development - Daya
  function SetVisibilityOfDailyHistory() {
    const volLBPeriod = parseInt((document.getElementById('lookbackHistTbl') as HTMLSelectElement).value);
    const volHistTbl = (getDocElementById('volHistTbl') as HTMLTableElement).rows;
    const min = 0; const max = volLBPeriod + 2;
    for (let i = 0; i < volHistTbl.length; i ++) {
      if (i >= min && i < max)
        volHistTbl[i].style.display = 'block';
      else
        volHistTbl[i].style.display = 'none'; // style.display = 'hidden
    }
  };

  getDocElementById('lookbackHistTbl').onchange = SetVisibilityOfDailyHistory;
  SetVisibilityOfDailyHistory();

  const monthlyVolTblParent = document.querySelectorAll('.volDataYearsNMonths tr');
  function toggleVolDataYearsNMonths(event) {
    const volDragMonthlyHistTblRws = document.querySelectorAll('.volDataYearsNMonths tr');
    const clickedParentRowIdx = event.currentTarget.rowIndex;
    for (let j = clickedParentRowIdx + 1; j < clickedParentRowIdx + 13; j++) {
      if (event.currentTarget.className === 'child' || event.currentTarget.className === '')
        break;
      if (volDragMonthlyHistTblRws[j].className !== 'parent')
        volDragMonthlyHistTblRws[j].classList.toggle('child');
      else
        break; // no further processing is required once we reach the next parent
    }
  };
  for (let i = 0; i < monthlyVolTblParent.length; i++)
    monthlyVolTblParent[i].addEventListener('click', toggleVolDataYearsNMonths);

  // Generating teh Charts
  const lookbackValue = 20;
  const lookbackPeriod = retHistLBPeriodsNo.indexOf(lookbackValue);
  const lengthSubSums: any[] = [];
  lengthSubSums[0] = 0;
  lengthSubSums[1] = retHistLBPeriodsNo[0];
  for (let i = 2; i < retHistLBPeriodsNo.length + 1; i++)
    lengthSubSums[i] = lengthSubSums[i - 1] + retHistLBPeriodsNo[i - 1];

  const nCurrDataVD = dailyDatesArray.length;
  const noAssets = selectedTickers.length;

  const assChartMtx: any[] = [];
  const tickersChecked = document.querySelectorAll('input[type=checkbox]:checked') as NodeListOf<Element>;

  for (let dayInd = 0; dayInd < nCurrDataVD; dayInd++) {
    const date = dailyDatesArray[dayInd];
    const dayDataArr = [date];
    for (let i = 0; i < tickersChecked.length; i++) {
      const tickerInd = assetNamesArray.indexOf(tickersChecked[i].id);
      dayDataArr.push(dailyVolDragsMtx[dayInd][tickerInd]);
    }
    assChartMtx.push(dayDataArr);
  }

  const xLabel: string = 'Dates';
  const yLabel: string = 'Percentage Change';
  const yScaleTickFormat: string = '%';
  const isDrawCricles: boolean = false;
  d3.selectAll('#volDragChrt > *').remove();
  const volDragChrt = getDocElementById('volDragChrt');
  const lineChrtTooltip = getDocElementById('tooltipChart');
  sqLineChartGenerator(noAssets, nCurrDataVD, selectedTickers, assChartMtx, xLabel, yLabel, yScaleTickFormat, volDragChrt, lineChrtTooltip, isDrawCricles);

  getDocElementById('idChartLength').innerHTML = '<strong>Percentage Changes of Prices in the Last &emsp;<select id="lookbackHistChrt"><option value="1">1 Day</option><option value="3">3 Days</option><option value="5">1 Week</option><option value="10">2 Weeks</option><option value="20" selected>1 Month</option><option value="63">3 Months</option><option value="126">6 Months</option><option value="252">1 Year</option>' + retHistLBPeriods[lookbackPeriod] + '</strong >';
  showPctChgChrt(lookbackPeriod, lookbackValue);

  getDocElementById('lookbackHistChrt').onchange = function() {
    const lookbackValue = parseInt((document.getElementById('lookbackHistChrt') as HTMLSelectElement).value);
    const lookbackPeriod = retHistLBPeriodsNo.indexOf(lookbackValue);
    showPctChgChrt(lookbackPeriod, lookbackValue);
  };

  // Declaring data sets to charts.
  function showPctChgChrt(lookbackPeriod: number, lookbackValue: number) {
    let chartStart: number;
    lookbackPeriod == 0 ? chartStart = lengthSubSums[lookbackPeriod] + 1 : chartStart = lengthSubSums[lookbackPeriod];

    const nCurrData = lookbackValue + 1;
    const lookbackChartMtx: any[] = [];
    for (let dayInd = 0; dayInd < nCurrData; dayInd++) { // when '3 days' selected, we show 4 days (data points) on the chart
      const date = dailyDatesArray[dailyDatesArray.length - nCurrData + dayInd];
      const dayDataArr = [date];
      for (let i = 0; i < tickersChecked.length; i++) {
        const tickerInd = assetNamesArray.indexOf(tickersChecked[i].id);
        dayInd == 0 ? dayDataArr.push(0) : dayDataArr.push((histRets2ChartsMtx[chartStart - 1 + dayInd][tickerInd])); // As the Day0 data (which is always 0%) is not in the received data from the server, insert 0%, 0%, ... 0% as the first item
      }
      lookbackChartMtx.push(dayDataArr);
    }
    d3.selectAll('#pctChgLookbackChrt > *').remove();
    const pctChgChrt = getDocElementById('pctChgLookbackChrt');
    const isDrawCricles1: boolean = true;
    sqLineChartGenerator(noAssets, nCurrData, selectedTickers, lookbackChartMtx, xLabel, yLabel, yScaleTickFormat, pctChgChrt, lineChrtTooltip, isDrawCricles1);
  }
}
console.log('SqCore: Script END');