import { Component } from '@angular/core';

// The following functions are created to illustrate the event binding mechanism in an Angular project. see: https://docs.google.com/document/d/14X1rRkSsa3H79b6kjPPrnRBVWYqPZxsj9zC3R6cF8xk/edit#heading=h.b806rr4bw9md
// A. Test run of 'Event binding (without ngModel)': <input type="text" [value]="m_ngTest1" oninput="nativeJsOnInputCb()" (input)="angularInputCb()">
// From JS console.log:
// Event: NativeJsOnInputCb()
// Event: AngularInputCb()
// -----------------------
// B. Test run of 'Event binding (with ngModel)': <input type="text" [ngModel]="m_ngTest2" oninput="nativeJsOnInputCb()" (input)="angularInputCb()" (ngModelChange)="angularNgModelChangeCb()">
// From JS console.log:
// Event: NativeJsOnInputCb()
// Event: AngularNgModelChangeCb()
// Event: AngularInputCb()
// Conclusion: NativeJs is called first, then ngModelChange, followed by the standard Angular event binding
// -----------------------
// Implementation issue: When the oninputCb and onchangeCb callback functions are defined inside the angular component, they are not accessible.
// Therefore, we need to make these functions globally available.
function nativeJsOnInputCb() {
  console.log("Event: NativeJsOnInputCb()");
}

function nativeJsOnChangeCb() {
  console.log("Event: NativeJsOnChangeCb()");
}

// Make the function accessible globally
(window as any).nativeJsOnInputCb = nativeJsOnInputCb;
(window as any).onchangeCb = nativeJsOnChangeCb;

@Component({
  selector: 'app-counter-component',
  templateUrl: './counter.component.html'
})
export class CounterComponent {
  public currentCount = 0;
  public m_ngTest1 = "ngTest1";
  public m_ngTest2 = "ngTest2";

  public incrementCounter() {
    this.currentCount++;
  }

  ngOnInit(): void {
    console.log('m_ngTest2', this.m_ngTest2);
  }

  angularInputCb() {
    console.log("Event: AngularInputCb()");
  }

  angularChangeCb() {
    console.log("Event: AngularChangeCb()");
  }

  angularNgModelChangeCb() {
    console.log("Event: AngularNgModelChangeCb()");
  }
}
