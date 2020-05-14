import { Component, ViewChild, ElementRef, Output, EventEmitter } from '@angular/core';


@Component({
  selector: 'settings-dialog',
  templateUrl: './settings-dialog.component.html',
  // template: `
  //   <div #myModal class="container">
  //   <div class="content">
  //     <p>Some content here...</p>
  //     <button>Close</button>
  //   </div>
  //   </div>
  // `,
  styleUrls: ['./settings-dialog.component.scss']
})
export class SettingsDialogComponent {
  // tslint:disable-next-line: ban-types
 // @Input() _parentChangeThemeCallBack?: Function; // = undefined; // this property will be input from above parent container

  @Output() parentChangeTheme = new EventEmitter<string>();

  @ViewChild('settingsDialog', {static: false}) setDial!: ElementRef;

  constructor() { }

  open() {
    this.setDial.nativeElement.style.display = 'block';
  }

  close() {
    this.setDial.nativeElement.style.display = 'none';
  }

  onSetThemeSelector(theme: string) {
    // console.log(theme);
    this.parentChangeTheme.emit(theme);
    // console.log(this.parentChangeTheme.emit(theme));
    // onSetTheme(theme);
  }

}

// export class SettingsDialogComponent implements OnInit {

//   constructor() { }

//   ngOnInit() {
//   }

// }

