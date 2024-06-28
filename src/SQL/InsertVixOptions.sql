
-- ********  Microsoft SQL server. Inserting VIX futures options into MsSql
-- At expiration, VRO data is needed: CBOE Index Settlement Values: https://www.cboe.com/index_settlement_values/

>1. Check that the previous month VIX options exists or not.
SELECT * FROM [dbo].[Option] WHERE UnderlyingSubTableID = 2 AND UnderlyingAssetType = 8  AND
  ExpirationDate > '2022-05-01' AND ExpirationDate < '2022-05-30' 

>2. Insert new options.
DECLARE @NewExpirationDate AS VARCHAR(100)
SELECT @NewExpirationDate = '2022-06-15' -- IB shows last trading day (Tuesday), we have to put here ExpirationDate = Wednesday. Increase IB's date by 1

INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,0				-- 0: Put, 1: Call
           ,@NewExpirationDate
           ,18				-- Strike
           ,8
           ,1);
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,0				-- 0: Put, 1: Call
           ,@NewExpirationDate
           ,22				-- Strike
           ,8
           ,1);
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,1				-- 0: Put, 1: Call
           ,@NewExpirationDate
           ,25				-- Strike
           ,8
           ,1);	   
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,1				-- 0: Put, 1: Call
           ,@NewExpirationDate
           ,29				-- Strike
           ,8
           ,1);

3. Check that now records are inserted
SELECT * FROM [dbo].[Option] WHERE UnderlyingSubTableID = 2 AND UnderlyingAssetType = 8  AND
  ExpirationDate > '2022-06-01' AND ExpirationDate < '2022-06-30' 
