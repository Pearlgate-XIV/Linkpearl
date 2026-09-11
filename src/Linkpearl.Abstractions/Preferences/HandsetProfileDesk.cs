namespace Linkpearl.Preferences;

public sealed class HandsetProfileDesk
{
    private readonly List<IHandsetProfileSink> sinks = new();
    private string lastName = string.Empty;
    private string lastHonorific = string.Empty;
    private bool hasPush;

    public void Add(IHandsetProfileSink sink)
    {
        sinks.Add(sink);
        if (hasPush)
        {
            sink.AcceptHandsetProfile(lastName, lastHonorific);
        }
    }

    public void Push(string name, string honorific)
    {
        lastName = name;
        lastHonorific = honorific;
        hasPush = true;
        for (var index = 0; index < sinks.Count; index++)
        {
            sinks[index].AcceptHandsetProfile(name, honorific);
        }
    }
}
