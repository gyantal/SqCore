import { Component } from '@angular/core';

// The following functions are created to illustrate the event binding mechanism in an Angular project. see: https://docs.google.com/document/d/14X1rRkSsa3H79b6kjPPrnRBVWYqPZxsj9zC3R6cF8xk/edit#heading=h.b806rr4bw9md
// When the oninputCb and onchangeCb callback functions are defined inside the angular component, they are not accessible.
// Therefore, we need to make these functions globally available.
function oninputCb(event: Event) {
  console.log("Event: oninputCb. $event.value: " + (event.target as HTMLInputElement).value + ". m_ngTest2: NA");
}

function onchangeCb(event: Event) {
  console.log("Event: onchangeCb. $event.value: "+ (event.target as HTMLInputElement).value + ". m_ngTest2: NA");
}

// Make the function accessible globally
(window as any).oninputCb = oninputCb;
(window as any).onchangeCb = onchangeCb;

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

  inputCb(event: Event) {
    console.log("Event: inputCb. $event.value: " + (event.target as HTMLInputElement).value + ". m_ngTest2: " + this.m_ngTest1);
  }

  changeCb(event: Event) {
    console.log("Event: changeCb. $event.value: " + (event.target as HTMLInputElement).value + ". m_ngTest2: " + this.m_ngTest1);
  }

  ngModelChangeCb(event: Event) {
    console.log("Event: ngModelChangeCb. $event.value: " + event + ". m_ngTest2: " + this.m_ngTest2);
  }
}
