import './../css/main.css';

// 1. Declare some global variables and hook on DOMContentLoaded() and window.onload()
console.log('SqCore: Script BEGIN');

function getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
  return document.getElementById(id) as HTMLElement;
}

window.onload = function onLoadWindow() {
  console.log('SqCore: window.onload() BEGIN.');
  getNonNullDocElementById('topStocks').onclick = () => onButtonClick('topStocks');
  getNonNullDocElementById('topAnalysts').onclick = () => onButtonClick('topAnalysts');
  console.log('SqCore: window.onload() END.');
};

function onButtonClick(saDataSelector: string) { // 'TopStocks'/'TopAnalysts'
  console.log('OnClick received.' + saDataSelector);
  asyncFetchAndExecuteCallback(
      '/SeekingAlphaHelper?dataSelector=' + saDataSelector,
  );
}

async function asyncFetchAndExecuteCallback(url: string) {
  fetch(url)
      .then((response) => {
        if (!response.ok)
          console.log('SqCore.asyncFetchAndExecuteCallback : Invalid response');
        return response.text();
      })
      .then((data) => {
        const topStocksOrTopAnalystTextAreaElement = getNonNullDocElementById('topStocksOrTopAnalyst') as HTMLTextAreaElement;
        topStocksOrTopAnalystTextAreaElement.value = data;
        console.log('SqCore.asyncFetchAndExecuteCallback - Data received:', data);
      })
      .catch((error) => {
        console.error('SqCore.asyncFetchAndExecuteCallback - Error:', error);
      });
}
console.log('SqCore: Script END');