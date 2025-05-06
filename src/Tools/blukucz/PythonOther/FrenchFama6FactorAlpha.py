import pandas as pd
import yfinance as yf
import numpy as np
import statsmodels.api as sm
from datetime import datetime, timedelta
import os

# Read the CSV file containing ticker-recommendation date pairs
tickers_dates = pd.read_csv('D:/Temp/tickers_dates.csv')

# Read the factor file
ff_factors = pd.read_csv('D:/Temp/FF6Factor.csv')
ff_factors['Date'] = pd.to_datetime(ff_factors['Date'])

# Ensure output directory exists
output_directory = r'D:/Temp/FF'
if not os.path.exists(output_directory):
    os.makedirs(output_directory)

# Function to download stock data from Yahoo Finance
def get_stock_data(ticker, start_date, end_date):
    stock_data = yf.download(ticker, start=start_date, end=end_date, auto_adjust=False) # 2025-02-27: yf API changed. The default auto_adjust=True gives only adjusted OHLC, not giving AdjClose, so impossible to reverse engineer the splits, dividindends and rawPrices. The auto_adjust=false gives OHLC (raw) + 'Adj Close'.
    stock_data['Return'] = stock_data['Adj Close'].pct_change()  # Calculate daily returns
    stock_data = stock_data.dropna()  # Remove NaN values
    return stock_data

# Function to process each ticker and recommendation date pair
def process_ticker(ticker, recommendation_date_str):
    try:
        recommendation_date = datetime.strptime(recommendation_date_str, '%Y-%m-%d')
        end_date = recommendation_date + timedelta(days=100)
        start_date = end_date - timedelta(days=500)  # Approx. one year

        # Download stock data
        stock_data = get_stock_data(ticker, start_date, end_date)

        # Select the appropriate time window
        filtered_stock_data = stock_data.loc[(stock_data.index >= recommendation_date - timedelta(days=396)) & 
                                             (stock_data.index <= recommendation_date - timedelta(days=30))]

        # Merge stock data with factor data
        merged_data = pd.merge(filtered_stock_data, ff_factors, left_index=True, right_on='Date')

        # Calculate excess returns
        merged_data['Excess Return'] = merged_data['Return'] * 100 - merged_data['RF']

        # Prepare the regression model
        X = merged_data[['MKT-RF', 'SMB', 'HML', 'RMW', 'CMA', 'MOM']]
        X = sm.add_constant(X)
        y = merged_data['Excess Return']
        model = sm.OLS(y, X).fit()

        # Extract R-squared value
        r_squared = model.rsquared

        # Define the future period
        start_future = recommendation_date
        end_future_2m = start_future + timedelta(days=61)
        end_future_3m = start_future + timedelta(days=91)

        # Filter future data
        future_data = stock_data.loc[(stock_data.index >= start_future) & 
                                     (stock_data.index <= end_future_3m)].copy()
        future_factors = ff_factors.loc[(ff_factors['Date'] >= start_future) & 
                                        (ff_factors['Date'] <= end_future_3m)].copy()
        future_data.index = future_data.index.normalize()
        future_factors.set_index('Date', inplace=True)

        # Calculate future excess returns
        future_data['Excess Return'] = future_data['Return'].sub(future_factors['RF'] / 100, fill_value=0)
        future_data['Cumulative Return'] = (1 + future_data['Excess Return']).cumprod() - 1

        # Predicted returns
        predicted_return_2m = model.predict(sm.add_constant(future_factors[['MKT-RF', 'SMB', 'HML', 'RMW', 'CMA', 'MOM']][:len(future_data.loc[:end_future_2m])]))
        predicted_return_3m = model.predict(sm.add_constant(future_factors[['MKT-RF', 'SMB', 'HML', 'RMW', 'CMA', 'MOM']][:len(future_data.loc[:end_future_3m])]))

        # Cumulative predicted returns
        cumulative_predicted_return_2m = (1 + predicted_return_2m / 100).cumprod() - 1
        cumulative_predicted_return_3m = (1 + predicted_return_3m / 100).cumprod() - 1

        # Calculate alphas
        alpha_2m = future_data.loc[:end_future_2m, 'Cumulative Return'].iloc[-1] * 100 - cumulative_predicted_return_2m.iloc[-1] * 100
        alpha_3m = future_data.loc[:end_future_3m, 'Cumulative Return'].iloc[-1] * 100 - cumulative_predicted_return_3m.iloc[-1] * 100

        return alpha_2m, alpha_3m, r_squared

    except Exception as e:
        print(f"Error processing {ticker} for date {recommendation_date_str}: {e}")
        return None, None, None

# Process each ticker and date
results = []

for index, row in tickers_dates.iterrows():
    ticker = row['Ticker']
    recommendation_date = row['RecommendationDate']
    alpha_2m, alpha_3m, r_squared = process_ticker(ticker, recommendation_date)
    results.append({'Ticker': ticker, 'RecommendationDate': recommendation_date, 'Alpha_2M': alpha_2m, 'Alpha_3M': alpha_3m, 'R-Squared': r_squared})

# Save results to CSV
results_df = pd.DataFrame(results)
results_df.to_csv(os.path.join(output_directory, 'results.csv'), index=False)

print("Processing complete. Results saved to 'D:/Temp/FF/results.csv'.")