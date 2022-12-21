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
    used_avg_price = pd.DataFrame(0, index = used_df_p.index, columns = used_df_p.columns)
    for i in range(len(rel_mom_lbs_p)):
        used_avg_price += used_df_p.shift(rel_mom_lbs_p[i]) * rel_mom_weights_p[i]
    used_avg_price = used_avg_price / sum(rel_mom_weights_p)
    used_rets = used_df_p.shift(skipped_period_p * 21) / used_avg_price - 1
    return used_rets

def baa(sel, ticker_list_canary, ticker_list_defensive, ticker_list_aggressive, ticker_list_balanced, rebalance_unit, rebalance_freq, rebalance_shift, skipped_period, no_played_ETFs, abs_threshold, start_date, end_date):

    ticker_list_all = ticker_list_canary + ticker_list_defensive + ticker_list_aggressive + ticker_list_balanced
    adj_close_price = yf.download(ticker_list_all,start = pd.to_datetime(start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(end_date) + pd.DateOffset(days= 1) )['Adj Close']

    df = adj_close_price.copy()
    df['Year'], df['Month'], df['Week'] = df.index.year, df.index.month, df.index.week
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
    canary_weights = [12, 4, 2, 1]
    canary_df = df[ticker_list_canary]
    canary_rets = (canary_df / canary_df.shift(canary_lbs[0]) - 1) * canary_weights[0] + (canary_df / canary_df.shift(canary_lbs[1]) - 1) * canary_weights[1] + (canary_df / canary_df.shift(canary_lbs[2]) - 1) * canary_weights[2] + (canary_df / canary_df.shift(canary_lbs[3]) - 1) * canary_weights[3]
    canary_signal = (canary_rets > 0).sum(1) / len(ticker_list_canary)

    rel_mom_lbs_base = np.array([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12])
    rel_mom_lbs = (rel_mom_lbs_base + skipped_period) * 21
    rel_mom_weights = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
    defensive_df = df[ticker_list_defensive]
    aggressive_df = df[ticker_list_aggressive]
    balanced_df = df[ticker_list_balanced]

    defensive_rets = rel_mom_weighted(defensive_df, rel_mom_lbs, rel_mom_weights, skipped_period)
    aggressive_rets = rel_mom_weighted(aggressive_df, rel_mom_lbs, rel_mom_weights, skipped_period)
    balanced_rets = rel_mom_weighted(balanced_df, rel_mom_lbs, rel_mom_weights, skipped_period)
    defensive_rank = defensive_rets.rank(axis = 1, ascending = False)
    aggressive_rank = aggressive_rets.rank(axis = 1, ascending = False)
    balanced_rank = balanced_rets.rank(axis = 1, ascending = False)
    defensive_played = ((defensive_rets > abs_threshold) & (defensive_rank <= no_played_ETFs['def'])).sum(1)
    aggressive_played = ((aggressive_rets > abs_threshold) & (aggressive_rank <= no_played_ETFs['offAgg'])).sum(1)
    balanced_played = ((balanced_rets > abs_threshold) & (balanced_rank <= no_played_ETFs['offBal'])).sum(1)
    defensive_weights = (defensive_rank.divide(defensive_rank, axis = 0).divide(defensive_played, axis =0)).where((defensive_rank <= no_played_ETFs['def']) & (defensive_rets > abs_threshold), 0)
    aggressive_weights = (aggressive_rank.divide(aggressive_rank, axis = 0).divide(aggressive_played, axis =0)).where((aggressive_rank <= no_played_ETFs['offAgg']) & (aggressive_rets > abs_threshold), 0)
    balanced_weights = (balanced_rank.divide(balanced_rank, axis = 0).divide(balanced_played, axis =0)).where((balanced_rank <= no_played_ETFs['offBal']) & (balanced_rets > abs_threshold), 0)
    def_cash_weight = 1 - defensive_weights.sum(axis = 1)
    agg_cash_weight = 1 - aggressive_weights.sum(axis = 1)
    bal_cash_weight = 1 - balanced_weights.sum(axis = 1)

    no_agg_etfs = len(ticker_list_aggressive)
    no_bal_etfs = len(ticker_list_balanced)
    no_def_etfs = len(ticker_list_defensive)
    canary_signal = np.array(canary_signal)
    aggressive_weights_helper = np.array(aggressive_weights)
    balanced_weights_helper = np.array(balanced_weights)
    defensive_weights_helper = np.array(defensive_weights)

    agg_def_df = pd.concat([aggressive_df, defensive_df], axis = 1)
    no_rows, no_cols = agg_def_df.shape
    agg_def_weights_helper = np.zeros((no_rows, no_cols))
    for i in range(no_rows):
            if canary_signal[i] == 1:
                        for j in range(no_agg_etfs):
                            agg_def_weights_helper[i, j] = aggressive_weights_helper[i, j]
            else:
                for j in range(no_def_etfs):
                    agg_def_weights_helper[i, j + no_agg_etfs] = defensive_weights_helper[i, j]
    agg_def_weights = pd.DataFrame(agg_def_weights_helper, index = agg_def_df.index, columns = agg_def_df.columns)
    agg_def_cash_weight = 1 - agg_def_weights.sum(axis = 1)

    bal_def_df = pd.concat([balanced_df, defensive_df], axis = 1)
    no_rows, no_cols = bal_def_df.shape
    bal_def_weights_helper = np.zeros((no_rows, no_cols))
    for i in range(no_rows):
        if canary_signal[i] == 1:
            for j in range(no_bal_etfs):
                bal_def_weights_helper[i, j] = balanced_weights_helper[i, j]
        else:
            for j in range(no_def_etfs):
                bal_def_weights_helper[i, j + no_bal_etfs] = defensive_weights_helper[i, j]
    bal_def_weights = pd.DataFrame(bal_def_weights_helper, index = bal_def_df.index, columns = bal_def_df.columns)
    bal_def_cash_weight = 1 - bal_def_weights.sum(axis = 1)

    pos_agg, cash_agg, pv_agg = com.positions_pv(aggressive_df, df.Rebalance, aggressive_weights, agg_cash_weight, start_date)
    pos_bal, cash_bal, pv_bal = com.positions_pv(balanced_df, df.Rebalance, balanced_weights, bal_cash_weight, start_date)
    pos_def, cash_def, pv_def = com.positions_pv(defensive_df, df.Rebalance, defensive_weights, def_cash_weight, start_date)
    pos_agg_def, cash_agg_def, pv_agg_def = com.positions_pv(agg_def_df, df.Rebalance, agg_def_weights, agg_def_cash_weight, start_date)
    pos_bal_def, cash_bal_def, pv_bal_def = com.positions_pv(bal_def_df, df.Rebalance, bal_def_weights, bal_def_cash_weight, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price, df.Rebalance, start_date)

    ew_rets = pv_ew / pv_ew.shift(1) - 1
    agg_rets = pv_agg / pv_agg.shift(1) - 1
    bal_rets = pv_bal / pv_bal.shift(1) - 1
    def_rets = pv_def / pv_def.shift(1) - 1
    agg_def_rets = pv_agg_def / pv_agg_def.shift(1) - 1
    bal_def_rets = pv_bal_def / pv_bal_def.shift(1) - 1
    agg_weights2 = aggressive_weights.copy()
    agg_weights2['cash'] = agg_cash_weight
    bal_weights2 = balanced_weights.copy()
    bal_weights2['cash'] = bal_cash_weight
    def_weights2 = defensive_weights.copy()
    def_weights2['cash'] = def_cash_weight
    agg_def_weights2 = agg_def_weights.copy()
    agg_def_weights2['cash'] = agg_def_cash_weight
    bal_def_weights2 = bal_def_weights.copy()
    bal_def_weights2['cash'] = bal_def_cash_weight

    agg_curr_weights = agg_weights2.iloc[-1]
    bal_curr_weights = bal_weights2.iloc[-1]
    def_curr_weights = def_weights2.iloc[-1]
    agg_def_curr_weights = agg_def_weights2.iloc[-1]
    bal_def_curr_weights = bal_def_weights2.iloc[-1]

    pv_dct = {'agg' : pv_agg, 'bal' : pv_bal, 'def' : pv_def, 'agg_def' : pv_agg_def, 'bal_def' : pv_bal_def}
    rets_dct = {'agg' : agg_rets, 'bal' : bal_rets, 'def' : def_rets, 'agg_def' : agg_def_rets, 'bal_def' : bal_def_rets}
    weights_dct = {'agg' : agg_weights2, 'bal' : bal_weights2, 'def' : def_weights2, 'agg_def' : agg_def_weights2, 'bal_def' : bal_def_weights2}
    pos_dct = {'agg' : pos_agg, 'bal' : pos_bal, 'def' : pos_def, 'agg_def' : pos_agg_def, 'bal_def' : pos_bal_def}
    cash_dct = {'agg' : cash_agg, 'bal' : cash_bal, 'def' : cash_def, 'agg_def' : cash_agg_def, 'bal_def' : cash_bal_def}
    curr_weights_dct = {'agg' : agg_curr_weights, 'bal' : bal_curr_weights, 'def' : def_curr_weights, 'agg_def' : agg_def_curr_weights, 'bal_def' : bal_def_curr_weights}

    return pv_dct[sel], rets_dct[sel], weights_dct[sel], pos_dct[sel], cash_dct[sel], curr_weights_dct[sel], pv_ew, ew_rets, pos_ew, cash_ew