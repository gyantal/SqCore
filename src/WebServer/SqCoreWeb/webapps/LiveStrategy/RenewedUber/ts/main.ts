import './../css/main.css';

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
    // const jsonToStr = JSON.stringify(json).substr(0, 60) + '...';
    // getDocElementById('DebugDataArrivesHere').innerText = '***"' + json[0].stringData + '"***';
    onReceiveRenewedUber(json);
  });

  console.log('SqCore: window.onload() END.');
};

function onReceiveRenewedUber(json: any) {
  getDocElementById('titleCont').innerHTML = '<small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small>';
  getDocElementById('requestTime').innerText = json.requestTime;
  getDocElementById('lastDataTime').innerText = json.lastDataTime;
  getDocElementById('currentPV').innerHTML = 'Current PV: <span class="pv">$' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank" >these current positions</a> updated on ' + json.currentPVDate + ')';
  // if (json.dailyProfSig !== 'N/A')
  //   getDocElementById('dailyProfit').innerHTML = '<b>Daily Profit/Loss: <span class="' + json.dailyProfString + '">' + json.dailyProfSig + json.dailyProfAbs + '</span></b>';
  getDocElementById('idCurrentEvent').innerHTML = 'Next trading day will be <span class="stci"> ' + json.currentEventName + '</span>, <div class="tooltip">used STCI is <span class="stci">' + json.currentSTCI + '</span><span class="tooltiptext">Second (third) month VIX futures divided by front (second) month VIX futures minus 1, with more (less) than 5-days until expiration.</span></div > and used VIX is <span class="stci">' + json.currentVIX + '</span>, thus leverage will be <span class="stci">' + json.currentFinalWeightMultiplier + '.</span >';

  renewedUberInfoTbls(json);
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

  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">' + json.nextTradingDay + '</td>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[0] + '">' + nextPosValArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr>';
  for (let i = 0; i < assetNames2Array.length; i++)
    chngInPosTbl += '<td bgcolor="#' + prevEventColors[0] + '">' + nextPosNumArray[i] + '</td>';


  chngInPosTbl += '</tr > <tr align="center"><td align="center" rowspan="2" bgcolor="#FF6633">' + json.currPosDate + '</td>';
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

  const currTableMtx7 = '<table class="currData2"><tr><td bgcolor="#32CD32">FOMC Bullish Day</td><td bgcolor="#7CFC00">Holiday Bullish Day</td><td bgcolor="#00FA9A">Other Bullish Event Day</td></tr><tr><td bgcolor="#c24f4f">FOMC Bearish Day</td><td bgcolor="#d46a6a">Holiday Bearish Day</td><td bgcolor="#ed8c8c">Other Bearish Event Day</td></tr><tr><td bgcolor="#C0C0C0">Non-Playable Other Event Day</td><td bgcolor="#00FFFF">STCI Bullish Day</td><td bgcolor="#FFFACD">STCI Neutral Day</td></tr></table > ';

  // "Sending" data to HTML file.
  const chngInPosMtx = getDocElementById('changeInPos');
  chngInPosMtx.innerHTML = chngInPosTbl;

  const upcomingEventsMtx = getDocElementById('upcomingEvents');
  upcomingEventsMtx.innerHTML = upcomingEventsTbl;

  const currTableMtx8 = getDocElementById('idCurrTableMtx7');
  currTableMtx8.innerHTML = currTableMtx7;
}

console.log('SqCore: Script END');