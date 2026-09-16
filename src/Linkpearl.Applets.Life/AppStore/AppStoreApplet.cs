using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.AppStore;

public sealed class AppStoreApplet : IApplet
{
    private static readonly string[] Categories = { "All", "Essentials", "Life", "Tools" };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "appstore",
        DisplayNameKey = "App Store",
        Family = AppletFamily.Life,
        Glyph = "▣",
        HomeOrder = 24,
    };

    private readonly DisplayPreferences display;
    private readonly IGameSession game;
    private readonly List<AppSpec> visible = new();
    private int category;
    private string query = string.Empty;
    private float scroll;

    public AppStoreApplet(DisplayPreferences display, IGameSession game)
    {
        this.display = display;
        this.game = game;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => "store";

    public void Enter(AppletEntry entry)
    {
        query = string.Empty;
        category = 0;
        scroll = 0f;
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var inner = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(8f), frame.Units(14f),
            frame.Units(6f)));
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(8f));
        DrawHeader(frame, stack.Take(frame.Units(40f)));
        DrawSearch(frame, stack.Take(frame.Units(34f)));
        DrawCategories(frame, stack.Take(frame.Units(28f)));
        DrawShelf(frame, stack.TakeRemaining());
    }

    private static void DrawHeader(in AppletFrame frame, Rect row)
    {
        frame.Text.DrawIn(row.TopSlice(frame.Units(22f)), "Store",
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(row.BottomSlice(frame.Units(16f)), "Search, pick a category, add or remove.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawSearch(in AppletFrame frame, Rect field)
    {
        frame.Paint.Fill(field, frame.Theme.Palette.SurfaceRaised with { W = 0.55f }, field.Height * 0.42f);
        var type = field.Inset(new Edges(frame.Units(28f), frame.Units(4f), frame.Units(10f), frame.Units(4f)));
        var next = frame.TextField.Draw("appstore-search", type, query, "Search apps");
        if (!string.Equals(next, query, StringComparison.Ordinal))
        {
            query = next;
            scroll = 0f;
        }

        SearchMark.Draw(frame.Paint, field.LeftSlice(frame.Units(28f)).Inset(frame.Units(6f)),
            frame.Theme.Palette.InkMuted);
    }

    private void DrawCategories(in AppletFrame frame, Rect row)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var gap = frame.Units(6f);
        var width = (row.Width - gap * (Categories.Length - 1)) / Categories.Length;
        for (var index = 0; index < Categories.Length; index++)
        {
            var chip = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            var on = category == index;
            frame.Paint.Fill(chip, on ? gold with { W = 0.92f } : frame.Theme.Palette.SurfaceOverlay with { W = 0.34f },
                chip.Height * 0.45f);
            frame.Text.DrawIn(chip, Categories[index],
                new TextStyle(FontRole.CaptionStrong, on ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.InkMuted,
                    TextAlign.Center));
            if (frame.Input.ConsumeClick(chip))
            {
                category = index;
                scroll = 0f;
            }
        }
    }

    private void DrawShelf(in AppletFrame frame, Rect body)
    {
        FillVisible();
        var rowH = frame.Units(64f);
        var gap = frame.Units(8f);
        var cursor = new LayoutFlow(body.Translate(new Vector2(0f, -scroll)), StackAxis.Vertical, gap);
        frame.Paint.PushClip(body);
        for (var index = 0; index < visible.Count; index++)
        {
            DrawListing(frame, cursor.Take(rowH), visible[index]);
        }

        if (visible.Count == 0)
        {
            frame.Text.DrawWrapped(body.Inset(frame.Units(16f)), "No apps match that search.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        var used = visible.Count * (rowH + gap);
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, body, ref scroll, used);
    }

    private void DrawListing(in AppletFrame frame, Rect row, AppSpec spec)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.38f }, frame.Units(12f));
        var icon = row.LeftSlice(frame.Units(56f)).Inset(frame.Units(8f));
        AppMarks.DrawFace(frame, icon, spec.Id, frame.Input.IsHovering(icon));
        var action = row.RightSlice(frame.Units(78f)).Inset(new Edges(0f, frame.Units(16f), frame.Units(10f),
            frame.Units(16f)));
        var text = row.Inset(new Edges(frame.Units(56f), frame.Units(12f), frame.Units(86f), frame.Units(10f)));
        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(20f)), PhoneLanguages.App(spec.Id, spec.Name),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(16f)), spec.Caption,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var owned = display.IsOwned(spec.Id);
        var verb = owned ? "Remove" : "Add";
        if (owned)
        {
            frame.Paint.Stroke(action, frame.Theme.Palette.InkMuted with { W = 0.7f }, frame.Theme.Metrics.Hairline,
                action.Height * 0.45f);
            frame.Text.DrawIn(action, verb,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(action, gold with { W = 0.92f }, action.Height * 0.45f);
            frame.Text.DrawIn(action, verb,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        }

        if (!frame.Input.ConsumeClick(action) && !frame.Input.ConsumeClick(row))
        {
            return;
        }

        if (owned)
        {
            display.UninstallApp(spec.Id);
            return;
        }

        display.InstallApp(spec.Id);
    }

    private void FillVisible()
    {
        visible.Clear();
        var needle = query.Trim();
        for (var index = 0; index < AppShelf.Catalog.Length; index++)
        {
            var spec = AppShelf.Catalog[index];
            if (spec.Hidden || string.Equals(spec.Id, "appstore", StringComparison.Ordinal) ||
                (string.Equals(spec.Id, "vybe", StringComparison.Ordinal) && BlocksVybe()) ||
                !Matches(spec, needle))
            {
                continue;
            }

            visible.Add(spec);
        }

        visible.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    private bool Matches(AppSpec spec, string needle)
    {
        if (category > 0 && spec.Group != (AppGroup)(category - 1))
        {
            return false;
        }

        return needle.Length == 0 ||
               spec.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               spec.Caption.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               AppShelf.GroupLabels[(int)spec.Group].Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private bool BlocksVybe()
    {
        var name = (game.RaceName ?? string.Empty) + " " + (game.TribeName ?? string.Empty);
        return game.RaceId == 3 ||
               game.TribeId is 5 or 6 ||
               name.Contains("Lalafell", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Dunesfolk", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Plainsfolk", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ララフェル", StringComparison.Ordinal);
    }
}
