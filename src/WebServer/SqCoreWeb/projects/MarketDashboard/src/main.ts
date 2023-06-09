import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';

import { gDiag } from '../../../TsLib/sq-common/sq-globals';


console.log('sq.d: ' + gDiag.mainTsTime.toISOString() + ': main.ts');

function dOMContentLoadedReady() {
  gDiag.dOMContentLoadedTime = new Date();
  console.log('sq.d: ' + gDiag.dOMContentLoadedTime.toISOString() + ': dOMContentLoadedReady()');

  // alert(`Image size: ${img.offsetWidth}x${img.offsetHeight}`);   // image is not yet loaded (unless was cached), so the size is 0x0
}
document.addEventListener('DOMContentLoaded', dOMContentLoadedReady);


function windowOnLoad() {
  gDiag.windowOnLoadTime = new Date();
  console.log('sq.d: ' + gDiag.windowOnLoadTime.toISOString() + ': windowOnLoad()');

  // alert(`Image size: ${img.offsetWidth}x${img.offsetHeight}`);   // image is loaded at this time
}
window.onload = windowOnLoad;


// official Angular code under this
if (environment.production)
  enableProdMode();


platformBrowserDynamic().bootstrapModule(AppModule)
    .catch((err) => console.error(err));