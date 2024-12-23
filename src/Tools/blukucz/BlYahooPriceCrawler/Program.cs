using System;
using CsvHelper.Configuration.Attributes;
using SqCommon;

namespace BlYahooPriceCrawler
{

    class Program
    {
        static void Main(string[] _)
        {
            string userInput;
            do
            {
                userInput = DisplayMenuAndExecute();
            } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");
        }

        static string DisplayMenuAndExecute()
        {

            ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
            Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
            Console.WriteLine("2. Download YF price data into CSVs.");
            Console.WriteLine("3. SA Top Analysts: create performance file.");
            Console.WriteLine("4. SA Quant Scores: create aggregated csv file.");
            Console.WriteLine("5. Steve Cress's recommendations QRs");
            Console.WriteLine("6. Exit gracefully (Avoid Ctrl-^C).");
            string userInput;
            try
            {
                userInput = Console.ReadLine() ?? string.Empty;
            }
            catch (System.IO.IOException) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
            {
                return "ConsoleIsForcedToShutDown";
            }
            switch (userInput)
            {
                case "1":
                    Console.WriteLine("Hello. I am not crashed yet! :)");
                    Utils.Logger.Info("Hello. I am not crashed yet! :)");
                    break;
                case "2":
                    string tickerFileName = "D:/Temp/YFHist/Tickers.csv";
                    DateTime expectedHistoryStartDateET = new(2020, 1, 1);
                    Controller.DownloadYFtoCsv(tickerFileName, expectedHistoryStartDateET, "D:/Temp/YFHist/");
                    Console.WriteLine("Writing YF adjusted priced into CVSs is done.");
                    break;
                case "3":
                    Controller.RecommendationPerformanceAnalyser();
                    Console.WriteLine("SA TopAnalysts Recommendation results are written into CSV.");
                    break;
                case "4":
                    Controller.SAQuantRatingScoreAggregator();
                    Console.WriteLine("SA Quant Score aggregated results are written into CSV.");
                    break;
                case "5":
                    Controller.SteveCressRecommendationQRs();
                    Console.WriteLine("Steve Cress's recommendations QRs are written into CSV.");
                    break;
                case "6":
                    return "UserChosenExit";
            }
            return string.Empty;
        }
    }
}
