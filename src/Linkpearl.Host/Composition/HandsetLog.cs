using Dalamud.Plugin.Services;
using Linkpearl.Diagnostics;

namespace Linkpearl.Host.Composition;

public sealed class HandsetLog : ILinkpearlLog
{
    private readonly IPluginLog log;
    private readonly string channel;

    public HandsetLog(IPluginLog log, string channel = "")
    {
        this.log = log;
        this.channel = channel;
    }

    public void Write(LogSeverity severity, string message) => Emit(severity, Prefix() + message);

    public void Write(LogSeverity severity, Exception failure, string message) =>
        EmitException(severity, Prefix() + message, failure);

    public ILinkpearlLog Scoped(string channel) => new HandsetLog(log, channel);

    private string Prefix() => channel.Length > 0 ? $"[{channel}] " : string.Empty;

    private void Emit(LogSeverity severity, string message)
    {
        switch (severity)
        {
            case LogSeverity.Debug:
                log.Debug(message);
                break;
            case LogSeverity.Warning:
                log.Warning(message);
                break;
            case LogSeverity.Error:
                log.Error(message);
                break;
            default:
                log.Information(message);
                break;
        }
    }

    private void EmitException(LogSeverity severity, string message, Exception failure)
    {
        if (severity == LogSeverity.Warning)
        {
            log.Warning(failure, message);
            return;
        }

        log.Error(failure, message);
    }
}
