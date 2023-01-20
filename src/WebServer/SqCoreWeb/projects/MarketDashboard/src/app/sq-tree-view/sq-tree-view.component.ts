import { Component, OnInit, Input } from '@angular/core';
import { TreeViewItemSelectionContainer } from '../portfolio-manager/portfolio-manager.component';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // nested tree view data receive from portfolio manager component
  @Input() treeviewContainerItems: TreeViewItemSelectionContainer | any; // treeview selected data processing

  isExpanded: boolean = false;
  isItemSelected: boolean = false;

  constructor() { }

  ngOnInit(): void {
  }

  onItemClicked(item: any) {
    if (item.isExpanded) {
      item.isExpanded = !item.isExpanded;
      item.isItemSelected = !item.isItemSelected;
      return;
    } else {
      if (item.children) {
        if (item.children.length > 0)
          item.isExpanded = true;
        else
          item.isExpanded = false;
      }
    }

    this.treeviewContainerItems.lastSelectedItem = item;
    const expandedIds = item.id;
    this.treeviewContainerItems.expandedPrtfFolderIds.push(expandedIds);

    item.isItemSelected = !item.isItemSelected;
  }

  // Yet to develop
  expandChildren(treeviewPortItemOpenPathIds: number[]) {
  }
}