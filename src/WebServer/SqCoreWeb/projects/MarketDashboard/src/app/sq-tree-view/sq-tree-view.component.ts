import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // data receive from other components

  isExpanded: boolean = false;
  isFolderHasChildren: boolean = false;
  static gLastSelectedItem: any;

  constructor() { }

  ngOnInit(): void {
  }

  onItemClicked(item: any) {
    if (item.isExpanded) {
      item.isExpanded = !item.isExpanded;
      return;
    } else {
      if (item.children) {
        if (item.children.length > 0) {
          item.isExpanded = true;
          this.isFolderHasChildren = true;
        } else
          item.isExpanded = false;
      }
    }

    // holder.SelecetdId = item.id

    SqTreeViewComponent.gLastSelectedItem = item;
  }

  // Yet to develop
  expendChildren(treeviewPortItemOpenPathIds: number[]) {

  }
}