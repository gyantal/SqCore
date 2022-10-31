import pandas as pd
import numpy as np
import math

def total_return(pvs_p):

    tot_ret = pvs_p.iloc[-1] / pvs_p.iloc[0] -1

    return tot_ret

def cagr(pvs_p):

    no_years = (pvs_p.index[-1] - pvs_p.index[0]) / np.timedelta64(1, 'Y')
    
    cagrs = pvs_p.iloc[-1] ** (1/no_years) - 1

    return cagrs

def daily_returns(pvs_p):

    daily_rets = pvs_p / pvs_p.shift(1) - 1

    return daily_rets

def annual_mean_ret(pvs_p):

    daily_rets = daily_returns(pvs_p)

    annual_mr = daily_rets.mean() * 252

    return annual_mr

def annual_stand_dev(pvs_p):

    daily_rets = daily_returns(pvs_p)
    
    annual_sd = daily_rets.std() * np.sqrt(252)

    return annual_sd

def sharpe_ratio(pvs_p):

    sharpe = annual_mean_ret(pvs_p) / annual_stand_dev(pvs_p)

    return sharpe

def max_drawdown(pvs_p):

    cum_max = pvs_p.cummax()

    dds = 1 - pvs_p / cum_max

    mdd = dds.cummax().iloc[-1]

    return mdd

def mar_ratio(pvs_p):

    mar = cagr(pvs_p) / max_drawdown(pvs_p)

    return mar