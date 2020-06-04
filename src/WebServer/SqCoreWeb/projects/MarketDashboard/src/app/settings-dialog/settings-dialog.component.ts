import { Component, Output, EventEmitter } from '@angular/core';

export class AppSettings {  // collect the settings variables here. This will be stored in a database in server code.
  uiTheme = 'sqClassic';
  marketHealthDefaultLookback = 'YTD';  // example. Not used yet.
  marketHealthDefaultStatistics = 'Return'; // example. Not used yet.
  catalystSnifferShowDetailedTooltips = true; // example. Not used yet.
  currentMood = '3';
  clone() {
    const newObj = new AppSettings();
    newObj.uiTheme = this.uiTheme;
    newObj.currentMood = this.currentMood;

    return newObj;
  }
}

@Component({
  selector: 'settings-dialog',
  templateUrl: './settings-dialog.component.html',
  styleUrls: ['./settings-dialog.component.scss']
})
export class SettingsDialogComponent {
  isVisible = false;  // see https://stackoverflow.com/questions/59013913/how-to-manipulate-a-div-style-in-angular-8
  @Output() parentChangeThemeEvent = new EventEmitter<string>();
  _appSettings = new AppSettings();
<<<<<<< HEAD
=======
  savedAppSettings = new AppSettings();
>>>>>>> 2c3ea30d69f71ce9f104b16b1bc731efec718c59

  constructor() { }

  openSettingsDialog() {
    this.isVisible = true;
    this.savedAppSettings = this._appSettings.clone();
    (document.getElementById('slider') as HTMLSelectElement).value = this._appSettings.currentMood;
    this.moodSelector();
  }

  closeSettingsDialog(discard: boolean) {
    if (discard) {
      this._appSettings = this.savedAppSettings;
      this.parentChangeThemeEvent.emit(this._appSettings.uiTheme);
    }
    this.isVisible = false;
  }

  onSetThemeSelector(theme: string) {
    if (this._appSettings.uiTheme === theme) {
      return;
    }
    this._appSettings.uiTheme = theme;
    console.log(theme);
    this.parentChangeThemeEvent.emit(theme);
  }

  moodSelector() {
    const slider = (document.getElementById('slider') as HTMLSelectElement);
    const emoji = (document.getElementById('emoji') as HTMLSelectElement);
    const emoticons = ['mood_bad', 'sentiment_very_dissatisfied', 'sentiment_satisfied', 'sentiment_satisfied_alt', 'sentiment_very_satisfied'];
    this._appSettings.currentMood = slider.value;
    emoji.innerHTML = emoticons[this._appSettings.currentMood];
  }

  saveSettingsClick() {
    // TODO: Save into database.
    this.closeSettingsDialog(false);
  }

  discardSettingsClick() {
    this.closeSettingsDialog(true);
  }
}
