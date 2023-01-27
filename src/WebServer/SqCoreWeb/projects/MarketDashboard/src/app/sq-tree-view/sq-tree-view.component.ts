import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { TreeViewState } from '../portfolio-manager/portfolio-manager.component';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // nested tree view data receive from portfolio manager component
  @Input() treeViewState: TreeViewState | any; // treeview selected data processing

  // the below itemSelected is used as a 2 way binding to highlight the only selected item using @input, @output and EventEmitter decorators
  _itemSelected: any = null;
  @Input('itemSelected') set _(value: any) {
    this._itemSelected = value;
  }
  get itemSelected() {
    return this._itemSelected;
  }
  set itemSelected(value: any) {
    this._itemSelected = value;
    this.itemSelectedChange.emit(value);
  }
  @Output() itemSelectedChange: EventEmitter<any> = new EventEmitter<any>();

  isExpanded: boolean = false;
  isItemSelected: boolean = false;
  treeviewSelectedNode: any = [];

  constructor() { }

  ngOnInit(): void {
  }


  // DeselectAllChildren(node) { // UnderDevelopment - Daya
  //   // node.isItemSelected = false;
  //   node.isItemSelected = false;
  //   for (const child of this.treeViewState.nestedTree)
  //     this.DeselectAllChildren(child);
  //   this.treeviewSelectedNode.push(node);
  //   // for all Children
  //   //   this.DeselectAllChildren(child)
  // }

  onItemClicked(item: any) {
    this.itemSelectedChange.emit(item == this.itemSelected ? null : item);

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

    // this.treeViewState.lastSelectedItem = item;
    // const expandedIds = item.id;
    // this.treeViewState.expandedPrtfFolderIds.push(expandedIds);
  }

  // Yet to develop
  expandChildren(treeviewPortItemOpenPathIds: number[]) {
  }
}