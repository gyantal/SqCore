using System;
using CsvHelper.Configuration.Attributes;
using SqCommon;

namespace YahooCrawler
{

    class Program
    {
        static void Main(string[] args)
        {
            string userInput = string.Empty;
            do
            {
                userInput = DisplayMenuAndExecute();
            } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");
        }

        static string DisplayMenuAndExecute()
        {

            ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
            Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
            Console.WriteLine("2. Download YF data into CSVs.");
            Console.WriteLine("3. Read recommendations from CSV.");
            Console.WriteLine("4. Transform recommendations.");
            Console.WriteLine("5. Exit gracefully (Avoid Ctrl-^C).");
            string userInput = string.Empty;
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
                    Controller.g_controller.DownloadYFtoCsv();
                    Console.WriteLine("Writing YF adjusted priced into CVSs is done.");
                    break;
                case "3":
                    Controller.g_controller.ReadRecommendationsCsv();
                    Console.WriteLine("Recommendations CSV is read into the memory.");
                    break;
                case "4":
                    Controller.g_controller.TransformRecommendationsCsv();
                    Console.WriteLine("Recommendations CSV is read into the memory.");
                    break;
                case "5":
                    return "UserChosenExit";
            }
            return string.Empty;
        }
    }
}
