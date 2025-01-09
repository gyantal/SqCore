document.getElementById("startDownload").addEventListener("click", () => {
    const tickerList = document.getElementById("tickerList").value
        .split(",").map(ticker => ticker.trim()).filter(ticker => ticker);

    if (tickerList.length > 0) {
        chrome.runtime.sendMessage({ action: "start", tickers: tickerList });
        window.close();
    } else {
        alert("Please enter at least one ticker.");
    }
});

