namespace SqCommon;

public enum SqLogLevel
{
    Off, Trace, Debug, Info, Warn, Error, Fatal
}

// This Log is used between SqCore app communications. Webserver, BrowserClient Etc. The C# Server main logger is Nlog, but that cannot be used in webclient communication in JSon and is too heavy.
public class SqLog
{
    public SqLogLevel SqLogLevel { get; set; } = SqLogLevel.Info;
    public string Message { get; set; } = string.Empty;
}