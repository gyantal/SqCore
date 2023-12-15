import pandas as pd
import numpy as np
import math
import plotly.express as px
import plotly.graph_objects as go

def total_return(pvs_p):
    tot_ret = pvs_p.iloc[-1] / pvs_p.iloc[0] -1
    return tot_ret

def cagr(pvs_p):

    no_years = (pvs_p.index[-1] - pvs_p.index[0]) / np.timedelta64(1, 'D') / 365.25
    
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

    return mdd, dds

def mar_ratio(pvs_p):

    mar = cagr(pvs_p) / max_drawdown(pvs_p)[0]

    return mar

def performance_indicators(pvs_p):
    
    tot_ret = total_return(pvs_p)
    cagrs = cagr(pvs_p)
    annual_mr = annual_mean_ret(pvs_p)
    annual_sd = annual_stand_dev(pvs_p)
    sharpe = sharpe_ratio(pvs_p)
    mdd, dds = max_drawdown(pvs_p)
    mar = mar_ratio(pvs_p)

    uws = 1 - dds

    pd.set_option('display.float_format', '{:.2f}%'.format)
    print()
    print('Performance indicators of the strategy:')
    print('Total return: ', '{:.2f}%'.format(tot_ret * 100))
    print('CAGR: ', '{:.2f}%'.format(cagrs * 100))
    print('Annualized Mean Return: ', '{:.2f}%'.format(annual_mr * 100))
    print('Annualized Standard Deviation: ', '{:.2f}%'.format(annual_sd * 100))
    print('Sharpe ratio: ', '{:.3f}'.format(sharpe))
    print('Maximum drawdown: ', '{:.2f}%'.format(mdd * 100))
    print('Mar ratio: ', '{:.3f}'.format(mar))
    print()
   
    fig = px.line(pvs_p, x = pvs_p.index, y = pvs_p.values, title = 'Portfolio Value')
    fig.show()
    fig2 = px.line(uws, x=uws.index, y=uws.values, title='Underwater Plot')
    fig2.show()

    return