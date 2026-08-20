import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-tooltip-sandpit',
  templateUrl: './tooltip-sandpit.component.html',
  styleUrls: ['./tooltip-sandpit.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false
})
export class TooltipSandpitComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
