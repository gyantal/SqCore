
-- ********  Microsoft SQL server. Inserting VIX futures options into MsSql

>1. Check that the previous month VIX options exists or not.
SELECT * FROM [dbo].[Option] WHERE UnderlyingSubTableID = 2 AND UnderlyingAssetType = 8  AND
  ExpirationDate > '2022-04-01' AND ExpirationDate < '2022-04-30' 

>2. Insert new options.
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,0				-- 0: Put, 1: Call
           ,'2022-05-18'	-- IB shows last trading day (Tuesday), we have to put here ExpiratinDate = Wednesday. Increase IB's date by 1
           ,18				-- Strike
           ,8
           ,1);
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,0				-- 0: Put, 1: Call
           ,'2022-05-18'	-- IB shows last trading day (Tuesday), we have to put here ExpiratinDate = Wednesday. Increase IB's date by 1
           ,22				-- Strike
           ,8
           ,1);
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,1				-- 0: Put, 1: Call
           ,'2022-05-18'	-- IB shows last trading day (Tuesday), we have to put here ExpiratinDate = Wednesday. Increase IB's date by 1
           ,25				-- Strike
           ,8
           ,1);	   
INSERT INTO [dbo].[Option] ([UnderlyingAssetType],[UnderlyingSubTableID],[Flags],[ExpirationDate],[StrikePrice],[StockExchangeID],[CurrencyID])
VALUES(8,2
           ,1				-- 0: Put, 1: Call
           ,'2022-05-18'	-- IB shows last trading day (Tuesday), we have to put here ExpiratinDate = Wednesday. Increase IB's date by 1
           ,29				-- Strike
           ,8
           ,1);

3. Check that now records are inserted
SELECT * FROM [dbo].[Option] WHERE UnderlyingSubTableID = 2 AND UnderlyingAssetType = 8  AND
  ExpirationDate > '2022-05-01' AND ExpirationDate < '2022-05-30' 
