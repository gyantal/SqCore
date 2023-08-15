# %pip install yfinance
# %pip install scipy
# %pip install matplotlib
# %pip install pyfolio
# %pip uninstall pyfolio
# %pip install git+https://github.com/quantopian/pyfolio
# %pip install panda

# Ignore printing all warnings
import warnings
warnings.filterwarnings('ignore')

# Importing necessary libraries
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import yfinance as yf
import pyfolio as pf
import datetime as dt
import pandas_datareader.data as web
import os
import common_aa_pv as com

# print all outputs
from IPython.core.interactiveshell import InteractiveShell
InteractiveShell.ast_node_interactivity = "all"
from scipy.stats import rankdata

def rel_mom_weighted(used_df_p, rel_mom_lbs_p, rel_mom_weights_p, skipped_period_p):
    used_avg_returns = pd.DataFrame(0, index = used_df_p.index, columns = used_df_p.columns)
    for i in range(len(rel_mom_lbs_p)):
        used_avg_returns += (used_df_p / used_df_p.shift(rel_mom_lbs_p[i]) - 1) * rel_mom_weights_p[i]
    used_avg_returns = used_avg_returns / sum(rel_mom_weights_p)
    # used_rets = used_df_p.shift(skipped_period_p * 21) / used_avg_price - 1
    return used_avg_returns

def haa(ticker_list_canary, ticker_list_defensive, ticker_list_offensive,rebalance_unit, rebalance_freq, rebalance_shift, skipped_period, no_played_ETFs, abs_threshold, start_date, end_date):

    # ticker_list_all = ticker_list_canary + ticker_list_defensive + ticker_list_offensive
    ticker_list_all = list(set(ticker_list_canary + ticker_list_defensive + ticker_list_offensive))
    ticker_list_played = list(set(ticker_list_defensive + ticker_list_offensive))
    adj_close_price = yf.download(ticker_list_all,start = pd.to_datetime(start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(end_date) + pd.DateOffset(days= 1) )['Adj Close']
    adj_close_price2 = adj_close_price[ticker_list_played]

    df = adj_close_price.copy()
    df['Year'], df['Month'], df['Week'] = df.index.year, df.index.month, df.index.isocalendar().week
    df['Monthly_Rb'] = df.Month != df.Month.shift(-1)
    df['Weekly_Rb'] = df.Week != df.Week.shift(-1)
    df['Daily_Rb'] = True
    df['RebalanceUnit'] = df.Monthly_Rb if rebalance_unit == 'Month' else df.Weekly_Rb if rebalance_unit == 'Week' else df.Daily_Rb
    df.RebalanceUnit = df.RebalanceUnit.shift(rebalance_shift).fillna(False) 
    df.RebalanceUnit[0] = True
    df['NoPeriod'] = df.RebalanceUnit.shift(1).cumsum()
    df.NoPeriod[0] = 0
    df['NoData'] = np.arange(len(df)) + 1
    df['Rebalance'] = np.where((df.RebalanceUnit == True) & (df.NoPeriod.mod(rebalance_freq) == 0), True, False)

    dailyret = adj_close_price/adj_close_price.shift(1) - 1

    canary_lbs_base = np.array([1, 3, 6, 12])
    canary_lbs = canary_lbs_base * 21
    canary_weights = [1, 1, 1, 1]
    canary_df = df[ticker_list_canary]
    canary_rets = (canary_df / canary_df.shift(canary_lbs[0]) - 1) * canary_weights[0] + (canary_df / canary_df.shift(canary_lbs[1]) - 1) * canary_weights[1] + (canary_df / canary_df.shift(canary_lbs[2]) - 1) * canary_weights[2] + (canary_df / canary_df.shift(canary_lbs[3]) - 1) * canary_weights[3]
    canary_signal = (canary_rets > 0).sum(1) / len(ticker_list_canary)

    rel_mom_lbs_base = np.array([ 1, 3, 6, 12])
    rel_mom_lbs = (rel_mom_lbs_base + skipped_period) * 21
    rel_mom_weights = [1, 1, 1, 1]
    defensive_df = df[ticker_list_defensive]
    offensive_df = df[ticker_list_offensive]
    
    defensive_rets = rel_mom_weighted(defensive_df, rel_mom_lbs, rel_mom_weights, skipped_period)
    offensive_rets = rel_mom_weighted(offensive_df, rel_mom_lbs, rel_mom_weights, skipped_period)
    defensive_rank = defensive_rets.rank(axis = 1, ascending = False)
    offensive_rank = offensive_rets.rank(axis = 1, ascending = False)
    defensive_played = ((defensive_rets > abs_threshold) & (defensive_rank <= no_played_ETFs['def'])).sum(1)
    offensive_played = ((offensive_rets > abs_threshold) & (offensive_rank <= no_played_ETFs['off'])).sum(1)
    defensive_weights = (defensive_rank.divide(defensive_rank, axis = 0).divide(no_played_ETFs['def'], axis = 0)).where(defensive_rank <= no_played_ETFs['def'], 0)
    offensive_weights = (offensive_rank.divide(offensive_rank, axis = 0).divide(offensive_played, axis = 0)).where((offensive_rank <= no_played_ETFs['off']) & (offensive_rets > abs_threshold), 0)
    defensive_weights2 = (defensive_rank.divide(defensive_rank, axis = 0).mul(1 - offensive_weights.sum(axis = 1), axis = 0).divide(no_played_ETFs['def'], axis =0)).where(defensive_rank <= no_played_ETFs['def'], 0)
    off_def_base_weights = pd.DataFrame({k: offensive_weights.get(k, 0) + defensive_weights2.get(k, 0) for k in set(offensive_weights) | set(defensive_weights2) }).fillna(0)
    
    no_off_etfs = len(ticker_list_offensive)
    no_def_etfs = len(ticker_list_defensive)
    no_off_def_etfs = len(off_def_base_weights.columns)
    canary_signal = np.array(canary_signal)
    offensive_weights_helper = np.array(offensive_weights)
    defensive_weights_helper = np.array(defensive_weights)
    off_def_base_weights_helper = np.array(off_def_base_weights)

    off_def_df = pd.concat([off_def_base_weights, defensive_weights], axis = 1)
    no_rows, no_cols = off_def_df.shape
    off_def_weights_helper = np.zeros((no_rows, no_cols))
    for i in range(no_rows):
            if canary_signal[i] == 1:
                        for j in range(no_off_def_etfs):
                            off_def_weights_helper[i, j] = off_def_base_weights_helper[i, j]
            else:
                for j in range(no_def_etfs):
                    off_def_weights_helper[i, j + no_off_def_etfs] = defensive_weights_helper[i, j]
    off_def_weights = pd.DataFrame(off_def_weights_helper, index = off_def_df.index, columns = off_def_df.columns)
    off_def_weights = off_def_weights.groupby(off_def_weights.columns, axis=1).sum()
    off_def_cash_weight = 1 - off_def_weights.sum(axis = 1)

    adj_close_price2 = adj_close_price2.sort_index(axis = 1)
    off_def_weights = off_def_weights.sort_index(axis = 1)

    pos_off_def, cash_off_def, pv_off_def = com.positions_pv(adj_close_price2, df.Rebalance, off_def_weights, off_def_cash_weight, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price2, df.Rebalance, start_date)

    ew_rets = pv_ew / pv_ew.shift(1) - 1
    off_def_rets = pv_off_def / pv_off_def.shift(1) - 1
    off_def_weights2 = off_def_weights.copy()
    off_def_weights2['cash'] = off_def_cash_weight
    
    off_def_curr_weights = off_def_weights2.iloc[-1]

    return pv_off_def, off_def_rets, off_def_weights2, pos_off_def, cash_off_def, off_def_curr_weights, pv_ew, ew_rets, pos_ew, cash_ew