using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Persistence;

namespace Linkpearl.Applets.Life.Notes;

public sealed class NotesApplet : IApplet
{
    private const int MaxNotes = 12;

    public static readonly AppletManifest Manifest = new()
    {
        Id = "notes",
        DisplayNameKey = "Notes",
        Family = AppletFamily.Life,
        Glyph = "✎",
        HomeOrder = 5,
    };

    private readonly ISettings<NotesScratch> store;
    private readonly List<string> notes = [];

    public NotesApplet(ISettings<NotesScratch> store)
    {
        this.store = store;
        Load();
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
        Persist();
    }

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new LayoutFlow(content, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Notes",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Kept on this phone.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var removeAt = -1;
        var dirty = false;
        for (var index = 0; index < notes.Count; index++)
        {
            var row = stack.Take(frame.Units(44f));
            CardChrome.Draw(frame, row);
            var inset = row.Inset(frame.Units(10f));
            var field = notes.Count > 1 ? inset.Inset(new Edges(0f, 0f, frame.Units(28f), 0f)) : inset;
            var next = frame.TextField.Draw("note-" + index, field, notes[index], "Write something");
            if (!string.Equals(next, notes[index], StringComparison.Ordinal))
            {
                notes[index] = next;
                dirty = true;
            }

            if (notes.Count > 1)
            {
                var clear = inset.RightSlice(frame.Units(24f));
                frame.Text.DrawIn(clear, "×",
                    new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.InkFaint, TextAlign.Center));
                if (frame.Input.ConsumeClick(clear))
                {
                    removeAt = index;
                }
            }
        }

        if (removeAt >= 0)
        {
            notes.RemoveAt(removeAt);
            if (notes.Count == 0)
            {
                notes.Add(string.Empty);
            }

            dirty = true;
        }

        if (notes.Count < MaxNotes)
        {
            var add = stack.Take(frame.Units(40f));
            CardChrome.Draw(frame, add);
            frame.Text.DrawIn(add, "+ New note",
                new TextStyle(FontRole.Body, frame.Theme.Palette.Accent, TextAlign.Center));
            if (frame.Input.ConsumeClick(add))
            {
                notes.Add(string.Empty);
                dirty = true;
            }
        }

        if (dirty)
        {
            Persist();
        }
    }

    private void Load()
    {
        notes.Clear();
        var lines = store.Value.Lines;
        if (lines is { Length: > 0 })
        {
            for (var index = 0; index < lines.Length && notes.Count < MaxNotes; index++)
            {
                notes.Add(lines[index] ?? string.Empty);
            }
        }

        if (notes.Count == 0)
        {
            notes.Add(string.Empty);
        }
    }

    private void Persist()
    {
        var lines = notes.ToArray();
        store.Mutate(section => section.Lines = lines);
    }
}
