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

def kellerprotmom(ticker_list, rebalance_unit, rebalance_freq, rebalance_shift, correl_lb_months, lb_periods, lb_weights, no_selected_ETFs, start_date, end_date):

    ticker_list.append('IEF')
    adj_close_price = yf.download(ticker_list,start = pd.to_datetime(start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(end_date) + pd.DateOffset(days= 1) )['Adj Close']
    adj_close_price_played = adj_close_price.drop(columns = ['IEF'])

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
    dailyret_played = dailyret.drop(columns = ['IEF'])
    avg_dailyret = dailyret_played.mean(axis = 1)
    
    correl_lb_days = correl_lb_months * 21

    keller_corr = dailyret_played.rolling(correl_lb_days).corr(avg_dailyret)
    
    lb_days_base = {'Month' : 21, 'Week' : 5, 'Day' : 1}
    lb_days = lb_days_base[rebalance_unit] * np.array(lb_periods)
    lb_weights = lb_weights / np.sum(lb_weights)

    keller_df = df[ticker_list]
    keller_rel_mom_rets = (keller_df / keller_df.shift(lb_days[0]) - 1) * lb_weights[0] + (keller_df / keller_df.shift(lb_days[1]) - 1) * lb_weights[1] + (keller_df / keller_df.shift(lb_days[2]) - 1) * lb_weights[2] + (keller_df / keller_df.shift(lb_days[3]) - 1) * lb_weights[3]
    rel_mom_IEF = keller_rel_mom_rets.IEF
    rel_mom_played = keller_rel_mom_rets.drop(columns = ['IEF'])

    z_score = rel_mom_played * (1 - keller_corr)
    number_of_available_ETFs = adj_close_price_played[adj_close_price_played > 0].count(axis = 1)
    number_of_positive_z_scores = z_score[z_score > 0].count(axis = 1)
    cash_prot_perc = (number_of_available_ETFs).divide(number_of_available_ETFs, axis = 0).where(number_of_positive_z_scores < (number_of_available_ETFs / 2), (number_of_available_ETFs - number_of_positive_z_scores) / (number_of_available_ETFs / 2))
    number_of_played_ETFs = number_of_positive_z_scores.where(number_of_positive_z_scores < no_selected_ETFs, no_selected_ETFs).values.reshape(-1,1)
    z_score_rank = z_score.rank(axis = 1, ascending = False).fillna(1000)

    etf_weights = (z_score_rank.divide(z_score_rank, axis = 0).multiply(1 - cash_prot_perc, axis = 0).divide(number_of_played_ETFs, axis = 0)).where(z_score_rank < number_of_played_ETFs + 1, 0)
    etf_weights['IEF'] = (1 - etf_weights.sum(axis = 1)).where(rel_mom_IEF > 0, 0)
    etf_weights = etf_weights.sort_index(axis = 1)
    cash_weight = 1 - etf_weights.sum(axis = 1)

    pos, cash, pv = com.positions_pv(adj_close_price, df.Rebalance, etf_weights, cash_weight, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price_played, df.Rebalance, start_date)
    strat_rets = pv / pv.shift(1) - 1
    strat_rets_ew = pv_ew / pv_ew.shift(1) - 1
    weights2 = etf_weights.copy()
    weights2['cash'] = cash_weight
    
    curr_weights = weights2.iloc[-1]

    return pv, strat_rets, weights2, pos, cash, curr_weights, pv_ew, strat_rets_ew, pos_ew, cash_ew