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

def dualmom(ticker_list, rebalance_unit, rebalance_freq, rebalance_shift, lb_period, skipped_period, no_played_ETFs, sub_rank_weights, abs_threshold, start_date, end_date):

    adj_close_price = yf.download(ticker_list,start = pd.to_datetime(start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(end_date) + pd.DateOffset(days= 1), auto_adjust=False )['Adj Close'] # 2025-02-27: yf API changed. The default auto_adjust=True gives only adjusted OHLC, not giving AdjClose, so impossible to reverse engineer the splits, dividindends and rawPrices. The auto_adjust=false gives OHLC (raw) + 'Adj Close'.

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

    lb_days_base = {'Month' : 21, 'Week' : 5, 'Day' : 1}
    lb_days = lb_days_base[rebalance_unit] * lb_period
    lb_days_with_skipped = lb_days_base[rebalance_unit] * (lb_period + skipped_period)

    rel_mom_rets = adj_close_price.shift(lb_days_base[rebalance_unit] * skipped_period) / adj_close_price.shift(lb_days_with_skipped) - 1

    sd_rets_helper = dailyret.rolling(lb_days).std()
    sd_rets = sd_rets_helper.shift(lb_days_base[rebalance_unit] * skipped_period)
    corr_rets_helper = dailyret.rolling(lb_days).corr()
    corr_rets_helper2 = corr_rets_helper.groupby(corr_rets_helper.index.get_level_values('Date')).mean()
    corr_rets = corr_rets_helper2.shift(lb_days_base[rebalance_unit] * skipped_period)

    rel_mom_rets_rank = rel_mom_rets.rank(axis = 1, ascending = False)
    sd_rets_rank = sd_rets.rank(axis = 1, ascending = True)
    corr_rets_rank = corr_rets.rank(axis = 1, ascending = True)
   
    total_rank_helper = sub_rank_weights['relMom'] * rel_mom_rets_rank + sub_rank_weights['volatility'] * sd_rets_rank + sub_rank_weights['correlation'] * corr_rets_rank
    total_rank = total_rank_helper.rank(axis = 1, ascending = True)
    no_selected_etfs = ((total_rank < no_played_ETFs + 1) & (rel_mom_rets > abs_threshold)).sum(1)
    no_really_played_etfs = no_selected_etfs.where(no_selected_etfs > no_played_ETFs, no_played_ETFs)
    etf_weights = (total_rank.divide(total_rank, axis = 0).divide(no_really_played_etfs, axis = 0)).where((total_rank < no_played_ETFs + 1) & (rel_mom_rets > abs_threshold), 0)
    cash_weight = 1 - etf_weights.sum(axis = 1)

    pos, cash, pv = com.positions_pv(adj_close_price, df.Rebalance, etf_weights, cash_weight, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price, df.Rebalance, start_date)
    strat_rets = pv / pv.shift(1) - 1
    strat_rets_ew = pv_ew / pv_ew.shift(1) - 1
    weights2 = etf_weights.copy()
    weights2['cash'] = cash_weight
    
    curr_weights = weights2.iloc[-1]

    return pv, strat_rets, weights2, pos, cash, curr_weights, pv_ew, strat_rets_ew, pos_ew, cash_ew