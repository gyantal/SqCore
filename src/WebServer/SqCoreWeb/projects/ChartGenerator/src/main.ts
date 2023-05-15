import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';
import { chrtGenDiag } from './app/app.component';


if (environment.production)
  enableProdMode();

function windowOnLoad() {
  chrtGenDiag.windowOnLoadTime = new Date();
  console.log('sq.d: ' + chrtGenDiag.windowOnLoadTime.toISOString() + ': windowOnLoad()');
}
window.onload = windowOnLoad;

platformBrowserDynamic().bootstrapModule(AppModule)
    .catch((err) => console.error(err));