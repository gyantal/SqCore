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

def scores(acp_p, lb_p, l_th_p, u_th_p):

    lowerPerc = acp_p.rolling(lb_p).quantile(l_th_p).to_numpy()
    upperPerc = acp_p.rolling(lb_p).quantile(u_th_p).to_numpy()
    prices = acp_p.to_numpy()
    no_rows, no_cols = acp_p.shape
    score = np.zeros((no_rows, no_cols))
    for i in range(1, no_rows):
        for j in range(no_cols):
            if prices[i,j] > upperPerc[i,j]:
                score[i,j] = 1
            elif prices[i,j] < lowerPerc[i,j]:
                score[i,j] = -1
            else:
                score[i,j] = score[i-1,j]
    score_df = pd.DataFrame(score, index = acp_p.index, columns = acp_p.columns) 
    return score_df


def taa(ticker_list, perc_ch_lb_list, vol_lb, perc_ch_up_thres, perc_ch_low_thres, rebalance_unit, rebalance_freq, rebalance_shift, start_date, end_date):

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

    scores1 = scores(adj_close_price, perc_ch_lb_list[0], perc_ch_low_thres, perc_ch_up_thres)
    scores2 = scores(adj_close_price, perc_ch_lb_list[1], perc_ch_low_thres, perc_ch_up_thres)
    scores3 = scores(adj_close_price, perc_ch_lb_list[2], perc_ch_low_thres, perc_ch_up_thres)
    scores4 = scores(adj_close_price, perc_ch_lb_list[3], perc_ch_low_thres, perc_ch_up_thres)
    avgscores = (scores1 + scores2 + scores3 + scores4)/4
    volatility = dailyret.rolling(vol_lb).std(perc_ch_low_thres)
    rel_score_vol = avgscores / volatility
    abs_score_vol = rel_score_vol.abs()
    sum_abs_score_vol = abs_score_vol.sum(axis = 1)
    weights = (rel_score_vol.divide(sum_abs_score_vol, axis = 0)).where(rel_score_vol > 0, 0)
    cash_weight = 1 - weights.sum(axis = 1)

    pos, cash, pv = com.positions_pv(adj_close_price, df.Rebalance, weights, cash_weight, start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(adj_close_price, df.Rebalance, start_date)
    strat_rets = pv / pv.shift(1) - 1
    strat_rets_ew = pv_ew / pv_ew.shift(1) - 1
    weights2 = weights.copy()
    weights2['cash'] = cash_weight

    curr_weights = weights2.iloc[-1]

    return pv, strat_rets, weights2, pos, cash, curr_weights, pv_ew, strat_rets_ew, pos_ew, cash_ew