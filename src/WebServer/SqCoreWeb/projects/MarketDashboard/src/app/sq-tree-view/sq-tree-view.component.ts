import { Component, OnInit, Input, ViewChildren, QueryList } from '@angular/core';
import { TreeViewState, TreeViewItem } from '../portfolio-manager/portfolio-manager.component';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  // @Input(), @ViewChildren variables: without "| any". Compile error: ' Property 'treeViewState' has no initializer and is not definitely assigned in the constructor.'
  // use the ! after the variable name 'Definite Assignment Assertion' to tell typescript that this variable will have a value at runtime
  @Input() items!: TreeViewItem[]; // nested tree view data receive from portfolio manager component
  @Input() treeViewState!: TreeViewState; // treeview selected data processing
  @ViewChildren(SqTreeViewComponent) public _children!: QueryList<SqTreeViewComponent>;

  isSelected: boolean = false;
  isExpanded: boolean = false;

  constructor() { }

  ngOnInit(): void {
  }

  DeselectThisItemsAndAllChildren() {
    for (const item of this.items) // at start, the root TreeViewComponent has no _children TreeviewComponents. But it has 3+ items[] for the root folders Shared, UserFolder, NoUser
      item.isSelected = false;

    for (const child of this._children) // for all Children
      child.DeselectThisItemsAndAllChildren();
  }

  onItemClicked(item: TreeViewItem) {
    this.treeViewState.lastSelectedItem = item;
    console.log('TreeView.onItemClicked(): ' + this.treeViewState.lastSelectedItem?.name);
    this.treeViewState.rootSqTreeViewComponent!.DeselectThisItemsAndAllChildren();
    item.isSelected = true;

    if (item.isExpanded) {
      item.isExpanded = !item.isExpanded;
      return; // ! George: this return is dangerous here. The code behind this IF might not execute
    } else {
      if (item.children) {
        if (item.children.length > 0)
          item.isExpanded = true;
        else
          item.isExpanded = false;
      }
    }
    const expandedIds = item.id; // selecting the expanded item ids
    if (!(this.treeViewState.expandedPrtfFolderIds.includes(expandedIds))) // check if the selected is already there in the list, if exist don't push it else add it
      this.treeViewState.expandedPrtfFolderIds.push(expandedIds);

    console.log('Expanded id length', this.treeViewState.expandedPrtfFolderIds.length); // just for checking the code....will be removed
  }

  // Yet to develop
  expandChildren(treeviewPortItemOpenPathIds: number[]) {
    // expanded folder ids are checked against the lsit and set the isSelected to true.
    for (const item of this.items) {
      for (let i = 0; i < this.treeViewState.expandedPrtfFolderIds.length; i++) {
        if (this.treeViewState.expandedPrtfFolderIds[i] == item.id) // check the item id's in the treeviewstate and invert isSelected
          item.isSelected = true;
      }
    }
  }
}