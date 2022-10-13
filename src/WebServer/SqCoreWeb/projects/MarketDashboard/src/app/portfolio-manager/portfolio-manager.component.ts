import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-portfolio-manager',
  templateUrl: './portfolio-manager.component.html',
  styleUrls: ['./portfolio-manager.component.scss']
})
export class PortfolioManagerComponent implements OnInit {
  @Input() _parentWsConnection?: WebSocket = undefined; // this property will be input from above parent container

  // isShowPortfolioView: boolean = true;
  portfolioSelection: string[] = ['Dr. Gyorgy, Antal', 'Didier Charmat'];
  portfolioSelectionSelected: string = 'Dr. Gyorgy, Antal';

  constructor() { }

  ngOnInit(): void {
  }


  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'PortfMgr.Portfolios': // The most frequent message should come first. Note: LstVal (realtime price) is handled earlier in a unified way.
        console.log('PortfMgr.Portfolios:' + msgObjStr);
        return true;
      case 'PortfMgr.Handshake': // The least frequent message should come last.
        console.log('PortfMgr.Handshake:' + msgObjStr);
        return true;
      default:
        return false;
    }
  }

  // Under development - Daya
  onClickPortfolio(portfolioSelected: string) {
    this.portfolioSelectionSelected = portfolioSelected;
    const portfolioView = document.getElementsByClassName('portfolioNestedView');
    console.log('The length of tree view is :', portfolioView.length);
    console.log('The portfolioSelected is :', portfolioSelected);
    for (const portfolio of portfolioView) {
      if (this.portfolioSelectionSelected == portfolio.previousElementSibling?.innerHTML) {
        // toggling between plus and minus signs for nested view
        if (portfolio.previousElementSibling?.classList.contains('portfolioManager')) {
          portfolio.previousElementSibling?.classList.remove('portfolioManager');
          portfolio.previousElementSibling?.classList.add('portfolioManagerMinus');
        } else {
          portfolio.previousElementSibling?.classList.remove('portfolioManagerMinus');
          portfolio.previousElementSibling?.classList.add('portfolioManager');
        }
        portfolio.classList.toggle('active');
        break;
      }
    }
  }
}