import yfinance as yf
from curl_cffi import requests
from datetime import datetime, timedelta
import time

# Create a curl_cffi session
session = requests.Session(impersonate="chrome")

# Initialize Ticker
ticker = yf.Ticker("AAPL", session=session)

# **Define start and end dates for historical data**
end_date = datetime.now() 
start_date = end_date - timedelta(days=365)  # Fetch data for the past year

# Fetch historical data with retry
max_retries = 3
retry_delay = 60
for attempt in range(max_retries):
    try:
        # **Get historical data instead of latest**
        historical_data = ticker.history(start=start_date, end=end_date)  
        
        if not historical_data.empty:
            print(f"Historical Stock Data for AAPL (from {start_date.strftime('%Y-%m-%d')} to {end_date.strftime('%Y-%m-%d')}):")
            print(historical_data)  # Print the historical data DataFrame
            break
        else:
            print("No historical data available for the specified period.")
            break
    except yf.exceptions.YFRateLimitError:
        if attempt < max_retries - 1:
            print(f"Rate limit hit. Retrying in {retry_delay} seconds...")
            time.sleep(retry_delay)
        else:
            print("Max retries reached.")
    except Exception as e:
        print(f"Error: {e}")
        break