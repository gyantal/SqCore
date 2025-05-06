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
import sys
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
from functools import reduce

from taa_lib import taa
from baa_lib import baa
from dualmom_lib import dualmom
from kellerprotmom_lib import kellerprotmom
from novelltactbond_lib import novelltactbond
from haa_lib import haa



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
    curr_ETF_weights_dm = (meta_curr_weights.DualMom * curr_weights_substrat_dict_p['DualMom']).to_dict()
    curr_ETF_weights_taa = (meta_curr_weights.TAA * curr_weights_substrat_dict_p['TAA']).to_dict()
    curr_ETF_weights_baa_aggdef = (meta_curr_weights.BAA_AggDef * curr_weights_substrat_dict_p['BAA_AggDef']).to_dict()
    curr_ETF_weights_baa_baldef = (meta_curr_weights.BAA_BalDef * curr_weights_substrat_dict_p['BAA_BalDef']).to_dict()
    curr_ETF_weights_pm = (meta_curr_weights.KellerProtMom * curr_weights_substrat_dict_p['KellerProtMom']).to_dict()
    curr_ETF_weights_tb = (meta_curr_weights.NovellTactBond * curr_weights_substrat_dict_p['NovellTactBond']).to_dict()
    curr_ETF_weights_haa = (meta_curr_weights.HAA * curr_weights_substrat_dict_p['HAA']).to_dict()
    curr_ETF_weights = {k: curr_ETF_weights_dm.get(k, 0) + curr_ETF_weights_taa.get(k, 0) + curr_ETF_weights_baa_aggdef.get(k, 0) + curr_ETF_weights_baa_baldef.get(k, 0) + curr_ETF_weights_pm.get(k, 0) + curr_ETF_weights_tb.get(k, 0) + curr_ETF_weights_haa.get(k, 0) for k in set(curr_ETF_weights_dm) | set(curr_ETF_weights_taa) | set(curr_ETF_weights_baa_aggdef) | set(curr_ETF_weights_baa_baldef) | set(curr_ETF_weights_pm) | set(curr_ETF_weights_tb) | set(curr_ETF_weights_taa) }
    curr_cash = 1 - sum(curr_ETF_weights.values())
    curr_ETF_weights['cash'] += curr_cash
    return curr_ETF_weights

def cum_ETF_weights(meta_weights, weights_substrat_dict_p):
    ETF_weights_dm = (weights_substrat_dict_p['DualMom'].multiply(meta_weights.DualMom.to_numpy(), axis = 0))
    ETF_weights_taa = (weights_substrat_dict_p['TAA'].multiply(meta_weights.TAA.to_numpy(), axis = 0))
    ETF_weights_baa_aggdef = (weights_substrat_dict_p['BAA_AggDef'].multiply(meta_weights.BAA_AggDef.to_numpy(), axis = 0))
    ETF_weights_baa_baldef = (weights_substrat_dict_p['BAA_BalDef'].multiply(meta_weights.BAA_BalDef.to_numpy(), axis = 0))
    ETF_weights_pm = (weights_substrat_dict_p['KellerProtMom'].multiply(meta_weights.KellerProtMom.to_numpy(), axis = 0))
    ETF_weights_tb = (weights_substrat_dict_p['NovellTactBond'].multiply(meta_weights.NovellTactBond.to_numpy(), axis = 0))
    ETF_weights_haa = (weights_substrat_dict_p['HAA'].multiply(meta_weights.HAA.to_numpy(), axis = 0))
    ETF_weights = reduce(lambda x, y: x.add(y, fill_value = 0), [ETF_weights_dm, ETF_weights_taa, ETF_weights_baa_aggdef, ETF_weights_baa_baldef, ETF_weights_pm, ETF_weights_tb, ETF_weights_haa])
    cash = 1 - ETF_weights.sum(axis = 1)
    ETF_weights['cash'] += cash
    return ETF_weights

def meta(meta_parameters, taa_parameters, bold_parameters, dual_mom_parameters, keller_protmom_parameters, novell_tactbond_parameters, hybrid_aa_parameters):

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

    protmom_tickers_list = keller_protmom_parameters['protmom_tickers_list']
    protmom_rebalance_unit = keller_protmom_parameters['protmom_rebalance_unit']
    protmom_rebalance_freq = keller_protmom_parameters['protmom_rebalance_freq']
    protmom_rebalance_shift = keller_protmom_parameters['protmom_rebalance_shift']
    protmom_correl_lb_months = keller_protmom_parameters['protmom_correl_lb_months']
    protmom_lb_periods = keller_protmom_parameters['protmom_lb_periods']
    protmom_lb_weights = keller_protmom_parameters['protmom_lb_weights']
    protmom_selected_ETFs = keller_protmom_parameters['protmom_selected_ETFs']

    tactbond_tickers_list = novell_tactbond_parameters['tactbond_tickers_list']
    tactbond_rebalance_unit = novell_tactbond_parameters['tactbond_rebalance_unit']
    tactbond_rebalance_freq = novell_tactbond_parameters['tactbond_rebalance_freq']
    tactbond_rebalance_shift = novell_tactbond_parameters['tactbond_rebalance_shift']
    tactbond_absolute_threshold = novell_tactbond_parameters['tactbond_absolute_threshold']
    tactbond_threshold_type = novell_tactbond_parameters['tactbond_threshold_type']
    tactbond_cash_subs = novell_tactbond_parameters['tactbond_cash_subs']
    tactbond_lb_periods = novell_tactbond_parameters['tactbond_lb_periods']
    tactbond_lb_weights = novell_tactbond_parameters['tactbond_lb_weights']
    tactbond_selected_ETFs = novell_tactbond_parameters['tactbond_selected_ETFs']

    haa_ticker_list_canary = hybrid_aa_parameters['haa_ticker_list_canary']
    haa_ticker_list_defensive = hybrid_aa_parameters['haa_ticker_list_defensive']
    haa_ticker_list_offensive = hybrid_aa_parameters['haa_ticker_list_offensive']
    haa_rebalance_unit = hybrid_aa_parameters['haa_rebalance_unit']
    haa_rebalance_freq = hybrid_aa_parameters['haa_rebalance_freq']
    haa_rebalance_shift = hybrid_aa_parameters['haa_rebalance_shift']
    haa_skipped_period = hybrid_aa_parameters['haa_skipped_period']
    haa_no_played_ETFs = hybrid_aa_parameters['haa_no_played_ETFs']
    haa_abs_threshold = hybrid_aa_parameters['haa_abs_threshold']

    taa_pv, taa_strat_rets, taa_weights, taa_pos, taa_cash, taa_curr_weights, taa_pv_ew, taa_strat_rets_ew, taa_pos_ew, taa_cash_ew =taa(taa_ticker_list, taa_perc_ch_lb_list, taa_vol_lb, taa_perc_ch_up_thres, taa_perc_ch_low_thres, taa_rebalance_unit, taa_rebalance_freq, taa_rebalance_shift, meta_start_date, meta_end_date)
    baa_pv, baa_strat_rets, baa_weights, baa_pos, baa_cash, baa_curr_weights, baa_pv_ew, baa_strat_rets_ew, baa_pos_ew, baa_cash_ew = baa('agg_def', baa_ticker_list_canary, baa_ticker_list_defensive, baa_ticker_list_aggressive, baa_ticker_list_balanced, baa_rebalance_unit, baa_rebalance_freq, baa_rebalance_shift, baa_skipped_period, baa_no_played_ETFs, baa_abs_threshold, meta_start_date, meta_end_date)
    baa2_pv, baa2_strat_rets, baa2_weights, baa2_pos, baa2_cash, baa2_curr_weights, baa2_pv_ew, baa2_strat_rets_ew, baa2_pos_ew, baa2_cash_ew = baa('bal_def', baa_ticker_list_canary, baa_ticker_list_defensive, baa_ticker_list_aggressive, baa_ticker_list_balanced, baa_rebalance_unit, baa_rebalance_freq, baa_rebalance_shift, baa_skipped_period, baa_no_played_ETFs, baa_abs_threshold, meta_start_date, meta_end_date)
    dm_pv, dm_strat_rets, dm_weights, dm_pos, dm_cash, dm_curr_weights, dm_pv_ew, dm_strat_rets_ew, dm_pos_ew, dm_cash_ew = dualmom(dm_tickers_list, dm_rebalance_unit, dm_rebalance_freq, dm_rebalance_shift, dm_lb_period, dm_skipped_period, dm_no_played_ETFs, dm_sub_rank_weights, dm_abs_threshold, meta_start_date, meta_end_date)
    pm_pv, pm_strat_rets, pm_weights, pm_pos, pm_cash, pm_curr_weights, pm_pv_ew, pm_strat_rets_ew, pm_pos_ew, pm_cash_ew = kellerprotmom(protmom_tickers_list, protmom_rebalance_unit, protmom_rebalance_freq, protmom_rebalance_shift, protmom_correl_lb_months, protmom_lb_periods, protmom_lb_weights, protmom_selected_ETFs, meta_start_date, meta_end_date)
    tb_pv, tb_strat_rets, tb_weights, tb_pos, tb_cash, tb_curr_weights, tb_pv_ew, tb_strat_rets_ew, tb_pos_ew, tb_cash_ew = novelltactbond(tactbond_tickers_list, tactbond_rebalance_unit, tactbond_rebalance_freq, tactbond_rebalance_shift, tactbond_absolute_threshold, tactbond_threshold_type, tactbond_cash_subs, tactbond_lb_periods, tactbond_lb_weights, tactbond_selected_ETFs, meta_start_date, meta_end_date)
    haa_pv, haa_strat_rets, haa_weights, haa_pos, haa_cash, haa_curr_weights, haa_pv_ew, haa_strat_rets_ew, haa_pos_ew, haa_cash_ew = haa(haa_ticker_list_canary, haa_ticker_list_defensive, haa_ticker_list_offensive, haa_rebalance_unit, haa_rebalance_freq, haa_rebalance_shift, haa_skipped_period, haa_no_played_ETFs, haa_abs_threshold, meta_start_date, meta_end_date)

    # If YF missing a day for all ETFs then it doesn't return that day, and that substrategy pv.Length is smaller. Usually if SPY is queried we have all days. But NovelTactBond doesn't query SPY.
    isAllSubStrategyHasSameDays = (taa_pv.size == baa_pv.size) and (taa_pv.size == baa2_pv.size) and (taa_pv.size == dm_pv.size) and (taa_pv.size == pm_pv.size) and (taa_pv.size == tb_pv.size) and (taa_pv.size == haa_pv.size)
    if not isAllSubStrategyHasSameDays:
        errMsg = f'taa_pv.size: {taa_pv.size}, baa_pv.size: {baa_pv.size}, baa2_pv.size: {baa2_pv.size}, dm_pv.size: {dm_pv.size}, pm_pv.size: {pm_pv.size}, tb_pv.size: {tb_pv.size}, haa_pv.size: {haa_pv.size}'
        print('SqError. Not all strategy has the same number of days. This usually happens if one (or almost all except SPY) ETF price is missing from YF. There is not much to do. Wait until YF has all the historical data for all ETFs. ' + errMsg)
        sys.exit()

    taa_weights = taa_weights.groupby(taa_weights.columns, axis = 1).sum()
    baa_weights = baa_weights.groupby(baa_weights.columns, axis = 1).sum()
    baa2_weights = baa2_weights.groupby(baa2_weights.columns, axis = 1).sum()
    dm_weights = dm_weights.groupby(dm_weights.columns, axis = 1).sum()
    pm_weights = pm_weights.groupby(pm_weights.columns, axis = 1).sum()
    tb_weights = tb_weights.groupby(tb_weights.columns, axis = 1).sum()
    haa_weights = haa_weights.groupby(haa_weights.columns, axis = 1).sum()

    list_total = list(set(taa_ticker_list + baa_ticker_list_aggressive + baa_ticker_list_balanced + baa_ticker_list_defensive + dm_tickers_list + protmom_tickers_list + tactbond_tickers_list + haa_ticker_list_canary + haa_ticker_list_defensive + haa_ticker_list_offensive))
    adj_close_price = yf.download(list_total,start = pd.to_datetime(meta_start_date) + pd.DateOffset(years= -2),end = pd.to_datetime(meta_end_date) + pd.DateOffset(days= 1), auto_adjust=False )['Adj Close'] # 2025-02-27: yf API changed. The default auto_adjust=True gives only adjusted OHLC, not giving AdjClose, so impossible to reverse engineer the splits, dividindends and rawPrices. The auto_adjust=false gives OHLC (raw) + 'Adj Close'.

    # get the last 63 rows of the dataframe and check for NaN values - last 3 months
    last_63_rows = adj_close_price.tail(63)
    if last_63_rows.isna().any(axis=0).any():
        # get the names of the columns containing NaN values
        columns_with_nan = last_63_rows.columns[last_63_rows.isna().any()].tolist()
        print(f"The following ticker(s) has missing prices in the last 3 months: {columns_with_nan}")

    meta_pvs = pd.concat([dm_pv, baa_pv, baa2_pv, taa_pv, pm_pv, tb_pv, haa_pv], axis = 1, keys = ['DualMom', 'BAA_AggDef', 'BAA_BalDef', 'TAA', 'KellerProtMom', 'NovellTactBond', 'HAA'])
    df = meta_pvs.copy()
    df['Year'], df['Month'], df['Week'] = df.index.year, df.index.month, df.index.isocalendar().week
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
    
    meta_rel_mom_rets_rank_helper = meta_rel_mom_rets.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007]
    meta_sharpe_rank_helper = meta_sharpe.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007]
    meta_sortino_rank_helper = meta_sortino.rank(axis = 1, ascending = False) + [0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007]

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
    
    all_weights_substrat_dict = {'DualMom' : dm_weights[dm_weights.index >= ew_based_weights2.index[0]], 'TAA' : taa_weights[taa_weights.index >= ew_based_weights2.index[0]], 'BAA_AggDef' : baa_weights[baa_weights.index >= ew_based_weights2.index[0]], 'BAA_BalDef' : baa2_weights[baa2_weights.index >= ew_based_weights2.index[0]], 'KellerProtMom' : pm_weights[pm_weights.index >= ew_based_weights2.index[0]], 'NovellTactBond' : tb_weights[tb_weights.index >= ew_based_weights2.index[0]], 'HAA' : haa_weights[haa_weights.index >= ew_based_weights2.index[0]]}

    cum_ew_based_weights = cum_ETF_weights(ew_based_weights2, all_weights_substrat_dict)
    cum_fixed_based_weights = cum_ETF_weights(fixed_weights2, all_weights_substrat_dict)
    cum_rel_mom_based_weights = cum_ETF_weights(rel_mom_weights2, all_weights_substrat_dict)
    cum_sharpe_based_weights = cum_ETF_weights(sharpe_weights2, all_weights_substrat_dict)
    cum_sortino_based_weights = cum_ETF_weights(sortino_weights2, all_weights_substrat_dict)  

    fixed_curr_weights = fixed_weights2.iloc[-1]
    ew_based_curr_weights = ew_based_weights2.iloc[-1]
    rel_mom_curr_weights = rel_mom_weights2.iloc[-1]
    sharpe_curr_weights = sharpe_weights2.iloc[-1]
    sortino_curr_weights = sortino_weights2.iloc[-1]

    curr_weights_substrat_dict = {'DualMom' : dm_curr_weights, 'TAA' : taa_curr_weights, 'BAA_AggDef' : baa_curr_weights, 'BAA_BalDef' : baa2_curr_weights, 'KellerProtMom' : pm_curr_weights, 'NovellTactBond' : tb_curr_weights, 'HAA' : haa_curr_weights} 
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
    cum_ETF_weights_dct = {'fixed_based' : cum_fixed_based_weights, 'ew_based' : cum_ew_based_weights, 'rel_mom_based' : cum_rel_mom_based_weights, 'sharpe_based' : cum_sharpe_based_weights, 'sortino_based' : cum_sortino_based_weights}

    return pv_dct, rets_dct, weights_dct, pos_dct, cash_dct, curr_substrats_weights_dct, curr_ETF_weights_dct, adj_close_price, cum_ETF_weights_dct