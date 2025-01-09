// Scroll to load all dynamic content and wait until no new data is loaded
async function scrollToLoadAllData() {
    return new Promise((resolve) => {
        let lastHeight = 0;
        let loadCounter = 0;
        const maxTries = 6; // Allow 6 total tries before stopping (extra patience at the end)

        const interval = setInterval(() => {
            window.scrollTo(0, document.body.scrollHeight);
            const currentHeight = document.body.scrollHeight;

            if (currentHeight === lastHeight) {
                loadCounter++;
            } else {
                loadCounter = 0; // Reset if new content is loaded
                lastHeight = currentHeight;
            }

            // Stop if we've tried too many times with no new data
            if (loadCounter >= maxTries) {
                clearInterval(interval);
                resolve();
            }
        }, 1000); // Scroll interval: 1 seconds
    });
}

// Extract data specifically from Table 3
function extractTable3Data() {
    const rows = [];
    const headers = [
        "Date", "Price", "Quant Rating", "Quant Score",
        "Valuation", "Growth", "Profitability", "Momentum", "EPS Rev."
    ];

    const tables = document.querySelectorAll("table");
    if (tables.length < 3) {
        console.warn("Table 3 not found!");
        return [];
    }

    const table = tables[2]; // Target Table 3

    // Process table rows
    table.querySelectorAll("tbody tr").forEach(row => {
        const cells = [];
        
        // Ensure the first column (Date) is always included, whether it's <th> or <td>
        const firstCell = row.querySelector("th, td");
        cells.push(firstCell ? firstCell.innerText.replace(/\n/g, " ").trim() : "N/A");

        // Add remaining cells (excluding the first one already added)
        const otherCells = Array.from(row.querySelectorAll("td")).map(cell =>
            cell.innerText
                .replace(/\n/g, " ")        // Remove newlines
                .replace(/,/g, "")         // Remove thousands separator commas
                .trim()
        );

        cells.push(...otherCells);

        // Fix Quant Rating: If "NOT COVERED" exists, clean up
        if (cells[2]) {
            if (cells[2].includes("NOT COVERED")) {
                cells[2] = "NOT COVERED";
            }
        }

        // Clean Quant Score: Only keep numeric value
        if (cells[3]) {
            const scoreMatch = cells[3].match(/[\d.]+/);
            cells[3] = scoreMatch ? scoreMatch[0] : "";
        }

        // Clear fields conditionally based on "NOT COVERED" and "- RATING: NOT COVERED"
		if (cells[2] === "NOT COVERED") {
			// If Quant Rating is "NOT COVERED", clear all relevant fields
			for (let i = 4; i < cells.length; i++) {
				cells[i] = "";
		}
		} else {
			// Otherwise, only clear specific fields containing "- RATING: NOT COVERED"
			for (let i = 4; i < cells.length; i++) {
				if (cells[i].includes("- RATING: NOT COVERED")) {
					cells[i] = "";
				}
			}
		}


        rows.push(cells.slice(0, headers.length));
    });

    return [headers, ...rows];

}

// Convert data to CSV and trigger download
function downloadCSV(data, filename) {
    return new Promise(resolve => {
        const csvContent = "data:text/csv;charset=utf-8," +
            data.map(row => row.join(",")).join("\n");
        const encodedUri = encodeURI(csvContent);
        const link = document.createElement("a");
        link.href = encodedUri;
        link.download = filename;

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        setTimeout(resolve, 2000); // Wait 2 seconds to ensure download completes
    });
}

// Get ticker symbol from the URL
function getTickerFromURL() {
    const urlParts = window.location.pathname.split('/');
    return urlParts[2] ? urlParts[2].toUpperCase() : "UNKNOWN_TICKER";
}

// Main function
async function main() {
    try {
        await scrollToLoadAllData();
        const tableData = extractTable3Data();

        if (tableData && tableData.length > 1) {
            const ticker = getTickerFromURL();
            await downloadCSV(tableData, `${ticker}_quant_ratings.csv`);
            chrome.runtime.sendMessage({ action: "done" }); // Success
        } else {
            console.warn("No data found in Table 3!");
            chrome.runtime.sendMessage({ action: "error", reason: "No data found" }); // Send error message
        }
    } catch (error) {
        console.error("Error processing data:", error);
        chrome.runtime.sendMessage({ action: "error", reason: error.message }); // Notify background script
    }

    setTimeout(() => window.close(), 1000); // Close the tab after 1 second
}

main();
