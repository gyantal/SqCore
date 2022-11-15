import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-sq-tree-view',
  templateUrl: './sq-tree-view.component.html',
  styleUrls: ['./sq-tree-view.component.scss']
})
export class SqTreeViewComponent implements OnInit {
  @Input() items: any; // data receive from other components

  isExpanded: boolean = false;
  // portfolioSelectionSelected: string = '';
  constructor() { }

  ngOnInit(): void {
  }

  onPortfoioFolderClicked(item: any) {
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