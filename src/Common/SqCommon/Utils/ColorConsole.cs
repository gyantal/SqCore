using System;

namespace SqCommon
{
    // ANSI escape code colour codes (which contain BOLD style too) in .NET Core Console applications: doesn't work on Windows, because of Windows. see more here
    // https://www.jerriepelser.com/blog/using-ansi-color-codes-in-net-console-apps/
    // http://www.lihaoyi.com/post/BuildyourownCommandLinewithANSIescapecodes.html
    //
    // Note: this Linux specific functionality is not necessary. But it is decided that we leave it here, because this Linux specific
    // Bright and Bold ("1" means bold, (char)27 + "[1;35m";) colours looks much much prettier
    // than the Dotnetcore official non-bold and darkish looking Console.Magenta.
    //
    // In the future, when DotNetCore supports Console.Bold colours too (in theory Windows console has this feature too), this code can be eliminated.
    // VT100 codes, http://www.cplusplus.com/forum/unices/36461/
    // this Linux handling should be temporary only until it is fixed in DotNetCore in Linux
    // this works too in the Terminal. Type this "printf '\e[38;5;196m Foreground color: red\n'" or printf '\e[1;35m Foreground color: Magenta'"
    // \033 is the C-style octal code for an escape character. it is 3*8+3=27
    // this works in a C++ program: printf("\033[1;35m  Hello, world!\n");   (even on the VirtualBroker server)
    public class ColorConsole
    {
        private static readonly NLog.Logger g_logger = NLog.LogManager.GetCurrentClassLogger(); // the name of the logger will be the "Namespace.Class"
        public static string GetLinuxVT100ForeColorCodes(ConsoleColor p_color)
        {
            switch (p_color)
            {
                case ConsoleColor.Black:
                    return (char)27 + "[1;30m";
                case ConsoleColor.White:
                    return (char)27 + "[1;37m";     // Gray

                case ConsoleColor.DarkBlue:
                    return (char)27 + "[2;34m";     // "2" means darker but not bold
                case ConsoleColor.DarkGreen:
                    return (char)27 + "[2;32m";
                case ConsoleColor.DarkCyan:
                    return (char)27 + "[2;36m";
                case ConsoleColor.DarkRed:
                    return (char)27 + "[2;31m";
                case ConsoleColor.DarkMagenta:
                    return (char)27 + "[2;35m";
                case ConsoleColor.DarkYellow:
                    return (char)27 + "[2;33m";
                case ConsoleColor.DarkGray:
                    return (char)27 + "[2;37m";

                case ConsoleColor.Blue:
                    return (char)27 + "[1;34m";     // "1" means bold
                case ConsoleColor.Green:
                    return (char)27 + "[1;32m";
                case ConsoleColor.Cyan:
                    return (char)27 + "[1;36m";
                case ConsoleColor.Red:
                    return (char)27 + "[1;31m";
                case ConsoleColor.Magenta:
                    return (char)27 + "[1;35m";
                case ConsoleColor.Yellow:
                    return (char)27 + "[1;33m";     // somebody said this is brown, because there is no Yellow  in Linux VT100. But we tested. It is not brown on Linux. It is Yellow. Correct.
                case ConsoleColor.Gray:
                    return (char)27 + "[1;37m";
                default:
                    string linuxDefaultConsoleColor = (char)27 + "[0m";  // VT100 codes, http://www.cplusplus.com/forum/unices/36461/
                    return linuxDefaultConsoleColor;
            }
        }

        public static Tuple<ConsoleColor?, ConsoleColor?> ConsoleColorBegin(ConsoleColor? p_foregroundColor, ConsoleColor? p_backgroundColor)
        {
            ConsoleColor? previousForeColor = null;
            ConsoleColor? previousBackColor = null;
            if (p_foregroundColor != null)
            {
                previousForeColor = Console.ForegroundColor;

                if (Utils.RunningPlatform() == Platform.Linux)
                {
                    Console.Write(GetLinuxVT100ForeColorCodes((ConsoleColor)p_foregroundColor));
                }
                else
                {
                    Console.ForegroundColor = (ConsoleColor)p_foregroundColor;
                }
            }

            if (p_backgroundColor != null)
            {
                if (Utils.RunningPlatform() == Platform.Linux)
                {
                    g_logger.Trace("Linux background colour is not yet implemented. The whole Linux implementation is temporary anyway, until DotNetCore is fixed on Linux.");
                }
                previousBackColor = Console.BackgroundColor;
                Console.BackgroundColor = (ConsoleColor)p_backgroundColor;
            }
            return new Tuple<ConsoleColor?, ConsoleColor?>(previousForeColor, previousBackColor);
        }

        public static void ConsoleColorRestore(Tuple<ConsoleColor?, ConsoleColor?> p_previousColors)
        {
            // Console.ResetColor(); is one option, but it is not that good than going back to previous
            if (p_previousColors.Item1 != null)
            {
                if (Utils.RunningPlatform() == Platform.Linux)
                {
                    Console.Write(GetLinuxVT100ForeColorCodes((ConsoleColor)p_previousColors.Item1));
                }
                else
                {
                    Console.ForegroundColor = (ConsoleColor)p_previousColors.Item1;
                }
            }
            if (p_previousColors.Item2 != null)
                Console.BackgroundColor = (ConsoleColor)p_previousColors.Item2;
        }

        // on Windows. Blue is too dark. DarkBlue is hardly visible. http://i.stack.imgur.com/Qmbj8.png  // Try to use
        // Magenta for menu
        // Cyan for VBroker strategy Start/End
        // Red for Warnings (bad)
        // Green for general important things (Yellow can be good too, because later we realized in spite of the rumour that it is Brown in VT100, it is correctly Yellow in DotNetCore ubuntu)
        public static void WriteLine(ConsoleColor? p_foreColor, string p_value) // static objects like Console cannot have Extensions methods with the 'this' keyword.
        {
            Write(p_foreColor, null, false, true, p_value);
        }

        public static void WriteLine(ConsoleColor? p_foreColor, bool p_writeTimeStamp, string p_value) // static objects like Console cannot have Extensions methods with the 'this' keyword.
        {
            Write(p_foreColor, null, p_writeTimeStamp, true, p_value);
        }

        public static void WriteLine(ConsoleColor? p_foreColor, ConsoleColor? p_backColor, string p_value) // static objects like Console cannot have Extensions methods with the 'this' keyword.
        {
            Write(p_foreColor, p_backColor, false, true, p_value);
        }

        public static void Write(ConsoleColor? p_foreColor, string p_value) // static objects like Console cannot have Extensions methods with the 'this' keyword.
        {
            Write(p_foreColor, null, false, false, p_value);
        }

        public static void Write(ConsoleColor? p_foreColor, ConsoleColor? p_backColor, bool p_writeTimeStamp, bool p_useWriteLine, string p_value) // static objects like Console cannot have Extensions methods with the 'this' keyword.
        {
            if (p_writeTimeStamp)
                Console.Write(DateTime.UtcNow.ToString("MMdd'T'HH':'mm':'ss.fff': '")); // timestamp uses the original colour

            var colors = ConsoleColorBegin(p_foreColor, p_backColor);
            if (p_useWriteLine)
                Console.WriteLine(p_value);
            else
                Console.Write(p_value);
            ConsoleColorRestore(colors);
        }
    }
}