using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Persistence;
using Linkpearl.Talk;

namespace Linkpearl.Device.Shell.Studio;

internal readonly struct StudioHit
{
    public required string Title { get; init; }

    public required string Blurb { get; init; }

    public string AppletId { get; init; }

    public DestinationTab Tab { get; init; }

    public int Pane { get; init; }

    public string TalkId { get; init; }

    public string ProfileId { get; init; }

    public bool OpensTab { get; init; }
}

internal sealed class StudioHunt
{
    private const int Cap = 18;
    private readonly ISettings<SearchScratch> draft;
    private readonly SearchResult[] people = new SearchResult[16];
    private readonly StudioHit[] hits = new StudioHit[Cap];
    private string query = string.Empty;

    public StudioHunt(ISettings<SearchScratch> draft)
    {
        this.draft = draft;
        query = draft.Value.Studio ?? string.Empty;
    }

    public static float BarHeight(in AppletFrame frame) => frame.Units(36f);

    public bool IsOpen => query.Trim().Length > 0;

    public void Dismiss()
    {
        if (query.Length == 0)
        {
            return;
        }

        query = string.Empty;
        draft.Mutate(section => section.Studio = string.Empty);
    }

    public void Draw(in AppletFrame frame, Rect bar, Rect dropBound, IPearlHub pearl, ITalk talk, DestinationHub hub,
        Action<string, Rect> openApplet)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(bar, frame.Theme.Palette.SurfaceOverlay with { W = 0.78f }, bar.Height * 0.5f);
        frame.Paint.Stroke(bar, gold with { W = 0.38f }, frame.Theme.Metrics.Hairline, bar.Height * 0.5f);
        SearchMark.Draw(frame.Paint, bar.LeftSlice(frame.Units(28f)).Inset(frame.Units(5f)),
            frame.Theme.Palette.InkMuted);
        var type = bar.Inset(new Edges(frame.Units(28f), frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        var next = frame.TextField.Draw("studio-hunt", type, query, "Search the phone");
        if (!string.Equals(next, query, StringComparison.Ordinal))
        {
            query = next;
            draft.Mutate(section => section.Studio = query);
        }

        pearl.NoteQuery(query);

        if (IsOpen && frame.Input.EscapePressed())
        {
            Dismiss();
            frame.TextField.Release();
            return;
        }

        var count = Fill(pearl.Current, talk, query);
        var sheet = default(Rect);
        var hasSheet = false;
        if (count > 0)
        {
            var rowH = frame.Units(44f);
            var pad = frame.Units(8f);
            var gap = frame.Units(4f);
            var sheetH = MathF.Min(dropBound.Max.Y - bar.Max.Y - frame.Units(6f),
                pad * 2f + count * rowH + MathF.Max(0, count - 1) * gap);
            if (sheetH >= frame.Units(40f))
            {
                hasSheet = true;
                sheet = new Rect(new Vector2(bar.Min.X, bar.Max.Y + frame.Units(6f)),
                    new Vector2(bar.Max.X, bar.Max.Y + frame.Units(6f) + sheetH));
                frame.Paint.Fill(sheet, frame.Theme.Palette.SurfaceRaised with { W = 0.96f }, frame.Units(12f));
                frame.Paint.Stroke(sheet, gold with { W = 0.40f }, frame.Theme.Metrics.Hairline, frame.Units(12f));

                var inner = sheet.Inset(pad);
                var stack = new Stack(inner, StackAxis.Vertical, gap);
                for (var index = 0; index < count; index++)
                {
                    if (stack.Remaining.Height < rowH * 0.6f)
                    {
                        break;
                    }

                    DrawHit(frame, stack.Take(rowH), hits[index], hub, openApplet);
                }

                // After rows so a result can take the tap. Then cover the sheet so dock/widgets
                // under it cannot fire on the same press.
                frame.Input.Claim(sheet);
            }
        }

        if (!IsOpen || !frame.Input.PointerReleased())
        {
            return;
        }

        var at = frame.Input.Pointer;
        if (bar.Contains(at) || (hasSheet && sheet.Contains(at)))
        {
            return;
        }

        Dismiss();
        frame.TextField.Release();
        frame.Input.Claim(frame.Content);
    }

    private void DrawHit(in AppletFrame frame, Rect row, in StudioHit hit, DestinationHub hub,
        Action<string, Rect> openApplet)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        if (frame.Input.IsHovering(row))
        {
            frame.Paint.Fill(row, gold with { W = 0.16f }, frame.Units(8f));
        }

        var copy = row.Inset(new Edges(frame.Units(10f), frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(20f)), hit.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), hit.Blurb,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (!frame.Input.ConsumeClick(row) && !frame.Input.PressedInside(row))
        {
            return;
        }

        OpenHit(hit, hub, openApplet, row);
        Dismiss();
        frame.TextField.Release();
    }

    private static void OpenHit(in StudioHit hit, DestinationHub hub, Action<string, Rect> openApplet, Rect row)
    {
        var talkId = hit.TalkId ?? "";
        var profileId = hit.ProfileId ?? "";
        var appletId = hit.AppletId ?? "";
        if (talkId.Length > 0)
        {
            hub.OpenTalk(talkId);
            return;
        }

        if (profileId.Length > 0)
        {
            hub.OpenProfile(profileId);
            return;
        }

        if (appletId.Length > 0)
        {
            if (AppShelf.Find(appletId) is { Kind: AppKind.Shortcut } spec)
            {
                hub.Open(spec.Tab, spec.Pane, spec.Name);
                return;
            }

            openApplet(appletId, row);
            return;
        }

        if (hit.OpensTab)
        {
            hub.Open(hit.Tab, hit.Pane, hit.Title ?? "");
        }
    }

    private int Fill(PearlSnapshot snapshot, ITalk talk, string raw)
    {
        var needle = raw.Trim();
        if (needle.Length == 0)
        {
            return 0;
        }

        var cursor = 0;
        cursor = AppendPlaces(needle, cursor);
        cursor = AppendApps(needle, cursor);
        var found = DirectorySearch.Fill(snapshot, needle, people, talk);
        for (var index = 0; index < found && cursor < hits.Length; index++)
        {
            var person = people[index];
            if (Already(cursor, person.Title))
            {
                continue;
            }

            hits[cursor] = new StudioHit
            {
                Title = person.Title,
                Blurb = person.Kind.Length > 0 ? person.Kind + " · " + person.Subtitle : person.Subtitle,
                TalkId = person.TalkId,
                ProfileId = person.ProfileId,
            };
            cursor++;
        }

        return cursor;
    }

    private int AppendPlaces(string needle, int cursor)
    {
        cursor = Place(cursor, needle, "You", "Your profile", DestinationTab.You);
        cursor = Place(cursor, needle, "Explore", "Places and events", DestinationTab.Explore);
        cursor = Place(cursor, needle, "Announcements", "Linkpearl news", DestinationTab.Home,
            HomePane.Announcements);
        cursor = Place(cursor, needle, "Messages", "PearlChat", DestinationTab.Social, SocialPane.Messages);
        cursor = Place(cursor, needle, "Friends", "People", DestinationTab.Social, SocialPane.People);
        cursor = Place(cursor, needle, "General", "Lock and combat", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Appearance", "Themes and display", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Sounds", "Speaker and microphone", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Notifications", "Do not disturb", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Feed", "Say shout yell party", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Language & Time", "Clock and language", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Terms of service", "Legal", DestinationTab.Settings);
        cursor = Place(cursor, needle, "Credits", "Who built this phone", DestinationTab.Settings);
        return cursor;
    }

    private int AppendApps(string needle, int cursor)
    {
        for (var index = 0; index < AppShelf.Catalog.Length && cursor < hits.Length; index++)
        {
            var spec = AppShelf.Catalog[index];
            if (spec.Hidden ||
                (!Hit(spec.Name, needle) && !Hit(spec.Caption, needle) && !Hit(spec.Id, needle)))
            {
                continue;
            }

            if (Already(cursor, spec.Name))
            {
                continue;
            }

            if (spec.Kind == AppKind.Shortcut)
            {
                hits[cursor] = new StudioHit
                {
                    Title = spec.Name,
                    Blurb = spec.Caption,
                    Tab = spec.Tab,
                    Pane = spec.Pane,
                    OpensTab = true,
                };
            }
            else
            {
                hits[cursor] = new StudioHit
                {
                    Title = spec.Name,
                    Blurb = spec.Caption,
                    AppletId = spec.Id,
                };
            }

            cursor++;
        }

        return cursor;
    }

    private int Place(int cursor, string needle, string title, string blurb, DestinationTab tab, int pane = 0)
    {
        if (cursor >= hits.Length || (!Hit(title, needle) && !Hit(blurb, needle)))
        {
            return cursor;
        }

        if (Already(cursor, title))
        {
            return cursor;
        }

        hits[cursor] = new StudioHit
        {
            Title = title,
            Blurb = blurb,
            Tab = tab,
            Pane = pane,
            OpensTab = true,
        };
        return cursor + 1;
    }

    private bool Already(int cursor, string title)
    {
        for (var index = 0; index < cursor; index++)
        {
            if (string.Equals(hits[index].Title, title, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Hit(string hay, string needle) =>
        hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
