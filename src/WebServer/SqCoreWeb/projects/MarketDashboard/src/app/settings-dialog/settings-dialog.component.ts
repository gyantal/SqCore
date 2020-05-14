import { Component, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'settings-dialog',
  templateUrl: './settings-dialog.component.html',
  styleUrls: ['./settings-dialog.component.scss']
})
export class SettingsDialogComponent {
  isVisible = false;  // see https://stackoverflow.com/questions/59013913/how-to-manipulate-a-div-style-in-angular-8
  @Output() parentChangeThemeEvent = new EventEmitter<string>();

  constructor() { }

  open() {
    this.isVisible = true;
  }

  close() {
    this.isVisible = false;
  }

  onSetThemeSelector(theme: string) {
    console.log(theme);
    this.parentChangeThemeEvent.emit(theme);
  }
}

