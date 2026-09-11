using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Applets.Life.Venues;
using Linkpearl.Calendar;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Notices;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Calendar;

public sealed class CalendarApplet : IApplet, IDisposable
{
    private static readonly int[] RemindOffsets = [-1, 0, 5, 15, 30, 60, 1440];
    private static readonly string[] RemindLabels =
        ["No alert", "At time", "5 min before", "15 min before", "30 min before", "1 hour before", "1 day before"];

    public static readonly AppletManifest Manifest = new()
    {
        Id = "calendar",
        DisplayNameKey = "Calendar",
        Family = AppletFamily.Life,
        Glyph = "▦",
        HomeOrder = 7,
        Capabilities = AppletCapabilities.BackgroundWork,
    };

    private readonly IClock clock;
    private readonly IFrameLoop frames;
    private readonly IChime chime;
    private readonly INoticeTray notices;
    private readonly CalendarBook book;
    private readonly VenuesDiary venues;
    private DateTime month;
    private DateTime selected;
    private float scroll;
    private bool editing;
    private CalendarKind draftKind;
    private string draftId = "";
    private string draftTitle = "";
    private int draftHour = 9;
    private int draftMinute;
    private int draftRemind = 1;

    public CalendarApplet(IClock clock, IFrameLoop frames, IChime chime, INoticeTray notices, CalendarBook book,
        VenuesDiary venues)
    {
        this.clock = clock;
        this.frames = frames;
        this.chime = chime;
        this.notices = notices;
        this.book = book;
        this.venues = venues;
        var now = clock.Now.Date;
        month = new DateTime(now.Year, now.Month, 1);
        selected = now;
        frames.Tick += OnTick;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public bool CanGoBack => editing;

    public string Place => selected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Enter(AppletEntry entry)
    {
        var now = clock.Now.Date;
        month = new DateTime(now.Year, now.Month, 1);
        selected = now;
        scroll = 0f;
        editing = false;
        if (entry.RouteHint is not { Length: > 0 } hint)
        {
            return;
        }

        if (book.Find(hint) is { } item)
        {
            OpenItem(item);
            return;
        }

        if (DateTime.TryParse(hint, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            ShowDay(day);
        }
    }

    public void Leave()
    {
        editing = false;
    }

    public bool Back()
    {
        if (!editing)
        {
            return false;
        }

        editing = false;
        return true;
    }

    public void Dispose()
    {
        frames.Tick -= OnTick;
        book.Dispose();
    }

    public void Compose(in AppletFrame frame)
    {
        var now = clock.Now;
        CalendarChrome.Paint(frame, frame.Content, now);
        var inner = frame.Content.Inset(new Edges(frame.Units(16f), frame.Units(8f), frame.Units(16f),
            frame.Units(10f)));
        if (editing)
        {
            DrawEditor(frame, inner);
            return;
        }

        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var page = inner.Translate(new Vector2(0f, -scroll));
        frame.Paint.PushClip(inner);
        var lines = ToChrome(book.ForDay(selected));
        var content = CalendarChrome.App(frame, page, now, month, selected, book.DayDots(month), WeekDots(now),
            lines, bells, out var hit);
        frame.Paint.PopClip();
        ApplyHit(hit, now);
        ScrollSlider.Apply(frame, inner, ref scroll, content);
    }

    private void ApplyHit(in CalendarAppHit hit, DateTimeOffset now)
    {
        if (hit.JumpToday)
        {
            ShowDay(now.Date);
            return;
        }

        if (hit.MonthStep != 0)
        {
            month = month.AddMonths(hit.MonthStep);
            var day = Math.Min(selected.Day, DateTime.DaysInMonth(month.Year, month.Month));
            selected = new DateTime(month.Year, month.Month, day);
            return;
        }

        if (hit.PickedDay is { } dayPick)
        {
            ShowDay(dayPick);
            return;
        }

        if (hit.OpenId is { Length: > 0 } id && book.Find(id) is { } item)
        {
            OpenItem(item);
            return;
        }

        if (hit.AddEvent)
        {
            BeginDraft(CalendarKind.Event);
        }
        else if (hit.AddReminder)
        {
            BeginDraft(CalendarKind.Reminder);
        }
    }

    private byte[] WeekDots(DateTimeOffset now)
    {
        var start = now.Date.AddDays(-(int)now.DayOfWeek);
        var dots = new byte[7];
        for (var index = 0; index < 7; index++)
        {
            dots[index] = (byte)book.DotsOn(start.AddDays(index));
        }

        return dots;
    }

    private void ShowDay(DateTime day)
    {
        selected = day.Date;
        month = new DateTime(day.Year, day.Month, 1);
    }

    private void BeginDraft(CalendarKind kind)
    {
        draftKind = kind;
        draftId = "";
        draftTitle = "";
        draftHour = Math.Clamp(clock.Now.Hour, 0, 23);
        draftMinute = 0;
        draftRemind = kind == CalendarKind.Reminder ? 1 : 1;
        editing = true;
    }

    private void OpenItem(CalendarItem item)
    {
        var local = item.StartsAt.ToLocalTime();
        ShowDay(local.Date);
        draftKind = item.Kind;
        draftId = item.Id;
        draftTitle = item.Title;
        draftHour = local.Hour;
        draftMinute = local.Minute;
        draftRemind = IndexOfRemind(item.RemindMinutes);
        if (item.Kind == CalendarKind.Reminder && draftRemind == 0)
        {
            draftRemind = 1;
        }

        editing = true;
    }

    private void DrawEditor(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var heading = draftKind == CalendarKind.Reminder ? "Reminder" : "Event";
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), heading,
            new TextStyle(FontRole.Title, CalendarChrome.Ink(true)));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            selected.ToString("dddd, MMM d", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.Caption, CalendarChrome.Hush(true)));
        var titleRow = stack.Take(frame.Units(36f));
        CardChrome.Draw(frame, titleRow);
        draftTitle = frame.TextField.Draw("cal-title", titleRow.Inset(frame.Units(8f)), draftTitle,
            draftKind == CalendarKind.Reminder ? "Remind me…" : "Event name", 80, out _);
        var setter = stack.Take(frame.Units(44f));
        DrawStepper(frame, setter.LeftSlice(setter.Width * 0.48f), ref draftHour, 0, 23, "h");
        DrawStepper(frame, setter.RightSlice(setter.Width * 0.48f), ref draftMinute, 0, 59, "m");
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Notify",
            new TextStyle(FontRole.CaptionStrong, CalendarChrome.Hush(true)));
        var remindRow = stack.Take(frame.Units(36f));
        CardChrome.Draw(frame, remindRow);
        var labels = draftKind == CalendarKind.Reminder ? RemindLabels.AsSpan(1).ToArray() : RemindLabels;
        var pick = draftKind == CalendarKind.Reminder ? Math.Max(0, draftRemind - 1) : draftRemind;
        pick = frame.TextField.Combo("cal-remind", remindRow.Inset(frame.Units(8f)), labels, pick);
        draftRemind = draftKind == CalendarKind.Reminder ? pick + 1 : pick;
        var save = stack.Take(frame.Units(40f));
        CardChrome.DrawGold(frame, save);
        frame.Text.DrawIn(save, "Save",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(save))
        {
            SaveDraft();
            editing = false;
        }

        if (draftId.Length > 0)
        {
            var drop = stack.Take(frame.Units(36f));
            frame.Text.DrawIn(drop, "Delete",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Negative, TextAlign.Center));
            if (frame.Input.ConsumeClick(drop))
            {
                book.Drop(draftId);
                venues.ForgetCalendar(draftId);
                editing = false;
            }
        }

        var cancel = stack.Take(frame.Units(32f));
        frame.Text.DrawIn(cancel, "Cancel",
            new TextStyle(FontRole.Caption, CalendarChrome.Hush(true), TextAlign.Center));
        if (frame.Input.ConsumeClick(cancel))
        {
            editing = false;
        }
    }

    private void SaveDraft()
    {
        var remind = RemindOffsets[Math.Clamp(draftRemind, 0, RemindOffsets.Length - 1)];
        if (draftKind == CalendarKind.Reminder && remind < 0)
        {
            remind = 0;
        }

        var starts = new DateTimeOffset(selected.Year, selected.Month, selected.Day, draftHour, draftMinute, 0,
            clock.Now.Offset);
        book.Put(new CalendarItem
        {
            Id = draftId,
            Kind = draftKind,
            Title = draftTitle.Trim(),
            StartsAt = starts,
            RemindMinutes = remind,
            LastFired = "",
        });
    }

    private void OnTick(float _)
    {
        book.FireDue(clock.Now, item =>
        {
            var title = CalendarBook.ShownTitle(item);
            var when = item.StartsAt.ToLocalTime().ToString("ddd, h:mm tt", CultureInfo.CurrentCulture);
            var detail = item.Kind == CalendarKind.Reminder ? when : "Starts " + when;
            if (TryVenueId(item.Id, out var venueId))
            {
                notices.PostVenue(venueId, title, detail, clock);
            }
            else
            {
                notices.PostCalendar(item.Id, title, detail, clock);
            }

            chime.Ring(title, detail);
        });
    }

    private static IReadOnlyList<CalendarDayLine> ToChrome(IReadOnlyList<CalendarAgendaLine> lines)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var mapped = new CalendarDayLine[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            mapped[index] = new CalendarDayLine(line.Id, line.Title, line.When, line.Kind);
        }

        return mapped;
    }

    private static int IndexOfRemind(int minutes)
    {
        for (var index = 0; index < RemindOffsets.Length; index++)
        {
            if (RemindOffsets[index] == minutes)
            {
                return index;
            }
        }

        return minutes < 0 ? 0 : 1;
    }

    private static void DrawStepper(in AppletFrame frame, Rect area, ref int value, int min, int max, string suffix)
    {
        CardChrome.Draw(frame, area);
        var down = area.LeftSlice(area.Width * 0.28f);
        var up = area.RightSlice(area.Width * 0.28f);
        frame.Text.DrawIn(down, "−", new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(up, "+", new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(area, value.ToString("00", CultureInfo.InvariantCulture) + suffix,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(down))
        {
            value = value <= min ? max : value - 1;
        }

        if (frame.Input.ConsumeClick(up))
        {
            value = value >= max ? min : value + 1;
        }
    }

    private static bool TryVenueId(string itemId, out string venueId)
    {
        venueId = string.Empty;
        if (itemId.StartsWith("vn:", StringComparison.Ordinal))
        {
            venueId = itemId[3..];
            return venueId.Length > 0;
        }

        if (!itemId.StartsWith("vs:", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = itemId[3..];
        var cut = rest.LastIndexOf(':');
        venueId = cut > 0 ? rest[..cut] : rest;
        return venueId.Length > 0;
    }
}
