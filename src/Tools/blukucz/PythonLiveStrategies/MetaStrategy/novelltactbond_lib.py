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

def novelltactbond(ticker_list, rebalance_unit, rebalance_freq, rebalance_shift, absolute_threshold, threshold_type, cash_subs, lb_periods, lb_weights, no_selected_ETFs, start_date, end_date):

    ticker_list.append('BIL')
    adj_close_price = yf.download(ticker_list,start = pd.to_datetime(start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(end_date) + pd.DateOffset(days= 1), auto_adjust=False )['Adj Close'] # 2025-02-27: yf API changed. The default auto_adjust=True gives only adjusted OHLC, not giving AdjClose, so impossible to reverse engineer the splits, dividindends and rawPrices. The auto_adjust=false gives OHLC (raw) + 'Adj Close'.
    adj_close_price_played = adj_close_price.drop(columns = ['BIL'])
    cash_subs = 0 if cash_subs == 0 else 1
    threshold_type = 0 if threshold_type == 0 else 1

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

    lb_days_base = {'Month' : 21, 'Week' : 5, 'Day' : 1}
    lb_days = lb_days_base[rebalance_unit] * np.array(lb_periods)
    lb_weights = lb_weights / np.sum(lb_weights)

    novell_df = df[ticker_list]
    novell_rel_mom_rets = (novell_df / novell_df.shift(lb_days[0]) - 1) * lb_weights[0] + (novell_df / novell_df.shift(lb_days[1]) - 1) * lb_weights[1] + (novell_df / novell_df.shift(lb_days[2]) - 1) * lb_weights[2] + (novell_df / novell_df.shift(lb_days[3]) - 1) * lb_weights[3]
    rel_mom_BIL = novell_rel_mom_rets.BIL.fillna(0)
    rel_mom_played = novell_rel_mom_rets.drop(columns = ['BIL'])

    number_of_available_ETFs = adj_close_price_played[adj_close_price_played > 0].count(axis = 1)
    number_of_abs_thres_rel_mom = rel_mom_played[rel_mom_played > absolute_threshold].count(axis = 1)
    rel_mom_BIL_mod = rel_mom_BIL.values.reshape(-1, 1)
    number_of_rel_thres_rel_mom = rel_mom_played[rel_mom_played > rel_mom_BIL_mod].count(axis = 1)
    number_of_played_ETFs_abs_thres = number_of_abs_thres_rel_mom.where(number_of_abs_thres_rel_mom < no_selected_ETFs, no_selected_ETFs).values.reshape(-1, 1)
    number_of_played_ETFs_rel_thres = number_of_rel_thres_rel_mom.where(number_of_rel_thres_rel_mom < no_selected_ETFs, no_selected_ETFs).values.reshape(-1, 1)
    rel_mom_played_rank = rel_mom_played.rank(axis = 1, ascending = False).fillna(99)

    etf_weights_abs_thres = (rel_mom_played_rank.divide(rel_mom_played_rank, axis = 0).divide(number_of_played_ETFs_abs_thres, axis = 0)).where(rel_mom_played_rank < number_of_played_ETFs_abs_thres + 1, 0)
    etf_weights_rel_thres = (rel_mom_played_rank.divide(rel_mom_played_rank, axis = 0).divide(number_of_played_ETFs_rel_thres, axis = 0)).where(rel_mom_played_rank < number_of_played_ETFs_rel_thres + 1, 0)
    etf_weights_abs_thres['BIL'] = np.where(adj_close_price['BIL'] > 0, ((1 - etf_weights_abs_thres.sum(axis = 1))*cash_subs), 0)
    etf_weights_rel_thres['BIL'] = np.where(adj_close_price['BIL'] > 0, ((1 - etf_weights_rel_thres.sum(axis = 1))*cash_subs), 0)
    etf_weights_abs_thres = etf_weights_abs_thres.sort_index(axis = 1)
    etf_weights_rel_thres = etf_weights_rel_thres.sort_index(axis = 1)
    cash_weight_abs_thres = 1 - etf_weights_abs_thres.sum(axis = 1)
    cash_weight_rel_thres = 1 - etf_weights_rel_thres.sum(axis = 1)

    pos, cash, pv = com.positions_pv(adj_close_price, df.Rebalance, etf_weights_abs_thres, cash_weight_abs_thres, start_date) if threshold_type == 0 else com.positions_pv(adj_close_price, df.Rebalance, etf_weights_rel_thres, cash_weight_rel_thres, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price_played, df.Rebalance, start_date)
    strat_rets = pv / pv.shift(1) - 1
    strat_rets_ew = pv_ew / pv_ew.shift(1) - 1
    weights2 = etf_weights_abs_thres.copy() if threshold_type == 0 else etf_weights_rel_thres.copy()
    weights2['cash'] = cash_weight_abs_thres if threshold_type == 0 else cash_weight_rel_thres
    
    curr_weights = weights2.iloc[-1]

    return pv, strat_rets, weights2, pos, cash, curr_weights, pv_ew, strat_rets_ew, pos_ew, cash_ew