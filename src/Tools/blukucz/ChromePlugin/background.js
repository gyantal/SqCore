chrome.runtime.onMessage.addListener((message) => {
    if (message.action === "start") {
        const tickers = message.tickers;
        processTickers(tickers);
    }
});

async function processTickers(tickers) {
    for (const ticker of tickers) {
        console.log(`Processing ${ticker}...`);
        const url = `https://seekingalpha.com/symbol/${ticker}/ratings/quant-ratings`;

        const tab = await chrome.tabs.create({ url, active: true });
        await waitForTabLoad(tab.id, url);

        console.log(`Injecting content script for ${ticker}`);
        await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            files: ["content.js"]
        });

        // Wait for completion or error
        const result = await waitForCompletion();

        if (result.action === "error") {
            console.warn(`Error processing ${ticker}: ${result.reason}`);
        } else {
            console.log(`Successfully processed ${ticker}`);
        }

        // Close the tab and move to the next ticker
        await chrome.tabs.remove(tab.id);
    }
    console.log("All tickers processed!");
}

function waitForTabLoad(tabId, expectedUrl) {
    return new Promise(resolve => {
        chrome.tabs.onUpdated.addListener(function listener(id, info, tab) {
            if (id === tabId && info.status === "complete" && tab.url.startsWith(expectedUrl)) {
                chrome.tabs.onUpdated.removeListener(listener);
                resolve();
            }
        });
    });
}

function waitForCompletion() {
    return new Promise(resolve => {
        chrome.runtime.onMessage.addListener(function listener(message) {
            if (message.action === "done" || message.action === "error") {
                chrome.runtime.onMessage.removeListener(listener);
                resolve(message);
            }
        });
    });
}
