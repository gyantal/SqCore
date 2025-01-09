Quant Ratings Downloader Chrome Plugin

Overview
The Quant Ratings Downloader is a Chrome plugin designed for users who wish to download historical Quant Ratings data for specific tickers from the Seeking Alpha website. 
This tool automates the process of scrolling through pages and extracting the relevant Quant Ratings data into a CSV file, making it easier to analyze trends and performance metrics over time.

Features
- Extracts historical Quant Ratings, including Date, Price, Quant Score, Valuation, Growth, Momentum, and EPS Revisions.
- Supports multiple tickers in one session.
- Automatically saves the data in CSV format with the ticker name included in the file name.
- Handles missing data (e.g., "NOT COVERED" entries).

Installation Instructions
1. Download the Plugin Files:
   - Ensure you have all necessary files in a specific folder, including manifest.json, background.js, content.js, popup.html, popup.js, and icon.webp.
2. Open Chrome Extensions Page:
   - Open Google Chrome and navigate to chrome://extensions/.
3. Enable Developer Mode:
   - In the top-right corner, toggle on "Developer mode."
4. Load the Unpacked Extension:
   - Click "Load unpacked" and select the folder containing the plugin files.
5. Verify Installation:
   - The Quant Ratings Downloader icon should appear in the browser's toolbar in the top right corner. If it doesn't, ensure all files are present and try reloading the extension.

Usage Instructions
1. Prepare the Ticker List:
   - Open the popup by clicking the plugin icon in the Chrome toolbar.
   - Enter the tickers (e.g., AAPL, MSFT, GOOGL) separated by commas or spaces.
2. Start Downloading:
   - Press the "Start Download" button.
   - The plugin will open Seeking Alpha pages for the specified tickers, scroll to load all historical data, and save the data as CSV files in the default 'browser downloads' folder.
3. Active Window Requirement:
   - Do not switch away from the active Chrome window while the plugin is running. The automation requires the Seeking Alpha tabs to remain in focus for scrolling and data extraction.
4. Completion:
   - Once the process is complete, the downloaded files will be available in your downloads folder, named as <TICKER>_quant_ratings.csv.

Notes
- Ensure a stable internet connection during the data extraction process.
- If a ticker fails to load or data is incomplete, the plugin will skip that ticker and move to the next.
- You can reload the extension or restart the process for any failed tickers.