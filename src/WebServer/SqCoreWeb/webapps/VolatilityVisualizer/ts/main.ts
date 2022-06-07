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

  AsyncStartDownloadAndExecuteCbLater('/VolatilityDragVisualizer', (json: any) => {
    onReceiveData(json);
  });

  function onReceiveData(json: any) {
    // const jsonToStr = JSON.stringify(json).substr(0, 60) + '...';
    // getDocElementById('DebugDataArrivesHere').innerText = '***"' + json[0].stringData + '"***';
    getDocElementById('titleCont').innerHTML = '<small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small>';
    getDocElementById('requestTime').innerText = json.requestTime;
    getDocElementById('lastDataTime').innerText = json.lastDataTime;

    const volAssetNamesArray = json.volAssetNames.split(', ');
    const etpAssetNamesArray = json.etpAssetNames.split(', ');
    const gchAssetNamesArray = json.gchAssetNames.split(', ');
    const gmAssetNamesArray = json.gmAssetNames.split(', ');
    // const defCheckedListArray = json.defCheckedList.split(', ');

    let chBxs = '<p class="left"><button class="button2" style="background: url(/images/vix.jpg); background-size: cover;" title="Volatility ETPs" onclick="choseall(\'volA\')"/></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < volAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="volA" id="' + volAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + volAssetNamesArray[iAssets].split('_')[0] + '">' + volAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button class="button2" style="background: url(/images/ImportantEtps.png); background-size: cover;" title="Important ETPs" onclick="choseall(\'etpA\')"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < etpAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="etpA" id="' + etpAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + etpAssetNamesArray[iAssets].split('_')[0] + '">' + etpAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button class="button2" style="background: url(/images/GameChangers.png); background-size: cover;" title="GameChanger Stocks" onclick="choseall(\'gchA\')"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < gchAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="gchA" id="' + gchAssetNamesArray[iAssets] + '"/><a target="_blank" href="https://finance.yahoo.com/quote/' + gchAssetNamesArray[iAssets].split('_')[0] + '">' + gchAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '<br><button class="button2" style="background: url(/images/GlobalAssets.png); background-size: cover;" title="Global Assets" onclick="choseall(\'gmA\')"></button>&emsp;&emsp;';
    for (let iAssets = 0; iAssets < gmAssetNamesArray.length; iAssets++)
      chBxs += '<input class= "szpari" type="checkbox" name="gmA" id="' + gmAssetNamesArray[iAssets] + '" /><a target="_blank" href="https://finance.yahoo.com/quote/' + gmAssetNamesArray[iAssets].split('_')[0] + '">' + gmAssetNamesArray[iAssets] + '</a> &emsp;';

    chBxs += '</p ><p class="center"><button class="button3" style="background: url(/images/selectall.png); background-size: cover;" title="Select/Deselect All" onclick="checkAll(this)"/></button>&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;<button class="button3" style="background: url(/images/updateall.png); background-size: cover;" title="Update Charts and Tables" id=\'update_all\'></button></p> ';

    console.log('check box', chBxs);
  }
  console.log('SqCore: window.onload() END.');
};

console.log('SqCore: Script END');