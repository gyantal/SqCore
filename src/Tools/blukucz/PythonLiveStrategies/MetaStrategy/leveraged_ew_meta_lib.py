# %pip install yfinance
# %pip install scipy
# %pip install matplotlib
# %pip install pyfolio
# %pip uninstall pyfolio
# %pip install git+https://github.com/quantopian/pyfolio
# %pip install panda
# %pip install pandas-market-calendars

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
import pandas_market_calendars as mcal



# print all outputs
from IPython.core.interactiveshell import InteractiveShell
InteractiveShell.ast_node_interactivity = "all"
from scipy.stats import rankdata
from functools import reduce

from taa_lib import taa
from baa_lib import baa
from dualmom_lib import dualmom
from meta_lib import meta


def weight_recalc(reb_df, cum_weights_p):
    reb_day = reb_df.Rebalance.to_numpy()
    cum_weights = cum_weights_p.to_numpy()
    no_rows, no_cols = cum_weights_p.shape

    new_weights = np.zeros((no_rows, no_cols))
    for i in range(1, no_rows):
        for j in range(no_cols):
            if reb_day[i-1] == True:
                new_weights[i, j] = cum_weights[i-1, j]
            else:
                new_weights[i, j] = new_weights[i-1, j]

    new_weights_df = pd.DataFrame(new_weights, index = cum_weights_p.index, columns = cum_weights_p.columns)

    return new_weights_df

def lev_by_rank(rank_df, leverage_array, min_lb_years, reb_df):
    rank_mx = rank_df.to_numpy()
    no_rows, no_cols = rank_df.shape
    lb_days = min_lb_years*252
    reb_day = reb_df.Rebalance.to_numpy()
    rank_based_leverages = np.ones((no_rows, no_cols))

    for no_days in range(lb_days, no_rows):
        for etfs in range(no_cols):
            if reb_day[no_days] == True:
                rank_based_leverages[no_days, etfs] = leverage_array[rank_mx[no_days, etfs] - 1]
            else:
                rank_based_leverages[no_days, etfs] = rank_based_leverages[no_days - 1, etfs]

    rank_based_leverages_df = pd.DataFrame(rank_based_leverages, index = rank_df.index, columns = rank_df.columns)

    return rank_based_leverages_df

def yearly_sharpe(pv_p):
    daily_profit_df = (pv_p.copy()).to_frame()
    daily_profit_df.columns = ['PV']
    daily_profit_df['Year'], daily_profit_df['Month'] = daily_profit_df.index.year, daily_profit_df.index.month
    daily_profit_df['Profit'] = daily_profit_df.PV / daily_profit_df.PV.shift(1) - 1
    
    years_played = np.array(sorted(daily_profit_df.Year.unique()))
    
    no_years = len(years_played)
    no_months = 12

    avg_mtx = np.zeros((no_years, no_months))
    std_mtx = np.zeros((no_years, no_months))
    sharpe_mtx = np.zeros((no_years, no_months))

    for i in range(no_years):
        for j in range(no_months):
            avg_mtx[i, j] = daily_profit_df.loc[(daily_profit_df['Year'] <= years_played[i]) & (daily_profit_df['Month'] == j+1), 'Profit'].mean() * 252
            std_mtx[i, j] = daily_profit_df.loc[(daily_profit_df['Year'] <= years_played[i]) & (daily_profit_df['Month'] == j+1), 'Profit'].std() * np.sqrt(252)
            sharpe_mtx[i, j] = avg_mtx[i, j] / std_mtx[i, j]

    sharpe_df = pd.DataFrame(sharpe_mtx, index = years_played)
    sharpe_df.columns += 1
    return sharpe_df

def lev_by_monthly_perf(sharpe_rank, leverage_array, year_month_df, min_lb_years):
    sharpe_rank_mtx = sharpe_rank.to_numpy()
    no_years, no_months = sharpe_rank.shape
    year_month_df_to_fill = year_month_df[['Year', 'Month']]
    lb_days = min_lb_years*252

    last_day = year_month_df.index[-1].tz_convert('UTC')
    nyse = mcal.get_calendar('NYSE')
    upcom_trading_days = nyse.valid_days(start_date = last_day, end_date= last_day + dt.timedelta(days=10))
    next_trading_day = upcom_trading_days[1]

    ym_array_length = len(year_month_df_to_fill.index) + 1
    year_month_array = np.ones(ym_array_length)


    monthly_leverages = np.ones((no_years, no_months))

    for years_p in range(no_years):
        for months_p in range(no_months):
            if sharpe_rank_mtx[years_p, months_p] < 13:
                monthly_leverages[years_p, months_p] = leverage_array[sharpe_rank_mtx[years_p, months_p] - 1]

    monthly_leverages_df = pd.DataFrame(monthly_leverages, index = sharpe_rank.index, columns = sharpe_rank.columns)

    for days in range(lb_days, ym_array_length - 1):
        if year_month_df_to_fill.Year[days] > monthly_leverages_df.index[0]:
            year_month_array[days] = monthly_leverages_df.loc[year_month_df_to_fill.Year[days] - 1, year_month_df_to_fill.Month[days]]

    next_trading_day_monthly_lev = monthly_leverages_df.loc[next_trading_day.year - 1, next_trading_day.month]
    year_month_array[-1] = next_trading_day_monthly_lev
    year_month_df_to_fill['MonthlyLev'] = year_month_array[1:]
    
    return year_month_df_to_fill

def leveraged_ew_meta(meta_parameters, taa_parameters, bold_parameters, dual_mom_parameters, meta_leverage_parameters):
    pv_dct, rets_dct, weights_dct, pos_dct, cash_dct, curr_substrats_weights_dct, curr_ETF_weights_dct, adj_close_price, cum_ew_based_weights = meta(meta_parameters, taa_parameters, bold_parameters, dual_mom_parameters)
    pv_df = pd.DataFrame.from_dict(pv_dct)
    ew_pv = pv_df.ew_based
    tlt_prices = adj_close_price.TLT.to_frame()

    adj_close_price = adj_close_price[adj_close_price.index >= ew_pv.index[0]]
    adj_close_price_wo_cash = adj_close_price.copy()
    adj_close_price['cash'] = 1

    meta_rebalance_unit = meta_parameters['meta_rebalance_unit']
    meta_rebalance_freq = meta_parameters['meta_rebalance_freq']
    meta_rebalance_shift = meta_parameters['meta_rebalance_shift']

    meta_overall_leverage = meta_leverage_parameters['meta_overall_leverage']
    meta_ETF_perf_leverage_threshol = meta_leverage_parameters['meta_ETF_perf_leverage_threshold']
    meta_ETF_perf_leverage = meta_leverage_parameters['meta_ETF_perf_leverage']
    meta_monthly_seas_based_leverage = meta_leverage_parameters['meta_monthly_seas_based_leverage']
    meta_leverage_lookback_years = meta_leverage_parameters['meta_leverage_lookback_years']
    meta_tlt_based_leverage = meta_leverage_parameters['meta_tlt_based_leverage']

    meta_leverage_lookback_days = meta_leverage_lookback_years * 12 * 21
    
    df = adj_close_price.copy()
    df['Year'], df['Month'], df['Week'] = df.index.year, df.index.month, df.index.week
    df['Monthly_Rb'] = df.Month != df.Month.shift(-1)
    df['Weekly_Rb'] = df.Week != df.Week.shift(-1)
    df['Daily_Rb'] = True
    df['RebalanceUnit'] = df.Monthly_Rb if meta_rebalance_unit == 'Month' else df.Weekly_Rb if meta_rebalance_unit == 'Week' else df.Daily_Rb
    df.RebalanceUnit = df.RebalanceUnit.shift(meta_rebalance_shift).fillna(False)
    df.RebalanceUnit[0] = True
    df['NoPeriod'] = df.RebalanceUnit.shift(1).cumsum()
    df.NoPeriod[0] = 0
    df['NoData'] = np.arange(len(df)) + 1
    df['Rebalance'] = np.where((df.RebalanceUnit == True) & (df.NoPeriod.mod(meta_rebalance_freq) == 0), True, False)

    dailyretsETFs = adj_close_price / adj_close_price.shift(1) - 1

    cum_rebalance_adjusted_weights = weight_recalc(df, cum_ew_based_weights)

    dailyretsETFs_played = dailyretsETFs.where(cum_rebalance_adjusted_weights > 0, pd.NA)

    avg_rets_ETFs_all = dailyretsETFs.rolling(meta_leverage_lookback_days).mean()
    std_rets_ETFs_all = dailyretsETFs.rolling(meta_leverage_lookback_days).std()
    sharpe_ETFs_all = (avg_rets_ETFs_all / std_rets_ETFs_all * np.sqrt(252)).fillna(0)

    avg_rets_ETFs_played = dailyretsETFs_played.rolling(window = meta_leverage_lookback_days, min_periods = 1).mean()
    std_rets_ETFs_played = dailyretsETFs_played.rolling(window = meta_leverage_lookback_days, min_periods = 1).std()
    sharpe_ETFs_played = (avg_rets_ETFs_played / std_rets_ETFs_played * np.sqrt(252)).fillna(0)

    rows_sharpe, cols_sharpe = sharpe_ETFs_all.shape
    rand_mx = pd.DataFrame(np.random.rand(rows_sharpe, cols_sharpe) / 100000, index = sharpe_ETFs_all.index, columns = sharpe_ETFs_all.columns)
    sharpe_ETFs_all = sharpe_ETFs_all + rand_mx
    sharpe_ETFs_played =sharpe_ETFs_played + rand_mx

    sharpe_ETFs_all_rank = sharpe_ETFs_all.rank(axis = 1, ascending = False).astype(int)
    sharpe_ETFs_played_rank = sharpe_ETFs_played.rank(axis = 1, ascending = False).astype(int)

    no_ETFs = len(sharpe_ETFs_all.columns)
    no_ETFs_boxes = np.multiply(np.array(meta_ETF_perf_leverage_threshol), no_ETFs)
    perf_based_ETF_leverage_array = np.ones(no_ETFs)
    for pos in range(no_ETFs):
        perf_based_ETF_leverage_array[pos] = meta_ETF_perf_leverage[np.argmax(no_ETFs_boxes > pos)]

    leverages_ETFs_by_all = lev_by_rank(sharpe_ETFs_all_rank, perf_based_ETF_leverage_array, meta_leverage_lookback_years, df)
    leverages_ETFs_by_played = lev_by_rank(sharpe_ETFs_played_rank, perf_based_ETF_leverage_array, meta_leverage_lookback_years, df)

    sharpe_by_years = yearly_sharpe(ew_pv)
    sharpe_by_years_rank = sharpe_by_years.rank(axis = 1, ascending = False).fillna(99).astype(int)
    levegare_by_month = lev_by_monthly_perf(sharpe_by_years_rank, meta_monthly_seas_based_leverage, df, meta_leverage_lookback_years)

    used_leverage_by_month = levegare_by_month.MonthlyLev

    tlt_prices.columns = ['TLT']
    tlt_prices['OneM'] = tlt_prices.TLT / tlt_prices.TLT.shift(21) - 1
    tlt_prices['ThreeM'] = tlt_prices.TLT / tlt_prices.TLT.shift(63) - 1
    tlt_prices = tlt_prices[tlt_prices.index >= ew_pv.index[0]]
    no_tlt_days = len(tlt_prices.index)
    tlt_leverage_by_days = np.ones(no_tlt_days)
    for days in range(no_tlt_days):
        if ((tlt_prices.OneM[days] >= 0) & (tlt_prices.ThreeM[days] >= 0)):
            tlt_leverage_by_days[days] = meta_tlt_based_leverage[0]
        elif ((tlt_prices.OneM[days] >= 0) & (tlt_prices.ThreeM[days] < 0)):
            tlt_leverage_by_days[days] = meta_tlt_based_leverage[1]
        elif ((tlt_prices.OneM[days] < 0) & (tlt_prices.ThreeM[days] >= 0)):
            tlt_leverage_by_days[days] = meta_tlt_based_leverage[2]
        else:
            tlt_leverage_by_days[days] = meta_tlt_based_leverage[3]
    tlt_prices['TLTLev'] = tlt_leverage_by_days
    
    final_leverage_all = (leverages_ETFs_by_all * meta_overall_leverage).multiply(used_leverage_by_month, axis = 'index').multiply(tlt_prices.TLTLev, axis = 'index')
    final_leverage_played = (leverages_ETFs_by_played * meta_overall_leverage).multiply(used_leverage_by_month, axis = 'index').multiply(tlt_prices.TLTLev, axis = 'index')

    final_weights_all = cum_ew_based_weights.mul(final_leverage_all)
    final_weights_all['cash'] = 0
    cash_all = 1 - final_weights_all.sum(axis = 1)
    final_weights_played = cum_ew_based_weights.mul(final_leverage_played)
    final_weights_played['cash'] = 0
    cash_played = 1 - final_weights_played.sum(axis = 1)

    pos_all_fin, cash_all_fin, pv_all_fin = com.positions_pv(adj_close_price_wo_cash, df.Rebalance, final_weights_all, cash_all, df.index[1])
    pos_played_fin, cash_played_fin, pv_played_fin = com.positions_pv(adj_close_price_wo_cash, df.Rebalance, final_weights_played, cash_played, df.index[1])

    strat_all_rets = pv_all_fin / pv_all_fin.shift(1) - 1
    strat_played_rets = pv_played_fin / pv_played_fin.shift(1) - 1

    strat_all_weights = final_weights_all.copy()
    strat_all_weights['cash'] = cash_all
    strat_played_weights = final_weights_played.copy()
    strat_played_weights['cash'] = cash_played

    strat_all_curr_weights = strat_all_weights.iloc[-1]
    strat_played_curr_weights = strat_played_weights.iloc[-1]

    return pv_played_fin, strat_played_rets, strat_played_weights, pos_played_fin, cash_played_fin, strat_played_curr_weights, cum_ew_based_weights.iloc[-1], leverages_ETFs_by_played.iloc[-1], used_leverage_by_month.iloc[-1], tlt_prices.TLTLev.iloc[-1]