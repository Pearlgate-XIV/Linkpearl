namespace Linkpearl.Diagnostics;

public enum LogSeverity : byte
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
}

public interface ILinkpearlLog
{
    void Write(LogSeverity severity, string message);

    void Write(LogSeverity severity, Exception failure, string message);

    ILinkpearlLog Scoped(string channel);
}
