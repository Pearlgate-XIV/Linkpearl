namespace Linkpearl.Preferences;

public sealed class HandsetProfileDesk
{
    private readonly List<IHandsetProfileSink> sinks = new();

    public void Add(IHandsetProfileSink sink) => sinks.Add(sink);

    public void Push(string name, string honorific)
    {
        for (var index = 0; index < sinks.Count; index++)
        {
            sinks[index].AcceptHandsetProfile(name, honorific);
        }
    }
}
