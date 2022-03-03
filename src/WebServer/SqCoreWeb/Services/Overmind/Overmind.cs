using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace SqCoreWeb
{
    public enum OvermindTaskSettingAction : byte
    {
        Unknown = 0, MorningCheck, MiddayCheck
    }

    public class Overmind
    {
        public static readonly Overmind gOvermind = new();

        public void Init()
        {
            Utils.Logger.Info("****Overmind:Init()");
            var sqTask = new SqTask()
            {
                Name = "Overmind",
                ExecutionFactory = OvermindExecution.ExecutionFactoryCreate,
            };
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "MorningCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.Daily,
                Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnAbsoluteTimeMidnightUtc, TimeOffset = TimeSpan.FromMinutes(9 * 60 + 5) },  // Activate every day 9:05 UTC,
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, OvermindTaskSettingAction.MorningCheck } }
            });
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "MiddayCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.Daily,
                Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnAbsoluteTimeMidnightUtc, TimeOffset = TimeSpan.FromMinutes(16 * 60 + 45) },  // Activate every day 16:45 UTC
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, OvermindTaskSettingAction.MiddayCheck } }
            });
            SqTaskScheduler.gSqTasks.Add(sqTask);
        }

        public void Exit()
        {
            Utils.Logger.Info("****Overmind:Exit()");
        }
    }

   
    public class OvermindExecution : SqExecution
    {
        public static SqExecution ExecutionFactoryCreate()
        {
            return new OvermindExecution();
        }

        public override void Run()  // try/catch is only necessary if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
        {
            Utils.Logger.Info($"OvermindExecution.Run() BEGIN, Trigger: '{Trigger?.Name ?? string.Empty}'");
            Console.WriteLine($"OvermindExecution.Run() BEGIN, Trigger: '{Trigger?.Name ?? string.Empty}'");
            // HealthMonitor.Exe is running on VBrokerDev server. It will stay there, so it can report if SqCore server is down.
            // However, it is too much work to set up its firewall port filter for all developers every time when a developer IP changes. 
            // So, in local development, we only run the HealthMonitorCheck for 1 developer. So, we don't receive "HealthMonitor is NOT Alive." warning emails all the time.
            if (Utils.RunningPlatform() == Platform.Linux || (Utils.RunningPlatform() == Platform.Windows && Environment.UserName == "gyantal"))
                CheckHealthMonitorAlive();

            OvermindTaskSettingAction action = OvermindTaskSettingAction.Unknown;
            if (Trigger!.TriggerSettings.TryGetValue(TaskSetting.ActionType, out object? actionObj))
                action = (OvermindTaskSettingAction)actionObj;
            if (action == OvermindTaskSettingAction.MorningCheck)
                MorningCheck();
            else if (action == OvermindTaskSettingAction.MiddayCheck)
                MiddayCheck();
        }
        async void CheckHealthMonitorAlive()
        {
            bool isHealthMonitorAlive = false;
            Task<string?> tcpMsgTask = TcpMessage.Send(string.Empty, (int)HealthMonitorMessageID.Ping, ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort);
            string? tcpMsgResponse = await tcpMsgTask;
            Utils.Logger.Debug("CheckHealthMonitorAlive() returned answer: " + tcpMsgResponse ?? string.Empty);
            Console.WriteLine($"HealthMonitor Ping return: '{tcpMsgResponse ?? string.Empty}'");
            if (tcpMsgTask.Exception != null || String.IsNullOrEmpty(tcpMsgResponse))
            {
                string errorMsg = $"Error. CheckHealthMonitorAlive() to {ServerIp.HealthMonitorPublicIp}:{ServerIp.DefaultHealthMonitorServerPort}";
                Utils.Logger.Error(errorMsg);
            } else
                isHealthMonitorAlive = tcpMsgResponse.StartsWith("Ping. Healthmonitor UtcNow: ");

            if (!isHealthMonitorAlive)
                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = "SqCore Warning! : HealthMonitor is NOT Alive.",
                    Body = $"SqCore Warning! : HealthMonitor is NOT Alive.",
                    IsBodyHtml = false
                }.SendAsync().RunInSameThreadButReturnAtFirstAwaitAndLogError();
        }

        
        async void MorningCheck()
        {
            string todayMonthAndDayStr = DateTime.UtcNow.ToString("MM-dd");
            if (todayMonthAndDayStr == "10-05")        // Orsi's birthday
                await new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = "SqCore.Overmind: Orsi's birthday", Body = "Orsi's birthday is on 1976-10-09.", IsBodyHtml = false }.SendAsync();

            Utils.Logger.Info("Overmind.MorningCheck(): Checking first day of the month");
            if (DateTime.UtcNow.AddDays(0).Day == 1)
            {
                // B. asked me to never send salaries on 30th or 31st of previous month. 
                // So I will report to accountant only on 1st day of every month, and maybe he will get it later. 
                // And this has an advantage that as I don't send the holidays report earlier, if they forget to tell me their 'last minute' holiday day-offs, it is not reported to accountant too early.
                // So less headache overall.
                new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = "SqCore.Overmind: send holidays, bank report to accountant", Body = "Send holidays, bank report to accountant. In 3 days, it is the 1st day of the month.", IsBodyHtml = false }.Send();
            }
            if ((new int[] { 11, 12, 1, 2, 3}).Contains(DateTime.UtcNow.Month) && (DateTime.UtcNow.Day == 1 || DateTime.UtcNow.Day == 16))
            {
                // every 2 weeks in winter, if I don't use the car, the battery is depleted. Charge it on Saturday.
                new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = "SqCore.Overmind: Charge Car battery in winter", Body = "Warning in every 2 weeks in winter: Charge car battery on Saturdays, otherwise you have to buy a battery every 2 years. (a lot of time to disassemble battery)", IsBodyHtml = false }.Send();
            }

            //double? price = GetAmazonProductPrice("https://www.amazon.co.uk/Electronics-Sennheiser-Professional-blocking-gaming-headset-Black/dp/B00JQDOANK/");
            //if (price == null || price <= 150.0)
            //{
            //    new Email
            //    {
            //        ToAddresses = Utils.Configuration["Emails:Gyant"],
            //        Subject = "SqCore.Overmind: Amazon price warning.",
            //        Body = (price == null) ?
            //            $"GetAmazonProductPrice() couldn't obtain current price. Check log file.":
            //            $"Time to buy Sennheiser GAME ZERO now. Amazon price dropped from 199.99 to {price}. Go to https://www.amazon.co.uk/Electronics-Sennheiser-Professional-blocking-gaming-headset-Black/dp/B00JQDOANK/ and buy headset now. See '2016-05, Sennheiser buying.txt'.",
            //        IsBodyHtml = false
            //    }.Send();
            //}
        }

        void MiddayCheck()
        {
            // TODO: if market holiday: it shouldn't process anything either
            if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Saturday || DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
            {
                Utils.Logger.Debug("Overmind.MiddayCheck(). Weekend is detected. Don't do a thing.");
                return;
            }

            CheckIfTomorrowIsMonthlyOptionExpirationDay();
            CheckIntradayStockPctChanges(); // if we don't wait the async method, it returns very quickly, but later it continues in a different threadpool bck thread, but then it loses stacktrace, and exception is not caught in try/catch, but it becomes an AppDomain_BckgThrds_UnhandledException() 
            CheckPriorClosePrices();
        }

        void CheckIfTomorrowIsMonthlyOptionExpirationDay()
        {
            // Expiration date: USA: 3rd Friday of the month. When that Friday falls on a holiday, the expiration date is on the Thursday immediately before.
            DateTime tomorrowDateUtc = DateTime.UtcNow.AddDays(1);
            if (tomorrowDateUtc.DayOfWeek != DayOfWeek.Friday)
                return;
            // method 1: if it is Friday, subtract 7 days backwards 2 times and check if that is a positive number. However, subtracting another 7 days should be a negative number
            // method 2: think about possible day of the months.
            //          > If 1st day of the month is Friday, then the 3rd Friday is: 1+7+7=15th  (it happened in 2018-06), that is the earliest possible date.
            //          > if 1st day of the month Saturday, then the 1rd Friday is 7th. The 3rd Friday is 7+7+7=21th, that is the largest day possible for the 3rd Friday
            if (tomorrowDateUtc.Day < 15 || tomorrowDateUtc.Day > 21)
                return;
            // if we are here, it is Friday and dayOfMonth in [15,21], which is the 3rd Friday
            string subject = "SqCore: Monthly Option expiration. Trade HarryLong 2 manually!";
            StringBuilder sb = new(Email.g_htmlEmailStart);
            sb.Append(
                @"<small>Because of the EU PRIIPs (KID) Regulation, IB UK doesn't allow US domiciled ETFs like SPY, QQQ, VXX to buy until the account is worth less than 500K EUR. Workaround is options.<br/>
- for shorting VXX stock, we buy VXX Put option (cheap, very close to expiration, when time value is tiny), then we let it expire or force-exercise. The result is 100 short VXX stock immediately. It WORKED !, because it is technically not a stock shorting/buying, and EU regulation protect Funds (ETF) only, and when you trade options you are assumed to be sophisticated investor already.<br/>
- only 100 stocks in batches can be obtained, but we can even go more granular by exercising 1 option for 100 shares, then liquidating 2/3rd of it instantly. Liquidation is allowed.<br/>
- Don't fret if it is rebalanced only after 2 months. Actually it (SR) is better.<br/>
&nbsp;&nbsp;&nbsp;&nbsp;Daily,1d: CAGR: 58.24%, SR 1.14<br/>&nbsp;&nbsp;&nbsp;&nbsp;Daily,20d: CAGR: 59.09%, SR 1.16<br/>&nbsp;&nbsp;&nbsp;&nbsp;Daily,40d: CAGR: 59.53%, SR 1.19 
</small><br/> 
<h2>ToDo:</h2>
<ul>
<li>VBroker: uncomment HarryLong scheduling, run HarryLong simulation locally, that calculates the proposed number of shares to trade. Comment out HarryLong scheduling again (so that it is not sent to the server later).</li>
<li>Round the number of stocks up to the nearest 100 or just ignore them if tiny. Buy options and force exercise them. Liquidate the unnecessary stock parts.</li>
<li>Register trades as stocks into SQDeskop.</li>
</ul>");
            sb.Append(Email.g_htmlEmailEnd);

            string emailHtmlBody = sb.ToString();
            new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
        }

        void CheckIntradayStockPctChanges()
        {
            string gyantalEmailInnerlStr = string.Empty;
            string gyantalPhoneCallInnerStr = string.Empty;
            string charmatEmailInnerlStr = string.Empty;
            string charmatPhoneCallInnerStr = string.Empty;

            double kwebTodayPctChange = GetTodayPctChange("KWEB");
            if (Math.Abs(kwebTodayPctChange) >= 0.04)
            {
                gyantalEmailInnerlStr += "KWEB price warning: bigger than usual move. In percentage: " + (kwebTodayPctChange * 100).ToString("0.00") + @"%." + Environment.NewLine;
                gyantalPhoneCallInnerStr += "the ticker K W E B, ";
            }

            double vxxTodayPctChange = GetTodayPctChange("VXX");
            if (Math.Abs(vxxTodayPctChange) >= 0.06)
            {
                gyantalEmailInnerlStr += "VXX price warning: bigger than usual move. In percentage: " + (vxxTodayPctChange * 100).ToString("0.00") + @"%";
                gyantalPhoneCallInnerStr += "the ticker V X X ";
                charmatEmailInnerlStr += "VXX price warning: bigger than usual move. In percentage: " + (vxxTodayPctChange * 100).ToString("0.00") + @"%";
                charmatPhoneCallInnerStr += "the ticker V X X ";
            }

            double amznTodayPctChange = GetTodayPctChange("AMZN");
            if (Math.Abs(amznTodayPctChange) >= 0.04)
            {
                charmatEmailInnerlStr += "Amazon price warning: bigger than usual move. In percentage: " + (amznTodayPctChange * 100).ToString("0.00") + @"%." + Environment.NewLine;
                charmatPhoneCallInnerStr += "the ticker Amazon, ";
            }

            double googleTodayPctChange = GetTodayPctChange("GOOGL");
            if (Math.Abs(googleTodayPctChange) >= 0.04)
            {
                charmatEmailInnerlStr += "Google price warning: bigger than usual move. In percentage: " + (googleTodayPctChange * 100).ToString("0.00") + @"%." + Environment.NewLine;
                charmatPhoneCallInnerStr += "the ticker Google, ";
            }

            if (!String.IsNullOrEmpty(gyantalEmailInnerlStr))
            {
                new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = "SqCore: Price Warning", Body = gyantalEmailInnerlStr, IsBodyHtml = false }.Send();
                var call = new PhoneCall
                {
                    FromNumber = Caller.Gyantal,
                    ToNumber = PhoneCall.PhoneNumbers[Caller.Gyantal],
                    Message = "This is a warning notification from SnifferQuant. There's a large up or down movement in " + gyantalPhoneCallInnerStr + " ... I repeat " + gyantalPhoneCallInnerStr,
                    NRepeatAll = 2
                };
                Console.WriteLine("call.MakeTheCall() return: " + call.MakeTheCallAsync().TurnAsyncToSyncTask());
            }

            if (!String.IsNullOrEmpty(charmatEmailInnerlStr))
            {
                new Email { ToAddresses = Utils.Configuration["Emails:Charm0"], Subject = "SqCore: Price Warning", Body = charmatEmailInnerlStr, IsBodyHtml = false }.Send();
                var call = new PhoneCall
                {
                    FromNumber = Caller.Gyantal,
                    ToNumber = PhoneCall.PhoneNumbers[Caller.Charmat0],
                    Message = "This is a warning notification from SnifferQuant. There's a large up or down movement in " + charmatPhoneCallInnerStr + " ... I repeat " + charmatPhoneCallInnerStr,
                    NRepeatAll = 2
                };
                Console.WriteLine("call.MakeTheCall() return: " + call.MakeTheCallAsync().TurnAsyncToSyncTask());
            }
        }

        // 2017-11-02: YF is discontinued (V7 API uses crumbs), GF uses cookies two (although it is fast, and it is real-time), decided to use CNBC for a while
        // We could do https://www.snifferquant.net/YahooFinanceForwarder?yffOutFormat=csv ..., but that depends on the web service and we don't want this Overmind to depend on a website
        // 2021-03-18: kept www.cnbc.com, because this way this code doesn't depend on MemDb (it can be outsourced later); And more Tickers can be checked, which are not in MemDb at all
        private static double GetTodayPctChange(string p_exchangeWithTicker)    // for GoogleFinance: TSE:VXX is the Toronto stock exchange, we need "NYSEARCA:VXX"
        {
            Utils.Logger.Info("GetTodayPctChange(): " + p_exchangeWithTicker);
            // https://finance.google.com/finance?q=BATS%3AVXX
            string url = $"https://www.cnbc.com/quotes/?symbol=" + p_exchangeWithTicker.Replace(":", "%3A");
            Utils.Logger.Trace("DownloadStringWithRetry() queried with:'" + url + "'");
            string? priceHtml = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            if (priceHtml == null)
                return double.NaN;

            string firstCharsWithSubString = !String.IsNullOrWhiteSpace(priceHtml!) && priceHtml.Length >= 300 ? priceHtml[..300] : priceHtml;
            Utils.Logger.Trace("HttpClient().GetStringAsync returned: " + firstCharsWithSubString);

            int iLastPriceStart = priceHtml.IndexOf($"\"last\":\"");
            if (iLastPriceStart != -1)
            {
                iLastPriceStart += $"\"last\":\"".Length;
                int iLastPriceEnd = priceHtml.IndexOf("\"", iLastPriceStart);
                if (iLastPriceEnd != -1)
                {
                    var lastPriceStr = priceHtml[iLastPriceStart..iLastPriceEnd];
                    double realTimePrice = Double.Parse(lastPriceStr);

                    int iChangePriceStart = priceHtml.IndexOf($"\"change\":\"", iLastPriceEnd);
                    if (iChangePriceStart != -1)
                    {
                        iChangePriceStart += $"\"change\":\"".Length;
                        int iChangePriceEnd = priceHtml.IndexOf("\"", iChangePriceStart);
                        if (iChangePriceEnd != -1)
                        {
                            var changePriceStr = priceHtml[iChangePriceStart..iChangePriceEnd];
                            Utils.Logger.Info($"GetTodayPctChange().changePriceStr: '{changePriceStr}' ");  // TEMP: uncomment when it is fixed: 2021-06-08, System.FormatException: Input string was not in a correct format.
                            double dailyChange = Double.Parse(changePriceStr);

                            double yesterdayClose = (double)realTimePrice - (double)dailyChange;
                            double todayPercentChange = (double)realTimePrice / yesterdayClose - 1;
                            return todayPercentChange;
                        }
                    }
                }
            }
            return Double.NaN;
        }

        void CheckPriorClosePrices()
        {
            // Data sources. Sometimes YF, sometimes GF is not good. We could try to use our Database then, but we don't have ^VIX futures historical price data in it yet.
            DateTime endDateET = DateTime.UtcNow.AddDays(0);    // include today, which is a realtime price, but more accurate estimation
            DateTime startDateET = endDateET.AddDays(-90);   // we need the last 50 items, but ask more trading days. Just to be sure. With this 90 calendar days, we got 62 trading days in July. However, in the Xmas season, we get less. So, keep the 90 calendar days.

            List<string> tickers = new() { "^VIX" };

            IReadOnlyList<Candle?> history = Yahoo.GetHistoricalAsync("^VIX", startDateET, endDateET, Period.Daily).TurnAsyncToSyncTask(); // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
            SqDateOnly[] dates = history.Select(r => new SqDateOnly(r!.DateTime)).ToArray();
            // for penny stocks, IB and YF considers them for max. 4 digits. UWT price (both in IB ask-bid, YF history) 2020-03-19: 0.3160, 2020-03-23: 2302
            float[] adjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4)).ToArray();

            // Check that 1.2*SMA(VIX, 50) < VIX_last_close:  (this is used by the VIX spikes document)
            // this is used in the Balazs's VIX spikes gDoc: https://docs.google.com/document/d/1YA8uBscP1WbxEFIHXDaqR50KIxLw9FBnD7qqCW1z794
            double priorClose = adjCloses[^1];
            int nSmaDays = 50;
            double sma = 0;
            for (int i = 0; i < nSmaDays; i++)
                sma += adjCloses[adjCloses.Length - 1 - i];
            sma /= (double)nSmaDays;

            bool isVixSpike = 1.2 * sma < priorClose;
            // VIX spike can be detected with another formulation if wished
            //Maybe I should use this(or Both) in the VIX checking email service
            //"Marked on the chart are instances where the VIX has risen by at least 30% (from close to"
            //the highest high) in a five-day period when a previous 30 +% advance had not occurred in the prior ten trading days.There have been 70 such occurrences of these spikes in the above-mentioned time period.
            //> But Balazs showed it is not really useful.Still, it can be used. Read that article and email again.
            //See "2017-Forecasting Volatility Tsunamy, Balazs-gmail.pdf"

            if (isVixSpike)  // if  1.2*SMA(VIX, 50) < VIX_last_close, sends an email. So, we can trade VIX MR subjectively.
            {
                string subject = "SqCore: VIX spike detected";
                StringBuilder sb = new(Email.g_htmlEmailStart);
                sb.Append(@"<span class=""sqImportantOK""><strong>VIX Spike</strong> is detected!</span><br/><br/>");
                sb.Append($"Using yesterday close prices for VIX, the condition<br/> <strong>'VIX_priorClose &gt; 1.2 * SMA(VIX, 50)'</strong><br/> ({priorClose:0.##} &gt;  1.2 * {sma:0.##}) was triggered.<br/>");
                sb.Append(@"Our <a href=""https://docs.google.com/document/d/1YA8uBscP1WbxEFIHXDaqR50KIxLw9FBnD7qqCW1z794"">VIX spikes collection gDoc</a> uses the same formula for identifying panic times.<br/>");
                sb.Append("Intraday price was not used for this trigger. You need to act with a delay anyway.<br/><br/>");
                sb.Append("<strong>Action: </strong><br/> This is a Mean Reversion (MR) opportunity.<br/> Trading 'fading the VIX spike' can be considered.<br/>");
                sb.Append("Maybe risking 1/10th of the portfolio.<br/> Doubling down in another chunk maximum 3 times.<br/>");
                sb.Append(Email.g_htmlEmailEnd);

                string emailHtmlBody = sb.ToString();
                new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
                new Email { ToAddresses = Utils.Configuration["Emails:Charm0"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
                new Email { ToAddresses = Utils.Configuration["Emails:Balazs"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
            }
        }

        // Amazon UK price history can be checked in uk.camelcamelcamel.com, for example: http://uk.camelcamelcamel.com/Sennheiser-Professional-blocking-gaming-headset-Black/product/B00JQDOANK
        // private static double? GetAmazonProductPrice(string p_amazonProductUrl)
        // {
        //     string errorMessage = string.Empty;
        //     string? webpage = Utils.DownloadStringWithRetryAsync(p_amazonProductUrl).TurnAsyncToSyncTask();
        //     if (webpage == null)
        //         return null;
        //     Utils.Logger.Info("HttpClient().GetStringAsync returned: " + ((webpage.Length > 100) ? webpage[..100] : webpage));

        //     // <span id="priceblock_ourprice" class="a-size-medium a-color-price">£199.95</span>
        //     string searchStr = @"id=""priceblock_ourprice"" class=""a-size-medium a-color-price"">";
        //     int startInd = webpage.IndexOf(searchStr);
        //     if (startInd == -1)
        //     {   // it is expected (not an exception), that sometimes Amazon changes its website, so we will fail. User will be notified.
        //         Utils.Logger.Info($"searchString '{searchStr}' was not found.");
        //         return null;
        //     }
        //     int endInd = webpage.IndexOf('<', startInd + searchStr.Length);
        //     if (endInd == -1)
        //     {   // it is expected (not an exception), that sometimes Amazon changes its website, so we will fail. User will be notified.
        //         Utils.Logger.Info($"'<' after searchString '{searchStr}' was not found.");
        //         return null;
        //     }
        //     string priceStr = webpage[(startInd + searchStr.Length + 1)..endInd];
        //     if (!Double.TryParse(priceStr, out double price))
        //     {
        //         Utils.Logger.Info($"{priceStr} cannot be parsed to Double.");
        //         return null;
        //     }
        //     return price;
        // }


    }
}