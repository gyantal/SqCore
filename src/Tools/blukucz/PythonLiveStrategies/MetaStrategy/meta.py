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

from taa import taa
from baa import baa
from dualmom import dualmom



def meta_perf_based_weights(substrat_rank_p, substrat_weights_p, performance_p, substrat_abs_threshold):

    ranknums = substrat_rank_p.to_numpy()
    perform = performance_p.to_numpy()
    ranknums = ranknums.astype(int)
    no_rows, no_cols = substrat_rank_p.shape
    weights = np.zeros((no_rows, no_cols))

    for i in range(no_rows):
        for j in range(no_cols):
            if perform[i, j] > substrat_abs_threshold:
                weights[i, j] = substrat_weights_p[ranknums[i, j] - 1]

    weights_df = pd.DataFrame(weights, index = substrat_rank_p.index, columns = substrat_rank_p.columns)
    cash_weights = 1 - weights_df.sum(axis = 1)
    return weights_df, cash_weights

def curr_ETF_weights(meta_curr_weights, curr_weights_substrat_dict_p):
    curr_ETF_weights_dm = (meta_curr_weights.DoubleMom * curr_weights_substrat_dict_p['DoubleMom']).to_dict()
    curr_ETF_weights_taa = (meta_curr_weights.TAA * curr_weights_substrat_dict_p['TAA']).to_dict()
    curr_ETF_weights_baa_aggdef = (meta_curr_weights.BAA_AggDef * curr_weights_substrat_dict_p['BAA_AggDef']).to_dict()
    curr_ETF_weights_baa_baldef = (meta_curr_weights.BAA_BalDef * curr_weights_substrat_dict_p['BAA_BalDef']).to_dict()
    curr_ETF_weights = {k: curr_ETF_weights_dm.get(k, 0) + curr_ETF_weights_taa.get(k, 0) + curr_ETF_weights_baa_aggdef.get(k, 0) + curr_ETF_weights_baa_baldef.get(k, 0) for k in set(curr_ETF_weights_dm) | set(curr_ETF_weights_taa) | set(curr_ETF_weights_baa_aggdef) | set(curr_ETF_weights_baa_baldef)}
    curr_cash = 1 - sum(curr_ETF_weights.values())
    curr_ETF_weights['cash'] += curr_cash
    return curr_ETF_weights

def meta(meta_parameters, taa_parameters, bold_parameters, dual_mom_parameters):

    meta_rebalance_unit = meta_parameters['meta_rebalance_unit']
    meta_rebalance_freq = meta_parameters['meta_rebalance_freq']
    meta_rebalance_shift = meta_parameters['meta_rebalance_shift']
    meta_lb_period = meta_parameters['meta_lb_period']
    meta_skipped_period = meta_parameters['meta_skipped_period']
    meta_fixed_substrat_weights = meta_parameters['meta_fixed_substrat_weights']
    meta_rel_mom_substrat_weights = meta_parameters['meta_rel_mom_substrat_weights']
    meta_sharpe_substrat_weights = meta_parameters['meta_sharpe_substrat_weights']
    meta_sortino_substrat_weights = meta_parameters['meta_sortino_substrat_weights']
    meta_rel_mom_abs_threshold = meta_parameters['meta_rel_mom_abs_threshold']
    meta_sharpe_abs_threshold = meta_parameters['meta_sharpe_abs_threshold']
    meta_sortino_abs_threshold = meta_parameters['meta_sortino_abs_threshold']
    meta_start_date = meta_parameters['meta_start_date']
    meta_end_date = meta_parameters['meta_end_date']

    taa_ticker_list = taa_parameters['taa_ticker_list']
    taa_perc_ch_lb_list = taa_parameters['taa_perc_ch_lb_list']
    taa_vol_lb = taa_parameters['taa_vol_lb']
    taa_perc_ch_up_thres = taa_parameters['taa_perc_ch_up_thres']
    taa_perc_ch_low_thres = taa_parameters['taa_perc_ch_low_thres']
    taa_rebalance_unit = taa_parameters['taa_rebalance_unit']
    taa_rebalance_freq = taa_parameters['taa_rebalance_freq']
    taa_rebalance_shift = taa_parameters['taa_rebalance_shift']

    baa_ticker_list_canary = bold_parameters['baa_ticker_list_canary']
    baa_ticker_list_defensive = bold_parameters['baa_ticker_list_defensive']
    baa_ticker_list_aggressive = bold_parameters['baa_ticker_list_aggressive']
    baa_ticker_list_balanced = bold_parameters['baa_ticker_list_balanced']
    baa_rebalance_unit = bold_parameters['baa_rebalance_unit']
    baa_rebalance_freq = bold_parameters['baa_rebalance_freq']
    baa_rebalance_shift = bold_parameters['baa_rebalance_shift']
    baa_skipped_period = bold_parameters['baa_skipped_period']
    baa_no_played_ETFs = bold_parameters['baa_no_played_ETFs']
    baa_abs_threshold = bold_parameters['baa_abs_threshold']

    dm_tickers_list = dual_mom_parameters['dm_tickers_list']
    dm_rebalance_unit = dual_mom_parameters['dm_rebalance_unit']
    dm_rebalance_freq = dual_mom_parameters['dm_rebalance_freq']
    dm_rebalance_shift = dual_mom_parameters['dm_rebalance_shift']
    dm_lb_period = dual_mom_parameters['dm_lb_period']
    dm_skipped_period = dual_mom_parameters['dm_skipped_period']
    dm_no_played_ETFs = dual_mom_parameters['dm_no_played_ETFs']
    dm_sub_rank_weights = dual_mom_parameters['dm_sub_rank_weights']
    dm_abs_threshold = dual_mom_parameters['dm_abs_threshold']

    taa_pv, taa_strat_rets, taa_weights, taa_pos, taa_cash, taa_curr_weights, taa_pv_ew, taa_strat_rets_ew, taa_pos_ew, taa_cash_ew =taa(taa_ticker_list, taa_perc_ch_lb_list, taa_vol_lb, taa_perc_ch_up_thres, taa_perc_ch_low_thres, taa_rebalance_unit, taa_rebalance_freq, taa_rebalance_shift, meta_start_date, meta_end_date)
    baa_pv, baa_strat_rets, baa_weights, baa_pos, baa_cash, baa_curr_weights, baa_pv_ew, baa_strat_rets_ew, baa_pos_ew, baa_cash_ew = baa('agg_def', baa_ticker_list_canary, baa_ticker_list_defensive, baa_ticker_list_aggressive, baa_ticker_list_balanced, baa_rebalance_unit, baa_rebalance_freq, baa_rebalance_shift, baa_skipped_period, baa_no_played_ETFs, baa_abs_threshold, meta_start_date, meta_end_date)
    baa2_pv, baa2_strat_rets, baa2_weights, baa2_pos, baa2_cash, baa2_curr_weights, baa2_pv_ew, baa2_strat_rets_ew, baa2_pos_ew, baa2_cash_ew = baa('bal_def', baa_ticker_list_canary, baa_ticker_list_defensive, baa_ticker_list_aggressive, baa_ticker_list_balanced, baa_rebalance_unit, baa_rebalance_freq, baa_rebalance_shift, baa_skipped_period, baa_no_played_ETFs, baa_abs_threshold, meta_start_date, meta_end_date)
    dm_pv, dm_strat_rets, dm_weights2, dm_pos, dm_cash, dm_curr_weights, dm_pv_ew, dm_strat_rets_ew, dm_pos_ew, dm_cash_ew = dualmom(dm_tickers_list, dm_rebalance_unit, dm_rebalance_freq, dm_rebalance_shift, dm_lb_period, dm_skipped_period, dm_no_played_ETFs, dm_sub_rank_weights, dm_abs_threshold, meta_start_date, meta_end_date)

    meta_pvs = pd.concat([dm_pv, baa_pv, baa2_pv, taa_pv], axis = 1, keys = ['DoubleMom', 'BAA_AggDef', 'BAA_BalDef', 'TAA'])
    df = meta_pvs.copy()
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

    meta_dailyret = meta_pvs / meta_pvs.shift(1) - 1

    lb_days_base = {'Month' : 21, 'Week' : 5, 'Day' : 1}
    lb_days = lb_days_base[meta_rebalance_unit] * meta_lb_period
    lb_days_with_skipped = lb_days_base[meta_rebalance_unit] * (meta_lb_period + meta_skipped_period)

    meta_rel_mom_rets = meta_pvs.shift(lb_days_base[meta_rebalance_unit] * meta_skipped_period) / meta_pvs.shift(lb_days_with_skipped) - 1
    
    avg_rets_helper = meta_dailyret.rolling(lb_days).mean()
    avg_rets = avg_rets_helper.shift(lb_days_base[meta_rebalance_unit] * meta_skipped_period)
  
    sd_rets_helper = meta_dailyret.rolling(lb_days).std()
    sd_rets = sd_rets_helper.shift(lb_days_base[meta_rebalance_unit] * meta_skipped_period)
    
    meta_neg_dailyret = (meta_dailyret).where(meta_dailyret < 0, 0)
    sd_neg_rets_helper = meta_neg_dailyret.rolling(lb_days).std()
    sd_neg_rets = sd_neg_rets_helper.shift(lb_days_base[meta_rebalance_unit] * meta_skipped_period)

    meta_sharpe = avg_rets / sd_rets * np.sqrt(252)
    meta_sortino = avg_rets / sd_neg_rets * np.sqrt(252)

    meta_sharpe.fillna(0, inplace=True)
    meta_sortino.fillna(0, inplace=True)
    
    meta_rel_mom_rets_rank_helper = meta_rel_mom_rets.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004]
    meta_sharpe_rank_helper = meta_sharpe.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004]
    meta_sortino_rank_helper = meta_sortino.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004]

    meta_rel_mom_rets_rank = meta_rel_mom_rets_rank_helper.rank(axis = 1, ascending = True)
    meta_sharpe_rank = meta_sharpe_rank_helper.rank(axis = 1, ascending = True)
    meta_sortino_rank = meta_sortino_rank_helper.rank(axis = 1, ascending = True)
    
    meta_rel_mom_substrat_weights_used = np.divide(meta_rel_mom_substrat_weights, sum(meta_rel_mom_substrat_weights))
    meta_sharpe_substrat_weights_used = np.divide(meta_sharpe_substrat_weights, sum(meta_sharpe_substrat_weights))
    meta_sortino_substrat_weights_used = np.divide(meta_sortino_substrat_weights, sum(meta_sortino_substrat_weights))
    meta_fixed_weights_used = {k: v / total for total in (sum(meta_fixed_substrat_weights.values()),) for k, v in meta_fixed_substrat_weights.items()}

    meta_fixed_weights = pd.DataFrame(meta_fixed_weights_used, index = meta_pvs.index, columns = meta_pvs.columns)
    meta_fixed_cash_weights = 1 - meta_fixed_weights.sum(axis = 1)
    meta_ew_weights = pd.DataFrame(1 / len(meta_fixed_substrat_weights), index = meta_pvs.index, columns = meta_pvs.columns)
    meta_ew_cash_weights = 1 - meta_ew_weights.sum(axis = 1)

    meta_rel_mom_based_weights, meta_rel_mom_based_cash_weights = meta_perf_based_weights(meta_rel_mom_rets_rank, meta_rel_mom_substrat_weights_used, meta_rel_mom_rets, meta_rel_mom_abs_threshold)
    meta_sharpe_based_weights, meta_sharpe_based_cash_weights = meta_perf_based_weights(meta_sharpe_rank, meta_sharpe_substrat_weights_used, meta_sharpe, meta_sharpe_abs_threshold)
    meta_sortino_based_weights, meta_sortino_based_cash_weights = meta_perf_based_weights(meta_sortino_rank, meta_sortino_substrat_weights_used, meta_sortino, meta_sortino_abs_threshold)

    pos_fixed, cash_fixed, pv_fixed = com.positions_pv(meta_pvs, df.Rebalance, meta_fixed_weights, meta_fixed_cash_weights, meta_start_date)
    pos_ew_based, cash_ew_based, pv_ew_based = com.positions_pv(meta_pvs, df.Rebalance, meta_ew_weights, meta_ew_cash_weights, meta_start_date)
    pos_rel_mom, cash_rel_mom, pv_rel_mom = com.positions_pv(meta_pvs, df.Rebalance, meta_rel_mom_based_weights, meta_rel_mom_based_cash_weights, meta_start_date)
    pos_sharpe, cash_sharpe, pv_sharpe = com.positions_pv(meta_pvs, df.Rebalance, meta_sharpe_based_weights, meta_sharpe_based_cash_weights, meta_start_date)
    pos_sortino, cash_sortino, pv_sortino = com.positions_pv(meta_pvs, df.Rebalance, meta_sortino_based_weights, meta_sortino_based_cash_weights, meta_start_date)
    pos_ew, cash_ew, pv_ew = com.positions_pv_ew(meta_pvs, df.Rebalance, meta_start_date)

    ew_rets = pv_ew / pv_ew.shift(1) - 1
    fixed_rets = pv_fixed / pv_fixed.shift(1) - 1
    ew_based_rets = pv_ew_based / pv_ew_based.shift(1) - 1
    rel_mom_rets = pv_rel_mom / pv_rel_mom.shift(1) - 1
    sharpe_rets = pv_sharpe / pv_sharpe.shift(1) - 1
    sortino_rets = pv_sortino / pv_sortino.shift(1) - 1

    fixed_weights2 = meta_fixed_weights.copy()
    fixed_weights2['cash'] = meta_fixed_cash_weights
    ew_based_weights2 = meta_ew_weights.copy()
    ew_based_weights2['cash'] = meta_ew_cash_weights
    rel_mom_weights2 = meta_rel_mom_based_weights.copy()
    rel_mom_weights2['cash'] = meta_rel_mom_based_cash_weights
    sharpe_weights2 = meta_sharpe_based_weights.copy()
    sharpe_weights2['cash'] = meta_sharpe_based_cash_weights
    sortino_weights2 = meta_sortino_based_weights.copy()
    sortino_weights2['cash'] = meta_sortino_based_cash_weights
    
    fixed_curr_weights = fixed_weights2.iloc[-1]
    ew_based_curr_weights = ew_based_weights2.iloc[-1]
    rel_mom_curr_weights = rel_mom_weights2.iloc[-1]
    sharpe_curr_weights = sharpe_weights2.iloc[-1]
    sortino_curr_weights = sortino_weights2.iloc[-1]

    curr_weights_substrat_dict = {'DoubleMom' : dm_curr_weights, 'TAA' : taa_curr_weights, 'BAA_AggDef' : baa_curr_weights, 'BAA_BalDef' : baa2_curr_weights} 
    fixed_curr_ETF_weights = curr_ETF_weights(fixed_curr_weights, curr_weights_substrat_dict)
    ew_based_curr_ETF_weights = curr_ETF_weights(ew_based_curr_weights, curr_weights_substrat_dict)
    rel_mom_curr_ETF_weights = curr_ETF_weights(rel_mom_curr_weights, curr_weights_substrat_dict)
    sharpe_curr_ETF_weights = curr_ETF_weights(sharpe_curr_weights, curr_weights_substrat_dict)
    sortino_curr_ETF_weights = curr_ETF_weights(sortino_curr_weights, curr_weights_substrat_dict)

    pv_dct = {'fixed_based' : pv_fixed, 'ew_based' : pv_ew_based, 'rel_mom_based' : pv_rel_mom, 'sharpe_based' : pv_sharpe, 'sortino_based' : pv_sortino, 'ew' : pv_ew}
    rets_dct = {'fixed_based' : fixed_rets, 'ew_based' : ew_based_rets, 'rel_mom_based' : rel_mom_rets, 'sharpe_based' : sharpe_rets, 'sortino_based' : sortino_rets, 'ew' : ew_rets}
    weights_dct = {'fixed_based' : fixed_weights2, 'ew_based' : ew_based_weights2, 'rel_mom_based' : rel_mom_weights2, 'sharpe_based' : sharpe_weights2, 'sortino_based' : sortino_weights2}
    pos_dct = {'fixed_based' : pos_fixed, 'ew_based' : pos_ew_based, 'rel_mom_based' : pos_rel_mom, 'sharpe_based' : pos_sharpe, 'sortino_based' : pos_sortino, 'ew' : pos_ew}
    cash_dct = {'fixed_based' : cash_fixed, 'ew_based' : cash_ew_based, 'rel_mom_based' : cash_rel_mom, 'sharpe_based' : cash_sharpe, 'sortino_based' : cash_sortino, 'ew' : cash_ew}
    curr_substrats_weights_dct = {'fixed_based' : fixed_curr_weights, 'ew_based' : ew_based_curr_weights, 'rel_mom_based' : rel_mom_curr_weights, 'sharpe_based' : sharpe_curr_weights, 'sortino_based' : sortino_curr_weights}
    curr_ETF_weights_dct = {'fixed_based' : fixed_curr_ETF_weights, 'ew_based' : ew_based_curr_ETF_weights, 'rel_mom_based' : rel_mom_curr_ETF_weights, 'sharpe_based' : sharpe_curr_ETF_weights, 'sortino_based' : sortino_curr_ETF_weights}

    return pv_dct, rets_dct, weights_dct, pos_dct, cash_dct, curr_substrats_weights_dct, curr_ETF_weights_dct