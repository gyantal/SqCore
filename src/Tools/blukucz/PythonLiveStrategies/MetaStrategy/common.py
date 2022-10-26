# Importing necessary libraries
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import yfinance as yf
import pyfolio as pf
import datetime as dt
import pandas_datareader.data as web
import os

def positions_pv(acp_p, rebalance_p, weights_p, cash_weight_p, start_date_p):
    adjclose = acp_p.fillna(1).to_numpy()
    rebalanceday = rebalance_p.to_numpy()
    weights = weights_p.fillna(0).to_numpy()
    cash_weight = cash_weight_p.to_numpy()
    no_rows, no_cols = acp_p.shape

    positions = np.zeros((no_rows, no_cols))
    pv = np.ones((no_rows,1))
    cash = np.ones((no_rows,1))
    start_ind = np.argmax(acp_p.index >= start_date_p)
    for i in range(start_ind, no_rows):
        pv[i] = 0
        for j in range(no_cols):
            if rebalanceday[i-1] == True:
                positions[i,j] = pv[i-1] * weights[i-1,j] / adjclose[i-1,j]                
            else:
                positions[i,j] = positions[i-1,j]
            pv[i] += positions[i,j] * adjclose[i,j]
        if rebalanceday[i-1] == True:
            cash[i] = pv[i-1] * cash_weight[i-1]
        else:
            cash[i] = cash[i-1]
        pv[i] += cash[i] 
    
    positions_df = pd.DataFrame(positions, index = acp_p.index, columns = acp_p.columns)
    positions_df_sel = positions_df.iloc[start_ind-1:no_rows,:]
    pv_df =  pd.DataFrame(pv, index = acp_p.index)
    pv_df_sel = pv_df.iloc[start_ind-1:no_rows,0]
    cash_df =  pd.DataFrame(cash, index = acp_p.index)
    cash_df_sel = cash_df.iloc[start_ind-1:no_rows,0]
    return positions_df_sel, cash_df_sel, pv_df_sel

def positions_pv_ew(acp_p, rebalance_p, start_date_p):
    adjclose = acp_p.fillna(0).to_numpy()
    rebalanceday = rebalance_p.to_numpy()
    no_rows, no_cols = acp_p.shape
    no_ava_etfs = (acp_p > 0).sum(1)

    positions = np.zeros((no_rows, no_cols))
    pv = np.ones((no_rows,1))
    cash = np.ones((no_rows,1))
    start_ind = np.argmax(acp_p.index >= start_date_p)
    for i in range(start_ind, no_rows):
        pv[i] = 0
        for j in range(no_cols):
            if rebalanceday[i-1] == True:
                positions[i,j] = pv[i-1] * (1 / no_ava_etfs[i-1]) / adjclose[i-1,j] if adjclose[i-1, j] > 0 else 0                
            else:
                positions[i,j] = positions[i-1,j]
            pv[i] += positions[i,j] * adjclose[i,j]
        if rebalanceday[i-1] == True:
            cash[i] = 0
        else:
            cash[i] = 0
        pv[i] += cash[i] 
    
    positions_df = pd.DataFrame(positions, index = acp_p.index, columns = acp_p.columns)
    positions_df_sel = positions_df.iloc[start_ind-1:no_rows,:]
    pv_df =  pd.DataFrame(pv, index = acp_p.index)
    pv_df_sel = pv_df.iloc[start_ind-1:no_rows,0]
    cash_df =  pd.DataFrame(cash, index = acp_p.index)
    cash_df_sel = cash_df.iloc[start_ind-1:no_rows,0]
    return positions_df_sel, cash_df_sel, pv_df_sel