import { Component, OnInit, Input } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';

class NewsItem {
  public ticker = '';
  public title = '';
  public summary = '';
  public linkUrl = '';
  public downloadTime: Date = new Date();
  public publishDate: Date = new Date();
  public source = '';
  public displayText = '';
  public sentiment = '';
  public isDuplicate = 'false';
}

@Component({
  selector: 'app-quickfolio-news',
  templateUrl: './quickfolio-news.component.html',
  styleUrls: ['./quickfolio-news.component.scss']
})
export class QuickfolioNewsComponent implements OnInit {
  @Input() _parentHubConnection?: HubConnection = undefined; // this property will be input from above parent container

  public request: XMLHttpRequest = new XMLHttpRequest();
  interval: NodeJS.Timeout;
  previewText = '';
  previewTextCommon = '';
  selectedTicker = '';
  selectedSource = '';
  totalnewsCount = 0;
  filteredNewsCount = 0;
  filterDuplicateNewsItems = true;
  previewedCommonNews: NewsItem = new NewsItem();
  previewCommonInterval: NodeJS.Timeout = setInterval(
    () => {
    }, 10 * 60 * 1000); // every 10 minutes do nothing (just avoid compiler error (uninitialised))
  previewedStockNews: NewsItem = new NewsItem();
  previewStockInterval: NodeJS.Timeout = setInterval(
    () => {
    }, 10 * 60 * 1000); // every 10 minutes do nothing (just avoid compiler error (uninitialised))
  stockTickers: string[] = [];
  stockNews: NewsItem[] = [];
  generalNews: NewsItem[] = [
    // {
    //   ticker: '',
    //   title: 'Example news 1: Tesla drives alone',
    //   summary:
    //     'Example summary: Tesla cars are driving alone. They don\'t need to sleep.',
    //   linkUrl: 'https://angular.io/start#components',
    //   downloadTime: '2020-02-02 02:02',
    //   publishDate: '2020-02-02 02:02'
    //   // Source;
    //   // isVisibleFiltered;
    // },
    // {
    //   ticker: '',
    //   title: 'Example news 2: Aaple beats Pear',
    //   summary:
    //     'Example summary: The tech giant AAPL beats Pear in a dramatic fight',
    //   linkUrl: 'https://stockcharts.com/h-sc/ui?s=AAPL',
    //   downloadTime: '2020-02-01 01:01',
    //   publishDate: '2020-02-01 01:01'
    // },
    // {
    //   ticker: '',
    //   title: 'Example news 3: Ebola after Corona',
    //   summary:
    //     'Example summary: The mexican beer manufacturer changes its name to avoid frightening its customers from Corona to Ebola',
    //   linkUrl: 'https://hu.wikipedia.org/wiki/Corona',
    //   downloadTime: '2020-02-02 03:03',
    //   publishDate: '2020-02-02 03:03'
    // },
    // {
    //   ticker: '',
    //   title: 'Example news 4: The queen is retiring',
    //   summary:
    //     'Example summary 4: Elisabeth wants to start a new life, but not as queen. Its too boring - she sad.',
    //   linkUrl: 'https://hu.wikipedia.org/wiki/Queen',
    //   downloadTime: '2020-01-02 03:04',
    //   publishDate: '2020-01-02 03:04'
    // }
  ];

  constructor() {
    this.interval = setInterval(
      () => {
        this.updateNewsDownloadTextValues();
        this.UpdatePreviewHighlightCommon();
      }, 15000); // every 15 sec
  }

  public mouseEnterCommon(news: NewsItem): void {
    // console.log('mouse Enter Common' + news.linkUrl);
    this.previewTextCommon = news.summary;
    this.previewedCommonNews = news;
    this.UpdatePreviewHighlightCommon();
  }

  UpdatePreviewHighlightCommon() {
    const newsElements = document.querySelectorAll('.newsItemCommon');
    // console.log('newsItems count = ' + newsElements.length);
    for (const newsElement of newsElements) {
      // console.log('news ' + newsElement);
      const hyperLink = newsElement.getElementsByClassName('newsHyperlink')[0];
      // console.log('news ticker count = ' + tickerSpan.innerHTML);
      if (hyperLink.getAttribute('href') === this.previewedCommonNews.linkUrl) {
        newsElement.className = newsElement.className.replace(' previewed', '') + ' previewed';
        // console.log('setting to previewed');
      } else {
        newsElement.className = newsElement.className.replace(' previewed', '');
      }
    }
    clearInterval(this.previewCommonInterval);
    // this.previewCommonInterval = null;
  }

  public mouseEnter(news: NewsItem): void {
    // console.log('mouse Enter ' + news.linkUrl);
    this.previewText = news.summary;
    this.previewedStockNews = news;
    this.UpdatePreviewHighlightStock();
  }

  UpdatePreviewHighlightStock() {
    const newsElements = document.querySelectorAll('.newsItemStock');
    // console.log('newsItems count = ' + newsElements.length);
    for (const newsElement of newsElements) {
      // console.log('news ' + newsElement);
      const hyperLink = newsElement.getElementsByClassName('newsHyperlink')[0];
      // console.log('news ticker count = ' + tickerSpan.innerHTML);
      if (hyperLink.getAttribute('href') === this.previewedStockNews.linkUrl) {
        newsElement.className = newsElement.className.replace(' previewed', '') + ' previewed';
        // console.log('setting to previewed ' + hyperLink.getAttribute('href'));
      } else {
        newsElement.className = newsElement.className.replace(' previewed', '');
      }
    }
  }

  public filterDuplicateChanged(): void {
    this.filterDuplicateNewsItems = !this.filterDuplicateNewsItems;
    console.log('filter duplicates value is ' + this.filterDuplicateNewsItems);
    this.UpdateNewsVisibility();
    console.log('update complete');
  }

  public reloadClick(event): void {
    // console.log('reload clicked');
    if (this._parentHubConnection != null) {
      this._parentHubConnection.send('ReloadQuickfolio');
    }
  }

  public menuClick(event, ticker: string): void {
    // console.log('menu clicked xx' + ticker + 'xx');
    if (ticker === 'All assets') {
      this.selectedTicker = '';
    } else {
      this.selectedTicker = ticker;
    }
    this.UpdateNewsVisibility();
  }

  public menuSourceClick(event, ticker: string): void {
    // console.log('menu source clicked xx' + ticker + 'xx');
    if (ticker === 'All sources') {
      this.selectedSource = '';
    } else {
      this.selectedSource = ticker;
    }
    this.UpdateNewsVisibility();
  }

  UpdateNewsVisibility() {
    const menuElements = document.querySelectorAll('.menuElement');
    for (const menuElement of menuElements) {
      // console.log('menu element found xx' + menuElement.innerHTML + 'xx');
      // menuElement.className += ' active';
      let ticker = this.selectedTicker;
      if (ticker === '') {
        ticker = 'All assets';
      }
      menuElement.className = 'menuElement';
      if (menuElement.innerHTML === ticker) {
        // console.log('menu element found ' + ticker);
        menuElement.className += ' active';
      }
    }

    const sourceElements = document.querySelectorAll('.source');
    for (const sourceElement of sourceElements) {
      // console.log('source element found xx' + sourceElement.innerHTML + 'xx');
      // menuElement.className += ' active';
      let source = this.selectedSource;
      if (source === '') {
        source = 'All sources';
      }
      sourceElement.className = 'source';
      if (sourceElement.innerHTML === source) {
        // console.log('menu element found ' + ticker);
        sourceElement.className += ' active';
      }
    }

    const newsElements = document.querySelectorAll('.newsItemStock');
    let visibleCount = 0;
    // console.log('newsItems count = ' + newsElements.length);
    for (const newsElement of newsElements) {
      // console.log('news ' + newsElement);
      const tickerSpan = newsElement.getElementsByClassName('newsTicker')[0];
      const sourceSpan = newsElement.getElementsByClassName('newsSource')[0];
      const sentimentSpan = newsElement.getElementsByClassName('newsSentiment')[0];
      // console.log('news ticker count = ' + tickerSpan.innerHTML);
      const isVisibleDueTicker = this.TickerIsPresent(tickerSpan.innerHTML, this.selectedTicker);
      const isVisibleDueSource = this.SourceIsSelected(sourceSpan.innerHTML, sentimentSpan.innerHTML, this.selectedSource);
      // news duplicate filtering: check if there is other news item with the same title and older (show only the newest one)
      const newsIsDuplicateSpan = newsElement.getElementsByClassName('newsIsDuplicate')[0];
      const newsIsDuplicate = newsIsDuplicateSpan.innerHTML;
      const isVisibleDueDuplicate = newsIsDuplicate === 'false';
      // console.log('newsIsDuplicate = ' + newsIsDuplicate + ', bool = ' + isVisibleDueDuplicate);
      if (isVisibleDueTicker && isVisibleDueSource && (isVisibleDueDuplicate || !this.filterDuplicateNewsItems)) {
        newsElement.className = newsElement.className.replace(' inVisible', '');
        visibleCount++;
      } else {
        newsElement.className = newsElement.className.replace(' inVisible', '') + ' inVisible';
      }
    }
    this.filteredNewsCount = visibleCount;
  }

  SourceIsSelected(source: string, sentiment: string, selectedSource: string): boolean {
    // console.log('source is XX' + source + 'XX, sentiment is XX' + sentiment + 'XX');
    if (selectedSource === '') {
      return true;
    }
    if (source === 'YahooRSS' && selectedSource === 'Yahoo') {
      return true;
    }
    if (source === 'Benzinga' && selectedSource === 'Benzinga') {
      return true;
    }
    // all, yahoo and benzinga handled. Tipranks needs special handling
    if (source !== 'TipRanks' || !selectedSource.startsWith('TipRanks')) {
      return false;
    }
    if (selectedSource === 'TipRanks all') {
      return true;
    }
    if (selectedSource === 'TipRanks bullish' && sentiment === 'positive') {
      return true;
    }
    if (selectedSource === 'TipRanks neutral' && sentiment === 'neutral') {
      return true;
    }
    if (selectedSource === 'TipRanks bearish' && sentiment === 'negative') {
      return true;
    }
    return false;
  }

  TickerIsPresent(tickersConcatenated: string, selectedTicker: string): boolean {
    if (selectedTicker === '') {
      return true;
    }
    const tickers = tickersConcatenated.split(',');
    let foundSame = false;
    tickers.forEach(existingTicker => {
      if (existingTicker.trim() === selectedTicker) {
        foundSame = true;
      }
    });
    return foundSame;
  }

  ngOnInit(): void {
    if (this._parentHubConnection != null) {
      this._parentHubConnection.on(
        'quickfNewsCommonNewsUpdated',
        (message: NewsItem[]) => {
          console.log('Quickfolio News: general news update arrived');
          this.extractNewsList(message, this.generalNews);
          this.previewCommonInterval = setInterval(
            () => {
              this.SetCommonPreviewIfEmpty();
            }, 1000); // after 1 sec
        }
      );
      this._parentHubConnection.on(
        'stockTickerList',
        (message: string[]) => {
          console.log('Quickfolio News: stock ticker list update arrived');
          this.stockTickers = message;
          console.log('Init menu');
          this.menuClick(null, 'All assets');
          this.removeUnreferencedNews();
        }
      );
      this._parentHubConnection.on(
        'quickfNewsStockNewsUpdated',
        (message: NewsItem[]) => {
          // console.log('Quickfolio News: stock news update arrived');
          this.extractNewsList(message, this.stockNews);
          this.UpdateNewsVisibility();
          this.totalnewsCount = this.stockNews.length;
          this.previewStockInterval = setInterval(
            () => {
              this.SetStockPreviewIfEmpty();
            }, 1000); // after 1 sec
        }
      );
    }
  }

  SetStockPreviewIfEmpty() {
    if (this.previewText === '') {
      if (this.stockNews.length > 0) {
        this.mouseEnter(this.stockNews[0]);
      }
    }
  }

  SetCommonPreviewIfEmpty() {
    if (this.previewTextCommon === '') {
      if (this.generalNews.length > 0) {
        this.mouseEnterCommon(this.generalNews[0]);
        // console.log('SetCommonPreviewIfEmpty ' + this.generalNews[0].linkUrl);
      }
    }
  }

  removeUnreferencedNews() {
    this.stockNews = this.stockNews.filter(news => this.NewsItemHasTicker(news));
  }

  NewsItemHasTicker(news: NewsItem): boolean {
    let tickers = news.ticker.split(',');
    tickers = tickers.filter(existingTicker => this.stockTickers.includes(existingTicker));
    news.ticker = tickers.join(', ');
    return tickers.length > 0;
  }


  extractNewsList(message: NewsItem[], newsList: NewsItem[]): void {
    // console.log('new common message list ' + message.length);
    for (const newNews of message) {
      newNews.isDuplicate = 'false';
      // console.log('new message ' + newNews.linkUrl);
      this.insertMessage(newsList, newNews);
    }
  }

  insertMessage(messages: NewsItem[], newItem: NewsItem): void {
    let index = 0;
    let foundOlder = false;
    while ((index < messages.length) && !foundOlder) {
      if (messages[index].linkUrl === newItem.linkUrl) {
        this.extendTickerSection(messages[index], newItem.ticker);
        return;
      }
      foundOlder = newItem.publishDate > messages[index].publishDate;
      if (messages[index].title.toUpperCase() === newItem.title.toUpperCase()) {
        if (!foundOlder) {
          newItem.isDuplicate = 'true1';
          // console.log('1 FOUND duplicate quickfolio news ' + newItem.ticker + ': ' + newItem.title + '; ' + newItem.linkUrl );
          // console.log('1 FOUND the other news ' + messages[index].ticker + ': ' + messages[index].title + '; ' + messages[index].linkUrl);
        } else {
          messages[index].isDuplicate = 'true2';
          // console.log('2 FOUND duplicate quickfolio news ' + messages[index].ticker + ': ' + messages[index].title + '; ' + messages[index].linkUrl);
          // console.log('2 FOUND the other news ' + newItem.ticker + ': ' + newItem.title + '; ' + newItem.linkUrl);
        }
      }
      if (!foundOlder) {
        index++;
      }
    }
    this.updateNewsDownloadText(newItem);
    messages.splice(index, 0, newItem);
    index += 2;
    while (index < messages.length) {
      if (messages[index].title.toUpperCase() === newItem.title.toUpperCase()) {
        messages[index].isDuplicate = 'true3';
        // console.log('3 FOUND duplicate quickfolio news ' + messages[index].ticker + ': ' + messages[index].title + '; ' + messages[index].linkUrl);
        // console.log('3 FOUND the other news ' + newItem.ticker + ': ' + newItem.title + '; ' + newItem.linkUrl);
      }
      index++;
    }
  }

  updateNewsDownloadTextValues() {
    for (const news of this.generalNews) {
      this.updateNewsDownloadText(news);
    }
    for (const news of this.stockNews) {
      this.updateNewsDownloadText(news);
    }
  }

  updateNewsDownloadText(newsItem: NewsItem) {
    newsItem.displayText = this.getpublishedString(newsItem.publishDate);
  }

  extendTickerSection(news: NewsItem, newTicker: string) {
    const tickers = news.ticker.split(',');
    let foundSame = false;
    tickers.forEach(existingTicker => {
      if (existingTicker.trim() === newTicker) {
        foundSame = true;
      }
    });
    if (!foundSame) {
      news.ticker += ', ' + newTicker;
    }
  }

  getpublishedString(date: Date) {
    // console.log('since ' + date + '  ...  ' + new Date());
    const downloadDate = new Date(date);
    const timeDiffInSecs = Math.floor((new Date().getTime() - downloadDate.getTime()) / 1000);
    // console.log('since ' + timeDiffInSecs);
    if (timeDiffInSecs < 60) {
      return timeDiffInSecs.toString() + 'sec ago';
    }
    let timeDiffMinutes = Math.floor(timeDiffInSecs / 60);
    if (timeDiffMinutes < 60) {
      return timeDiffMinutes.toString() + 'min ago';
    }
    const timeDiffHours = Math.floor(timeDiffMinutes / 60);
    timeDiffMinutes = timeDiffMinutes - 60 * timeDiffHours;
    if (timeDiffHours < 24) {
      return timeDiffHours.toString() + 'h ' + timeDiffMinutes.toString() + 'm ago';
    }
    const timediffDays = Math.floor(timeDiffHours / 24);
    return timediffDays.toString() + ' days ago';
  }
} // class
