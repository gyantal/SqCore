import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // data receive from other components
  // @Input() _parentWsConnection?: WebSocket = undefined; // this property will be input from above parent container

  isExpanded: boolean = false;
  portfolioSelectionSelected: string = '';
  parentFolderId: number = -1;

  // typeId: number = -1; // 1: PortfolioFolder, 2: Another treeview
  constructor() { }

  ngOnInit(): void {
  }

  onItemClicked(item: any) {
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

    // this.onClickPortfolio(item);
    // Under development - Daya
    this.portfolioSelectionSelected = item.name;
    this.parentFolderId = item.parentFolderId;
    // if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN)
    //   this._parentWsConnection.send('PortfMgr.CreatePortfFldr:' + this.parentFolderId);
    console.log('nested protfolio ', this.portfolioSelectionSelected, this.parentFolderId);
    const portfolioView = document.getElementsByClassName('treeView');
    console.log('count of treeView is ', portfolioView.length);
  }

  // // Under development - Daya
  // onClickPortfolio(portfolioSelected: string) {
  //   this.portfolioSelectionSelected = portfolioSelected;
  //   const portfolioView = document.getElementsByClassName('portfolioNestedView');
  //   console.log('The length of tree view is :', portfolioView.length);
  //   console.log('The portfolioSelected is :', portfolioSelected);
  //   for (const portfolio of portfolioView) {
  //     if (this.portfolioSelectionSelected == portfolio.previousElementSibling?.innerHTML) {
  //       // toggling between plus and minus signs for nested view
  //       if (portfolio.previousElementSibling?.classList.contains('portfolioFolder')) {
  //         portfolio.previousElementSibling?.classList.remove('portfolioFolder');
  //         portfolio.previousElementSibling?.classList.add('portfolioFolderMinus');
  //       } else {
  //         portfolio.previousElementSibling?.classList.remove('portfolioFolderMinus');
  //         portfolio.previousElementSibling?.classList.add('portfolioFolder');
  //       }
  //       portfolio.classList.toggle('active');
  //       break;
  //     }
  //   }
  // }
}