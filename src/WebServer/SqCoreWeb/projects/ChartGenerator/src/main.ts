import { enableProdMode, provideZoneChangeDetection } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';
import { gChrtGenDiag } from './app/app.component';


if (environment.production)
  enableProdMode();

function windowOnLoad() {
  gChrtGenDiag.windowOnLoadTime = new Date();
  console.log('sq.d: ' + gChrtGenDiag.windowOnLoadTime.toISOString() + ': windowOnLoad()');
}
window.onload = windowOnLoad;

platformBrowserDynamic().bootstrapModule(AppModule, { applicationProviders: [provideZoneChangeDetection()], })
    .catch((err) => console.error(err));