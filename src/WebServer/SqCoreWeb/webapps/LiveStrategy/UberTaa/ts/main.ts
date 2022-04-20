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

window.onload = function onLoadWindow() {
  console.log('SqCore: window.onload() BEGIN. All CSS, and images were downloaded.'); // images are loaded at this time, so their sizes are known

  AsyncStartDownloadAndExecuteCbLater('/StrategyUberTaa', (json: any) => {
    onReceiveData(json);
  });

  function onReceiveData(json: any) {
    // const jsonToStr = JSON.stringify(json).substr(0, 60) + '...';
    getDocElementById('DebugDataArrivesHere').innerText = '***"' + json[0].stringData + '"***';
  }
  console.log('SqCore: window.onload() END.');
};

function getDocElementById(id: string): HTMLElement {
  return document.getElementById(id) as HTMLElement; // type casting assures it is not null for the TS compiler. (it can be null during runtime)
}

function onImageClickGameChanger() {
  onImageClick(1);
}

function onImageClickGlobalAssets() {
  onImageClick(2);
}

function onImageClick(index: number) {
  console.log('OnClick received.' + index);
  AsyncStartDownloadAndExecuteCbLater(
      '/ContangoVisualizerData?commo=' + index,
      (json: any) => {
        onReceiveData(json);
      }
  );
}

function onReceiveData(json: any) {
  getDocElementById('idTitleCont').innerHTML = json.titleCont + ' <sup><small><a href="' + json.gDocRef + '" target="_blank">(Study)</a></small></sup>';
  getDocElementById('idTimeNow').innerHTML = json.requestTime;
  getDocElementById('idLiveDataTime').innerHTML = json.lastDataTime;
  getDocElementById('idCurrentPV').innerHTML = 'Current PV: <span class="pv">$ ' + json.currentPV + '</span> (based on <a href=' + json.gSheetRef + '" target="_blank">these current positions</a> updated for ' + json.currentPVDate + ')';
  getDocElementById('idCLMTString').innerHTML = 'Current Combined Leverage Market Timer signal is <span class="clmt">' + json.clmtSign + '</span> (SPX 50/200-day MA: ' + json.spxMASign + ', XLU/VTI: ' + json.xluVtiSign + ').';
  getDocElementById('idPosLast').innerHTML = 'Position weights in the last 20 days:';
  getDocElementById('idPosFut').innerHTML = 'Future events:';
}

getDocElementById('gameChanger').onclick = onImageClickGameChanger;
getDocElementById('globalAssets').onclick = onImageClickGlobalAssets;

document.addEventListener('DOMContentLoaded', (event) => {
  console.log('DOMContentLoaded(). All JS were downloaded. DOM fully loaded and parsed.');
});


console.log('SqCore: Script END');