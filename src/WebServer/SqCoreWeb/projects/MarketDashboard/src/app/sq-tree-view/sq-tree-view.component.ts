import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // data receive from other components
  @Input() treeviewHolderItems: any; // data receive from other components

  isExpanded: boolean = false;
  // isItemSelected: boolean = false;
  static gLastSelectedItem: any;

  constructor() { }

  ngOnInit(): void {
  }

  onItemClicked(item: any) {
    // item.isItemSelected = true;
    if (item.isExpanded) {
      item.isExpanded = !item.isExpanded;
      return;
    } else {
      if (item.children) {
        if (item.children.length > 0)
          item.isExpanded = true;
        else
          item.isExpanded = false;
      }
    }

    // Under Development - Daya
    // holder.SelecetdId = item.id
    // const lastSelected = new TreeViewItemSelectionHolder();
    // this.treeviewSelection.lastSelected = item;
    // this.treeviewSelection.lastSelectedId = item.id;
    // this.treeviewSelection.selectedIdList.push(item.id);
    // this.treeviewSelection.lastSelected;

    SqTreeViewComponent.gLastSelectedItem = item;
  }

  // Yet to develop
  expandChildren(treeviewPortItemOpenPathIds: number[]) {
  }
}