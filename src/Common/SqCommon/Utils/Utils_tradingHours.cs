using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SqCommon
{
    public enum TradingHours { PreMarketTrading, RegularTrading, PostMarketTrading, Closed }


    // many times we want to distinguish time from 0:00ET to 4:00ET, so PriorCloses are updated with Friday closesprices if we are in PrePreMarket, not only later at real PreMarket.
    // TODO: replace TradingHours to TradingHoursEx in code
    public enum TradingHoursEx { PrePreMarketTrading, PreMarketTrading, RegularTrading, PostMarketTrading, Closed }

    public static partial class Utils
    {
        static List<Tuple<DateTime, DateTime?>>? g_holidays = null;
        static DateTime g_holidaysDownloadDate = DateTime.MinValue;    // the last time we downloaded info from the internet

        public static TradingHours UsaTradingHoursNow_withoutHolidays()
        {
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            return UsaTradingHours_withoutHolidays(etNow);
        }

        // PreMarket: 4:00ET, Regular: 9:30ET, Post:16:00, Post-ends: 20:00
        public static TradingHours UsaTradingHours_withoutHolidays(DateTime p_timeET)
        {
            // we should use Holiday day data from Nasdaq website later. See code in SqLab.
            if (p_timeET.IsWeekend())
                return TradingHours.Closed;

            int nowTimeOnlySec = p_timeET.Hour * 60 * 60 + p_timeET.Minute * 60 + p_timeET.Second;
            if (nowTimeOnlySec < 4 * 60 * 60)
                return TradingHours.Closed;
            else if (nowTimeOnlySec < 9 * 60 * 60 + 30 * 60)
                return TradingHours.PreMarketTrading;
            else if (nowTimeOnlySec < 16 * 60 * 60)
                return TradingHours.RegularTrading;
            else if (nowTimeOnlySec < 20 * 60 * 60)
                return TradingHours.PostMarketTrading;
            else
                return TradingHours.Closed;
        }

        public static TradingHoursEx UsaTradingHoursExNow_withoutHolidays()
        {
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            return UsaTradingHoursEx_withoutHolidays(etNow);
        }

        // PreMarket: 4:00ET, Regular: 9:30ET, Post:16:00, Post-ends: 20:00
        public static TradingHoursEx UsaTradingHoursEx_withoutHolidays(DateTime p_timeET)
        {
            // we should use Holiday day data from Nasdaq website later. See code in SqLab.
            if (p_timeET.IsWeekend())
                return TradingHoursEx.Closed;

            int nowTimeOnlySec = p_timeET.Hour * 60 * 60 + p_timeET.Minute * 60 + p_timeET.Second;
            if (nowTimeOnlySec < 4 * 60 * 60)
                return TradingHoursEx.PrePreMarketTrading;
            else if (nowTimeOnlySec < 9 * 60 * 60 + 30 * 60)
                return TradingHoursEx.PreMarketTrading;
            else if (nowTimeOnlySec < 16 * 60 * 60)
                return TradingHoursEx.RegularTrading;
            else if (nowTimeOnlySec < 20 * 60 * 60)
                return TradingHoursEx.PostMarketTrading;
            else
                return TradingHoursEx.Closed;
        }

        public static bool IsInRegularUsaTradingHoursNow()
        {
            return IsInRegularUsaTradingHoursNow(TimeSpan.FromDays(3));
        }

        public static bool IsInRegularUsaTradingHoursNow(TimeSpan p_maxAllowedStaleness)
        {
            DateTime utcNow = DateTime.UtcNow;
            
            // 1. quick response for trivial case that works most of the time, that don't need DetermineUsaMarketTradingHours()
            DateTime utcNowET = Utils.ConvertTimeFromUtcToEt(utcNow).Date;
            if (utcNowET.DayOfWeek == DayOfWeek.Saturday || utcNowET.DayOfWeek == DayOfWeek.Sunday)
                return false;
            DateTime openInET = new DateTime(utcNowET.Year, utcNowET.Month, utcNowET.Day, 9, 30, 0);
            if (utcNowET < openInET)
                return false;
            DateTime maxPossibleCloseInET = new DateTime(utcNowET.Year, utcNowET.Month, utcNowET.Day, 16, 0, 0); // usually it is 16:00, but when half-day trading, then it is 13:00
            if (utcNowET > openInET)
                return false;

            // 2. During RTH on weekdays, we have to be more thorough.
            bool isMarketTradingDay;
            DateTime openTimeUtc, closeTimeUtc;
            bool isTradingHoursOK = Utils.DetermineUsaMarketTradingHours(utcNow, out isMarketTradingDay, out openTimeUtc, out closeTimeUtc, p_maxAllowedStaleness);
            if (!isTradingHoursOK)
            {
                Utils.Logger.Error("DetermineUsaMarketTradingHours() was not OK.");
                return false;
            }
            else
            {
                if (!isMarketTradingDay)
                    return false;
                if (utcNow < openTimeUtc)
                    return false;
                if (utcNow > closeTimeUtc)
                    return false;

                return true;
            }
        }



        // the advantage of using https://www.nyse.com/markets/hours-calendars is that it not only gives back Early Closes, but the Holiday days too
        public static List<Tuple<DateTime, DateTime?>>? GetHolidaysAndHalfHolidaysWithCloseTimesInET(TimeSpan p_maxAllowedStaleness)
        {
            if ((g_holidays != null) && (DateTime.UtcNow - g_holidaysDownloadDate) < p_maxAllowedStaleness)
                return g_holidays;

            // using http://www.thestreet.com/stock-market-news/11771386/market-holidays-2015.html is not recommended, 
            //because for 20x pages it does an Adver redirection instead of giving back the proper info the returned page 
            // is an advert. So, stick to the official NYSE website.
            string? webPage = Utils.DownloadStringWithRetryAsync("https://www.nyse.com/markets/hours-calendars", 5, TimeSpan.FromSeconds(2), false).TurnAsyncToSyncTask();
            if (webPage == null)
            {
                if ((g_holidays != null))
                {
                    if ((DateTime.UtcNow - g_holidaysDownloadDate) < TimeSpan.FromDays(8))
                        return g_holidays;  // silently use the old data
                    else
                    {
                        // the g_holidays data is considered to be too old, but use it
                        Utils.Logger.Error(@"Failed 5x to townload ""https://www.nyse.com/markets/hours-calendars"". We use older than 8 days g_holidays data. Which is OK, but take note of this. Check that that website works or not.");
                        return g_holidays;
                    }
                }
                else
                {
                    Utils.Logger.Error(@"Failed 5x to townload ""https://www.nyse.com/markets/hours-calendars"". And there is no old g_holidays data to use. Null is returned.");
                    return null;
                }
            }

            var holidays1 = new List<Tuple<DateTime, DateTime?>>();
            var holidays2 = new List<Tuple<DateTime, DateTime?>>();

            string? errorMsg = null;
            try
            {
                // 1. Get section from <thead> to </tbody> for the holidays
                // 2. Get section from ">*", ">**", ">***" to </p> to get the 3 footnotes
                // 2018: holiday table appears twice in the HTML. One in the middle, but one at the end is shorter, cleaner, get that. 
                // 2019: holiday table appears only once in the HTML.
                int iTHead = webPage.IndexOf(@"<table class=""table table-layout-fixed"">");
                int iTBody = webPage.IndexOf(@"</table>", iTHead);
                string holidayTable = webPage.Substring(iTHead, iTBody - iTHead);

                
                int iFootnoteStart = webPage.IndexOf(">* Each", iTBody, StringComparison.CurrentCultureIgnoreCase);   // 2017-02-08: a ">*Each" got a space as ">* Each"
                int iFootnoteEnd = webPage.IndexOf(@"</div>", iFootnoteStart);// in 2017: Footnote section is was Before the second-holiday-table in the html source, in 2018: no
                string footnote = webPage.Substring(iFootnoteStart, iFootnoteEnd - iFootnoteStart);

                int year1 = -1, year2 = -1;
                var trs = holidayTable.Split(new string[] { "<tr>\n  ", "<tr>", "</tr>\n  ", "</tr>" }, StringSplitOptions.RemoveEmptyEntries);
                var headerRow = trs[1];
                var tdsHeader = headerRow.Split(new string[] { @"<td>", @"</td>" }, StringSplitOptions.RemoveEmptyEntries);
                year1 = Int32.Parse(tdsHeader[3]);
                year2 = Int32.Parse(tdsHeader[5]);
                //year3 = Int32.Parse(tdsHeader[7]);  // there is year3 too, but we don't need it in VBroker or healthmonitor. So, just ignore them

                for (int i = 2; i < trs.Length; i++)
                {
                    if (!trs[i].TrimStart().StartsWith(@"<th>"))
                        continue;

                    var tds = trs[i].Split(new string[] { @"<th>", @"</th>", @"<td>", @"</td>" }, StringSplitOptions.RemoveEmptyEntries);
                    //string holidayName = tds[1];
                    ProcessHolidayCellInET(tds[3].Trim(), year1, footnote, holidays1);
                    ProcessHolidayCellInET(tds[5].Trim(), year2, footnote, holidays2);
                    //ProcessHolidayCellInET(tds[5], year2, footnote, holidays2);   // there is year3 too, but we don't need it in VBroker or healthmonitor. So, just ignore them
                }

            }
            catch (Exception ex)
            {
                errorMsg = "This error is expected once every year. Exception in DetermineUsaMarketOpenOrCloseTimeNYSE() in String operations. Probably the structure of the page changed, re-code is needed every year when a new year appears in the Nasdaq Trading Calendar webpage (in 2019: 09-24). Debug it in VS, recode and redeploy 3 apps: HealthMonitor & VBroker & VBrokerManual. Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.  Message:" + ex.Message;                
            }

            if (holidays1.Count == 0)
                errorMsg = "This error is expected once every year. Exception in DetermineUsaMarketOpenOrCloseTimeNYSE() in String operations. Probably the structure of the page changed, re-code is needed every year when a new year appears in the Nasdaq Trading Calendar webpage (in 2019: 09-24). Debug it in VS, recode and redeploy 3 apps: HealthMonitor & VBroker & VBrokerManual. Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.";
            if (errorMsg != null) {
                //  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.
                Utils.Logger.Error(errorMsg);
                throw new Exception(errorMsg);   // don't swallow this error in SqCommon.Utils, because VBroker.exe main app should know about it. This is a serious error. The caller should handle it.
            }

            g_holidays = holidays1;
            g_holidays.AddRange(holidays2); // the holidays list is not ordered by date, because sometimes this halfDay comes before/after the holiday day
            g_holidaysDownloadDate = DateTime.UtcNow;
            return g_holidays;
        }

        private static void ProcessHolidayCellInET(string p_td, int p_year, string p_footnote, List<Tuple<DateTime, DateTime?>> p_holidays)
        {
            // "<td>July 4 (Observed July 3)</td>"  , 4th is Saturday, so the market is closed on the "observed" date
            // <td>November 26**</td>
            //< td > December 25(Observed December 26) ***</ td >  this has both Observed and a half-holiday too

            // at first 
            string cellTrimmedLwr = p_td.Trim().ToLower();
            if (p_td.IndexOf('*') != -1)    // read the footnotes; there will be a half-holiday on the next or the previous day
            {
                // "**Each market will close early at 1:00 p.m. on Friday, November 27, 2015 and Friday, November 25, 2016 (the day after Thanksgiving)"
                // "***Each market will close early at 1:00 p.m. on Thursday, December 24, 2015. "
                int nAsterisk = p_td.Count(r => r == '*');
                int indExplanation = -1;
                for (int i = 0; i < p_footnote.Length; i++)
                {
                    if (p_footnote[i] == '>')
                    {
                        for (int j = 0; j < nAsterisk; j++)
                        {
                            if (p_footnote[i + 1 + j] != '*')
                                break;
                            if (j == nAsterisk - 1)
                                indExplanation = i + 1 + j + 1;
                        }
                    }
                    if (indExplanation != -1)
                        break;
                }

                int indExplanationEnd = p_footnote.IndexOf("</p>", indExplanation);    // go to the end of the sentence only.
                string explanation = p_footnote.Substring(indExplanation, indExplanationEnd - indExplanation);

                int indTimeET = explanation.IndexOf("Each market will close early at ");
                int indTimeET1 = indTimeET + "Each market will close early at ".Length;
                int indTimeET2 = explanation.IndexOf(':', indTimeET1);
                int indTimeET3 = explanation.IndexOf("p.m.", indTimeET2);
                string earlyCloseHourStr = explanation.Substring(indTimeET1, indTimeET2 - indTimeET1);
                string earlyCloseMinStr = explanation.Substring(indTimeET2 + 1, indTimeET3 - indTimeET2 - 1);
                int earlyCloseHour = Int32.Parse(earlyCloseHourStr) + 12; //"1 p.m." means you have to add 12 hours to the recognized digit
                int earlyCloseMin = Int32.Parse(earlyCloseMinStr);

                // try to find the Year in the text, then wark backwards for 2 commas
                int indYear = explanation.IndexOf(p_year.ToString(), indTimeET3);
                int indFirstComma = explanation.LastIndexOf(',', indYear);
                int indSecondComma = explanation.LastIndexOf(',', indFirstComma - 1);
                string dateStr = explanation.Substring(indSecondComma + 1, (indFirstComma - 1) - indSecondComma);
                DateTime halfDay = DateTime.Parse(dateStr + ", " + p_year.ToString());
                // the holidays list is not ordered by date, because sometimes this halfDay comes before/after the holiday day
                p_holidays.Add(new Tuple<DateTime, DateTime?>(halfDay, new DateTime(halfDay.Year, halfDay.Month, halfDay.Day, earlyCloseHour, earlyCloseMin, 0)));

                p_td = p_td.Replace('*', ' ');  //remove ** if it is in the string, because Date.Parse() will fail on that
            }

            // p_td can be "Friday, July 4 (Observed July 3)" or "Friday, July 3 (July 4 holiday observed)" or "Friday, July 3"
            DateTime dateHoliday = DateTime.MinValue;
            int indObserved = p_td.IndexOf("(Observed");
            if (indObserved != -1)
            {
                int observedDateStartInd = indObserved + "(Observed".Length;
                int indObservedEnd = p_td.IndexOf(')', observedDateStartInd);
                dateHoliday = DateTime.Parse(p_td.Substring(observedDateStartInd, indObservedEnd - observedDateStartInd) + ", " + p_year.ToString());
            }
            else
            {
                indObserved = p_td.IndexOf("observed)");
                if (indObserved != -1)
                {
                    int indObservedStart = p_td.LastIndexOf('(', indObserved - 1, indObserved);
                    dateHoliday = DateTime.Parse(p_td.Substring(0, indObservedStart) + ", " + p_year.ToString());
                } else {
                    
                    if (cellTrimmedLwr == "&#8212;" || cellTrimmedLwr == "—") // &#8212; = "—". This means that holiday is a weekend, therefore no need to store. In some cases, this missing "NewYearsEve" it can be deducted, in other cases, Independence Day, it can be any day, so better to not invent a non-existant holiday which is at the weekend and put it into DB.
                    {
                        // do nothing.
                    }
                    else
                        dateHoliday = DateTime.Parse(p_td + ", " + p_year.ToString());
                }
            }
            if (dateHoliday != DateTime.MinValue)
                p_holidays.Add(new Tuple<DateTime, DateTime?>(dateHoliday, null));

        }

        // it is important that p_timeUtc can be a Time and it is in UTC. Convert it to ET to work with it.
        public static bool DetermineUsaMarketTradingHours(DateTime p_timeUtc, out bool p_isMarketTradingDay, out DateTime p_openTimeUtc, out DateTime p_closeTimeUtc, TimeSpan p_maxAllowedStaleness)
        {
            p_openTimeUtc = p_closeTimeUtc = DateTime.MinValue;
            p_isMarketTradingDay = false;

            TimeZoneInfo utcZone = TimeZoneInfo.Utc;
            TimeZoneInfo? estZone = null;
            try
            {
                estZone = Utils.FindSystemTimeZoneById(TimeZoneId.EST);
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "Exception because of TimeZone conversion ");
                return false;
            }

            DateTime timeET = TimeZoneInfo.ConvertTime(p_timeUtc, utcZone, estZone);

            if (timeET.DayOfWeek == DayOfWeek.Saturday || timeET.DayOfWeek == DayOfWeek.Sunday)
            {
                p_isMarketTradingDay = false;
                return true;
            }

            List<Tuple<DateTime, DateTime?>>? holidaysAndHalfHolidays = GetHolidaysAndHalfHolidaysWithCloseTimesInET(p_maxAllowedStaleness);
            if (holidaysAndHalfHolidays == null || holidaysAndHalfHolidays.Count == 0)
            {
                Logger.Error("holidaysAndHalfHolidays are not recognized");
                return false; // temporarily off
            }

            DateTime openInET = new DateTime(timeET.Year, timeET.Month, timeET.Day, 9, 30, 0);
            DateTime closeInET;
            var todayHoliday = holidaysAndHalfHolidays.FirstOrDefault(r => r.Item1 == timeET.Date);
            if (todayHoliday == null)   // it is a normal day, not holiday: "The NYSE and NYSE MKT are open from Monday through Friday 9:30 a.m. to 4:00 p.m. ET."
            {
                p_isMarketTradingDay = true;
                closeInET = new DateTime(timeET.Year, timeET.Month, timeET.Day, 16, 0, 0);
            }
            else
            { // if it is a holiday or a half-holiday (that there is trading, but early close)
                p_isMarketTradingDay = (todayHoliday.Item2 != null);   // Item2 is the CloseTime (that is for half-holidays)
                if (todayHoliday.Item2 == null)
                {
                    p_isMarketTradingDay = false;
                    return true;
                }
                else
                {
                    p_isMarketTradingDay = true;
                    closeInET = (DateTime)todayHoliday.Item2;  // yes, halfHolidays are in ET 
                }
            }

            if (!p_isMarketTradingDay)
                return true;
            
            p_openTimeUtc = TimeZoneInfo.ConvertTime(openInET, estZone, utcZone);
            p_closeTimeUtc = TimeZoneInfo.ConvertTime(closeInET, estZone, utcZone);
            return true;
        }

    }

}