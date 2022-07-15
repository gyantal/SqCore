using System;

namespace SqCommon;

public class SqConsole // Console helper functions
{
    public static void WriteLine(string? value)
    {
        Console.WriteLine($"~{DateTime.UtcNow:dd'T'HH':'mm':'ss}: {value}");
    }
}