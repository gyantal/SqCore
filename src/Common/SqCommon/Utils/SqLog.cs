namespace SqCommon;

public enum SqLogLevel
{
    Off, Trace, Debug, Info, Warn, Error, Fatal
}

public class SqLog
{
    public SqLogLevel SqLogLevel { get; set; } = SqLogLevel.Info;
    public string Message { get; set; } = string.Empty;
}