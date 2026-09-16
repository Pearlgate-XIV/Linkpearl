using System.Globalization;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Clock;

public sealed partial class ClockApplet
{
    private void DrawWorld(in AppletFrame frame, Rect area)
    {
        var now = clock.Now.ToLocalTime();
        HandsOf(now, out var hour, out var minute, out var second);
        var showBells = display.ClockFace != ClockFace.Local;
        var pad = frame.Units(16f);
        var fab = FabRect(frame, area);
        var add = frame.Input.ConsumeClick(fab);
        var extra = showBells ? frame.Units(20f) : 0f;
        var content = MathF.Max(area.Height + frame.Units(40f),
            frame.Units(268f) + extra + state.Cities.Count * frame.Units(64f) + frame.Units(80f));
        ScrollSlider.Apply(frame, area, ref state.Scroll, content);
        frame.Paint.PushClip(area);
        var stack = new LayoutFlow(
            new Rect(new Vector2(area.Min.X, area.Min.Y - state.Scroll),
                new Vector2(area.Max.X, area.Min.Y - state.Scroll + content)),
            StackAxis.Vertical, frame.Units(10f));
        stack.Take(pad);
        var faceBox = stack.Take(frame.Units(188f));
        var radius = MathF.Min(faceBox.Width, faceBox.Height) * 0.46f;
        ClockChrome.Face(frame, faceBox.Center, radius, hour, minute, second, frame.Theme.Palette.Ink,
            frame.Theme.Palette.InkMuted, Accent(frame), seconds: true);

        var date = stack.Take(frame.Units(22f)).Inset(new Edges(pad, 0f));
        frame.Text.DrawIn(date, now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        var digital = stack.Take(frame.Units(20f)).Inset(new Edges(pad, 0f));
        frame.Text.DrawIn(digital, ClockLine(now, display.Use24HourClock) + "  " + ZoneLabel(now),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));

        if (showBells)
        {
            var bells = stack.Take(frame.Units(18f)).Inset(new Edges(pad, 0f));
            frame.Text.DrawIn(bells, "Eorzea  " + EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds()).Format(),
                new TextStyle(FontRole.Caption, Accent(frame), TextAlign.Center));
        }

        stack.Take(frame.Units(6f));
        for (var index = 0; index < state.Cities.Count; index++)
        {
            DrawCityRow(frame, stack.Take(frame.Units(58f)).Inset(new Edges(pad, 0f)), state.Cities[index],
                now);
        }

        frame.Paint.PopClip();
        ClockChrome.Fab(frame, fab, Accent(frame), frame.Theme.Palette.AccentInk);
        if (add)
        {
            state.CityHunt = string.Empty;
            state.Page = ClockPage.PickCity;
            state.Scroll = 0f;
        }
    }

    private void DrawCityRow(in AppletFrame frame, Rect row, string id, DateTimeOffset local)
    {
        if (!TryCityTime(id, out var when, out var eorzea))
        {
            return;
        }

        var city = ClockState.FindCity(id) ?? new ClockCity(id, id, string.Empty);
        var face = CoverFitSquare(row.LeftSlice(row.Height));
        if (eorzea)
        {
            HandsOfEorzea(clock.UtcNow, out var hour, out var minute, out _);
            ClockChrome.MiniFace(frame, face.Center, face.Width * 0.42f, hour, minute, frame.Theme.Palette.Ink,
                frame.Theme.Palette.InkMuted);
        }
        else
        {
            HandsOf(when, out var hour, out var minute, out _);
            ClockChrome.MiniFace(frame, face.Center, face.Width * 0.42f, hour, minute, frame.Theme.Palette.Ink,
                frame.Theme.Palette.InkMuted);
        }

        var drop = state.Cities.Count > 1 ? row.RightSlice(frame.Units(28f)) : default;
        var timeWidth = frame.Units(88f);
        var time = drop.IsEmpty
            ? row.RightSlice(timeWidth)
            : row.Inset(new Edges(0f, 0f, drop.Width + frame.Units(8f), 0f)).RightSlice(timeWidth);
        var copy = new Rect(new Vector2(face.Max.X + frame.Units(10f), row.Min.Y),
            new Vector2(time.Min.X - frame.Units(8f), row.Max.Y));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)), city.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var offset = eorzea ? "The Source" : CityOffset(local, when);
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(18f)),
            city.Place.Length > 0 ? city.Place + " · " + offset : offset,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(time, eorzea
                ? EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds()).Format()
                : ClockLine(when, display.Use24HourClock),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Right, scale: 0.92f));
        if (drop.IsEmpty)
        {
            return;
        }

        frame.Text.DrawIn(drop, "✕",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (frame.Input.ConsumeClick(drop))
        {
            state.DropCity(id);
        }
    }

    private void DrawAlarms(in AppletFrame frame, Rect area)
    {
        var pad = frame.Units(16f);
        var fab = FabRect(frame, area);
        var add = frame.Input.ConsumeClick(fab);
        var next = NextEnabled();
        var head = next is { } bell
            ? ClockState.UntilLine(bell, clock.Now)
            : "No alarms";
        var content = MathF.Max(area.Height + frame.Units(24f),
            frame.Units(72f) + Math.Max(1, state.Bells.Count) * frame.Units(92f) + frame.Units(88f));
        ScrollSlider.Apply(frame, area, ref state.Scroll, content);
        frame.Paint.PushClip(area);
        var stack = new LayoutFlow(
            new Rect(new Vector2(area.Min.X, area.Min.Y - state.Scroll),
                new Vector2(area.Max.X, area.Min.Y - state.Scroll + content)),
            StackAxis.Vertical, frame.Units(10f));
        stack.Take(pad);
        frame.Text.DrawIn(stack.Take(frame.Units(28f)).Inset(new Edges(pad, 0f)), head,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (state.Bells.Count == 0)
        {
            var empty = stack.Take(frame.Units(120f)).Inset(new Edges(pad, 0f));
            frame.Paint.Fill(empty, frame.Theme.Palette.SurfaceRaised, frame.Units(20f));
            frame.Text.DrawIn(empty.Inset(new Edges(frame.Units(16f), frame.Units(28f), frame.Units(16f), 0f))
                    .TopSlice(frame.Units(28f)), "No alarms",
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
            frame.Text.DrawIn(empty.Inset(new Edges(frame.Units(20f), frame.Units(56f), frame.Units(20f), 0f)),
                "Tap + to wake at a set hour.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }
        else
        {
            for (var index = 0; index < state.Bells.Count; index++)
            {
                DrawAlarmRow(frame, stack.Take(frame.Units(84f)).Inset(new Edges(pad, 0f)), state.Bells[index]);
            }
        }

        frame.Paint.PopClip();
        ClockChrome.Fab(frame, fab, Accent(frame), frame.Theme.Palette.AccentInk);
        if (add)
        {
            state.BeginAlarm(string.Empty);
        }
    }

    private void DrawAlarmRow(in AppletFrame frame, Rect row, ClockBell bell)
    {
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceRaised, frame.Units(18f));
        var inset = row.Inset(new Edges(frame.Units(16f), frame.Units(12f)));
        var toggle = inset.RightSlice(frame.Units(54f));
        var copy = inset.Inset(new Edges(0f, 0f, toggle.Width + frame.Units(8f), 0f));
        var ink = bell.Enabled ? frame.Theme.Palette.Ink : frame.Theme.Palette.InkMuted;
        var time = copy.TopSlice(frame.Units(34f));
        frame.Text.DrawIn(time.LeftSlice(time.Width * 0.72f), Stamp(bell.Hour, bell.Minute, display.Use24HourClock),
            new TextStyle(FontRole.Display, ink, TextAlign.Left, scale: 0.78f));
        if (!display.Use24HourClock)
        {
            frame.Text.DrawIn(time.RightSlice(time.Width * 0.28f).Inset(new Edges(0f, frame.Units(10f), 0f, 0f)),
                Period(bell.Hour), new TextStyle(FontRole.CaptionStrong, ink));
        }

        var label = bell.Label.Length > 0 ? bell.Label : "Alarm";
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(18f)),
            label + " · " + ClockState.DayLine(bell.Days) + " · " + ClockState.UntilLine(bell, clock.Now),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (ClockChrome.Switch(frame, toggle, bell.Enabled, Accent(frame)))
        {
            state.ToggleAlarm(bell.Id);
            return;
        }

        if (frame.Input.ConsumeClick(copy))
        {
            state.BeginAlarm(bell.Id);
        }
    }

    private void DrawEditAlarm(in AppletFrame frame)
    {
        var area = frame.Content.Inset(frame.Units(16f));
        var content = MathF.Max(area.Height + frame.Units(24f), frame.Units(520f));
        ScrollSlider.Apply(frame, area, ref state.Scroll, content);
        frame.Paint.PushClip(area);
        var stack = new LayoutFlow(
            new Rect(new Vector2(area.Min.X, area.Min.Y - state.Scroll),
                new Vector2(area.Max.X, area.Min.Y - state.Scroll + content)),
            StackAxis.Vertical, frame.Units(10f));
        var head = stack.Take(frame.Units(28f));
        frame.Text.DrawIn(head.LeftSlice(frame.Units(40f)), "‹",
            new TextStyle(FontRole.Title, Accent(frame), TextAlign.Center));
        frame.Text.DrawIn(head.Inset(new Edges(frame.Units(36f), 0f, 0f, 0f)),
            state.DraftId.Length > 0 ? "Edit alarm" : "Add alarm",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(head.LeftSlice(frame.Units(72f))))
        {
            state.Page = ClockPage.Tabs;
            state.Scroll = 0f;
            frame.Paint.PopClip();
            return;
        }

        var face = stack.Take(frame.Units(148f));
        ClockChrome.Face(frame, face.Center, MathF.Min(face.Width, face.Height) * 0.46f,
            state.DraftHour + state.DraftMinute / 60f, state.DraftMinute, 0f, frame.Theme.Palette.Ink,
            frame.Theme.Palette.InkMuted, Accent(frame), seconds: false);

        var digits = stack.Take(frame.Units(56f));
        if (display.Use24HourClock)
        {
            var hour = state.DraftHour;
            var minute = state.DraftMinute;
            StepValue(frame, digits.LeftSlice(digits.Width * 0.48f), ref hour, 0, 23);
            StepValue(frame, digits.RightSlice(digits.Width * 0.48f), ref minute, 0, 59);
            state.DraftHour = hour;
            state.DraftMinute = minute;
        }
        else
        {
            var hour12 = state.DraftHour % 12;
            if (hour12 == 0)
            {
                hour12 = 12;
            }

            var minute = state.DraftMinute;
            StepValue(frame, digits.LeftSlice(digits.Width * 0.36f), ref hour12, 1, 12);
            StepValue(frame, digits.Inset(new Edges(digits.Width * 0.38f, 0f, digits.Width * 0.30f, 0f)),
                ref minute, 0, 59);
            ApplyHour12(hour12);
            state.DraftMinute = minute;
            var period = digits.RightSlice(digits.Width * 0.28f);
            if (ClockChrome.Chip(frame, period.TopSlice(period.Height * 0.48f), "AM", state.DraftHour < 12,
                    Accent(frame)) &&
                state.DraftHour >= 12)
            {
                state.DraftHour -= 12;
            }

            if (ClockChrome.Chip(frame, period.BottomSlice(period.Height * 0.48f), "PM", state.DraftHour >= 12,
                    Accent(frame)) &&
                state.DraftHour < 12)
            {
                state.DraftHour += 12;
            }
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Repeat",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var days = stack.Take(frame.Units(36f));
        var gap = frame.Units(4f);
        var cell = (days.Width - gap * 6f) / 7f;
        for (var index = 0; index < 7; index++)
        {
            var day = (DayOfWeek)index;
            var chip = Rect.FromSize(new Vector2(days.Min.X + index * (cell + gap), days.Min.Y),
                new Vector2(cell, days.Height));
            if (ClockChrome.Chip(frame, chip, DayLetter(index), ClockState.DayOn(state.DraftDays, day), Accent(frame)))
            {
                state.DraftDays = ClockState.ToggleDay(state.DraftDays, day);
            }
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Label",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var field = stack.Take(frame.Units(40f));
        frame.Paint.Fill(field, frame.Theme.Palette.SurfaceRaised, frame.Units(12f));
        state.DraftLabel = frame.TextField.Draw("clock-alarm-label", field.Inset(new Edges(frame.Units(12f), 0f)),
            state.DraftLabel, "Alarm");

        var actions = stack.Take(frame.Units(48f));
        if (state.DraftId.Length > 0)
        {
            var drop = actions.LeftSlice(actions.Width * 0.36f);
            frame.Text.DrawIn(drop, "Delete",
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Negative, TextAlign.Center));
            if (frame.Input.ConsumeClick(drop))
            {
                state.DropAlarm(state.DraftId);
                frame.Paint.PopClip();
                return;
            }
        }

        var save = actions.RightSlice(actions.Width * 0.48f);
        frame.Paint.Fill(save, Accent(frame), save.Height * 0.5f);
        frame.Text.DrawIn(save, "Save",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(save))
        {
            state.CommitAlarm();
        }

        frame.Paint.PopClip();
    }

    private void DrawPickCity(in AppletFrame frame)
    {
        var area = frame.Content.Inset(new Edges(frame.Units(16f), frame.Units(12f), frame.Units(16f),
            frame.Units(12f)));
        var head = area.TopSlice(frame.Units(28f));
        frame.Text.DrawIn(head.LeftSlice(frame.Units(40f)), "‹",
            new TextStyle(FontRole.Title, Accent(frame), TextAlign.Center));
        frame.Text.DrawIn(head.Inset(new Edges(frame.Units(36f), 0f, 0f, 0f)), "Add a city",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(head.LeftSlice(frame.Units(72f))))
        {
            state.Page = ClockPage.Tabs;
            state.CityHunt = string.Empty;
            state.Scroll = 0f;
            return;
        }

        var hunt = area.Inset(new Edges(0f, frame.Units(36f), 0f, 0f)).TopSlice(frame.Units(40f));
        frame.Paint.Fill(hunt, frame.Theme.Palette.SurfaceRaised, frame.Units(12f));
        state.CityHunt = frame.TextField.Draw("clock-city-hunt", hunt.Inset(new Edges(frame.Units(12f), 0f)),
            state.CityHunt, "Search cities");

        var list = area.Inset(new Edges(0f, frame.Units(84f), 0f, 0f));
        var rows = new List<ClockCity>(ClockState.Catalog.Length);
        var needle = state.CityHunt.Trim();
        for (var index = 0; index < ClockState.Catalog.Length; index++)
        {
            var city = ClockState.Catalog[index];
            if (state.Cities.Contains(city.Id, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (needle.Length > 0 &&
                city.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
                city.Place.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            rows.Add(city);
        }

        var content = Math.Max(list.Height + 8f, Math.Max(1, rows.Count) * frame.Units(56f));
        ScrollSlider.Apply(frame, list, ref state.Scroll, content);
        frame.Paint.PushClip(list);
        if (rows.Count == 0)
        {
            frame.Text.DrawIn(list.TopSlice(frame.Units(40f)), "No matching cities",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = Rect.FromSize(new Vector2(list.Min.X, list.Min.Y - state.Scroll + index * frame.Units(56f)),
                new Vector2(list.Width, frame.Units(52f)));
            frame.Paint.Fill(row, frame.Theme.Palette.SurfaceRaised, frame.Units(14f));
            var city = rows[index];
            frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(14f), frame.Units(8f), frame.Units(14f), 0f))
                    .TopSlice(frame.Units(20f)), city.Name,
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
            frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(14f), 0f, frame.Units(14f), frame.Units(8f)))
                    .BottomSlice(frame.Units(16f)), city.Place,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            if (frame.Input.ConsumeClick(row))
            {
                state.CityHunt = string.Empty;
                state.AddCity(city.Id);
            }
        }

        frame.Paint.PopClip();
    }

    private void DrawTimer(in AppletFrame frame, Rect area)
    {
        var left = state.TimerLeft(clock.UtcNow);
        var running = state.TimerEnds is not null && !state.TimerDone;
        var pad = frame.Units(16f);
        var inner = area.Inset(new Edges(pad, pad, pad, frame.Units(8f)));
        var ringBox = inner.TopSlice(MathF.Min(inner.Height * 0.52f, frame.Units(220f)));
        var radius = MathF.Min(ringBox.Width, ringBox.Height) * 0.38f;
        var amount = state.TimerSet.TotalSeconds <= 0
            ? 0f
            : (float)(left.TotalSeconds / state.TimerSet.TotalSeconds);
        ClockChrome.Ring(frame, ringBox.Center, radius, running || state.TimerDone ? amount : 1f,
            frame.Theme.Palette.SurfaceRaised, state.TimerDone ? frame.Theme.Palette.Negative : Accent(frame),
            frame.Units(8f));
        frame.Text.DrawIn(Rect.FromSize(ringBox.Center - new Vector2(radius, frame.Units(18f)),
                new Vector2(radius * 2f, frame.Units(36f))),
            state.TimerDone ? "00:00" : ClockLabel(left),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center, scale: 0.82f));

        var rest = new Rect(new Vector2(inner.Min.X, ringBox.Max.Y + frame.Units(8f)), inner.Max);
        var stack = new LayoutFlow(rest, StackAxis.Vertical, frame.Units(10f));
        if (!running && !state.TimerDone)
        {
            var presets = stack.Take(frame.Units(36f));
            var gap = frame.Units(6f);
            var width = (presets.Width - gap * (TimerPresets.Length - 1)) / TimerPresets.Length;
            for (var index = 0; index < TimerPresets.Length; index++)
            {
                var seconds = TimerPresets[index];
                var cell = Rect.FromSize(new Vector2(presets.Min.X + index * (width + gap), presets.Min.Y),
                    new Vector2(width, presets.Height));
                var on = Math.Abs(state.TimerSet.TotalSeconds - seconds) < 0.5;
                if (ClockChrome.Chip(frame, cell, seconds >= 60 ? seconds / 60 + "m" : seconds + "s", on, Accent(frame)))
                {
                    state.SetTimer(TimeSpan.FromSeconds(seconds));
                    state.Save();
                }
            }

            var digits = stack.Take(frame.Units(52f));
            var col = (digits.Width - frame.Units(12f)) / 3f;
            var hour = state.TimerHour;
            var minute = state.TimerMinute;
            var second = state.TimerSecond;
            var dirty = StepValue(frame, Rect.FromSize(digits.Min, new Vector2(col, digits.Height)), ref hour, 0, 23);
            dirty |= StepValue(frame,
                Rect.FromSize(new Vector2(digits.Min.X + col + frame.Units(6f), digits.Min.Y),
                    new Vector2(col, digits.Height)), ref minute, 0, 59);
            dirty |= StepValue(frame, Rect.FromSize(new Vector2(digits.Max.X - col, digits.Min.Y),
                new Vector2(col, digits.Height)), ref second, 0, 59);
            state.TimerHour = hour;
            state.TimerMinute = minute;
            state.TimerSecond = second;
            if (dirty)
            {
                state.ApplyTimerDigits();
            }

            var labels = stack.Take(frame.Units(14f));
            frame.Text.DrawIn(Rect.FromSize(labels.Min, new Vector2(col, labels.Height)), "Hour",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
            frame.Text.DrawIn(
                Rect.FromSize(new Vector2(labels.Min.X + col + frame.Units(6f), labels.Min.Y),
                    new Vector2(col, labels.Height)), "Min",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
            frame.Text.DrawIn(Rect.FromSize(new Vector2(labels.Max.X - col, labels.Min.Y),
                    new Vector2(col, labels.Height)), "Sec",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        var actions = stack.Take(frame.Units(72f));
        var start = actions.Inset(new Edges(actions.Width * 0.18f, 0f, actions.Width * 0.18f, 0f));
        var leftAct = actions.LeftSlice(actions.Width * 0.28f);
        var rightAct = actions.RightSlice(actions.Width * 0.28f);
        if (ClockChrome.RoundAction(frame, start, running ? "Pause" : state.TimerDone ? "Again" : "Start", true,
                Accent(frame)))
        {
            state.ToggleTimer(clock.UtcNow);
            state.Save();
        }

        if (ClockChrome.RoundAction(frame, running ? rightAct : leftAct, "Reset", false, Accent(frame)))
        {
            state.ResetTimer();
            state.Save();
        }
    }

    private void DrawWatch(in AppletFrame frame, Rect area)
    {
        var elapsed = state.WatchElapsed(clock.UtcNow);
        var running = state.WatchStart is not null;
        var pad = frame.Units(16f);
        var inner = area.Inset(new Edges(pad, pad, pad, frame.Units(8f)));
        var ringBox = inner.TopSlice(MathF.Min(inner.Height * 0.46f, frame.Units(210f)));
        var radius = MathF.Min(ringBox.Width, ringBox.Height) * 0.38f;
        var tick = (float)(elapsed.TotalSeconds % 60.0) / 60f;
        ClockChrome.Ring(frame, ringBox.Center, radius, running || elapsed > TimeSpan.Zero ? tick : 0f,
            frame.Theme.Palette.SurfaceRaised, Accent(frame), frame.Units(7f));
        frame.Text.DrawIn(Rect.FromSize(ringBox.Center - new Vector2(radius, frame.Units(18f)),
                new Vector2(radius * 2f, frame.Units(36f))), WatchLabel(elapsed),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center, scale: 0.72f));

        var rest = new Rect(new Vector2(inner.Min.X, ringBox.Max.Y + frame.Units(4f)), inner.Max);
        var stack = new LayoutFlow(rest, StackAxis.Vertical, frame.Units(8f));
        var actions = stack.Take(frame.Units(72f));
        var third = actions.Width / 3f;
        if (ClockChrome.RoundAction(frame, Rect.FromSize(actions.Min, new Vector2(third, actions.Height)), "Lap",
                false, Accent(frame)))
        {
            state.LapWatch(clock.UtcNow);
        }

        if (ClockChrome.RoundAction(frame,
                Rect.FromSize(new Vector2(actions.Min.X + third, actions.Min.Y), new Vector2(third, actions.Height)),
                running ? "Pause" : "Start", true, Accent(frame)))
        {
            state.ToggleWatch(clock.UtcNow);
        }

        if (ClockChrome.RoundAction(frame, Rect.FromSize(new Vector2(actions.Max.X - third, actions.Min.Y),
                new Vector2(third, actions.Height)), "Reset", false, Accent(frame)))
        {
            state.ResetWatch();
            state.Scroll = 0f;
        }

        var laps = stack.Remaining;
        var content = Math.Max(laps.Height, state.Laps.Count * frame.Units(28f));
        ScrollSlider.Apply(frame, laps, ref state.Scroll, content);
        frame.Paint.PushClip(laps);
        for (var index = state.Laps.Count - 1; index >= 0; index--)
        {
            var fromBottom = state.Laps.Count - 1 - index;
            var row = Rect.FromSize(new Vector2(laps.Min.X, laps.Min.Y + fromBottom * frame.Units(28f) - state.Scroll),
                new Vector2(laps.Width, frame.Units(26f)));
            var prior = index == 0 ? TimeSpan.Zero : state.Laps[index - 1];
            var split = state.Laps[index] - prior;
            frame.Text.DrawIn(row.LeftSlice(row.Width * 0.28f),
                "Lap " + (index + 1).ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            frame.Text.DrawIn(row.Inset(new Edges(row.Width * 0.30f, 0f, row.Width * 0.36f, 0f)), WatchLabel(split),
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
            frame.Text.DrawIn(row.RightSlice(row.Width * 0.34f), WatchLabel(state.Laps[index]),
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
        }

        frame.Paint.PopClip();
    }

    private ClockBell? NextEnabled()
    {
        ClockBell? pick = null;
        var best = DateTimeOffset.MaxValue;
        var now = clock.Now;
        for (var index = 0; index < state.Bells.Count; index++)
        {
            var bell = state.Bells[index];
            if (!bell.Enabled)
            {
                continue;
            }

            var next = ClockState.NextRing(bell, now);
            if (next < best)
            {
                best = next;
                pick = bell;
            }
        }

        return pick;
    }

    private void ApplyHour12(int hour12)
    {
        var pm = state.DraftHour >= 12;
        var hour = hour12 % 12;
        state.DraftHour = pm ? hour + 12 : hour;
    }

    private static bool StepValue(in AppletFrame frame, Rect area, ref int value, int min, int max)
    {
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised, frame.Units(12f));
        var up = area.TopSlice(frame.Units(16f));
        var down = area.BottomSlice(frame.Units(16f));
        frame.Text.DrawIn(up, "▴",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        frame.Text.DrawIn(down, "▾",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        frame.Text.DrawIn(area, value.ToString("00", CultureInfo.InvariantCulture),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(up))
        {
            value = value >= max ? min : value + 1;
            return true;
        }

        if (frame.Input.ConsumeClick(down))
        {
            value = value <= min ? max : value - 1;
            return true;
        }

        return false;
    }

    private static Rect FabRect(in AppletFrame frame, Rect area)
    {
        var side = frame.Units(52f);
        return Rect.FromSize(new Vector2(area.Max.X - side - frame.Units(16f), area.Max.Y - side - frame.Units(12f)),
            new Vector2(side, side));
    }

    private static Rect CoverFitSquare(Rect area)
    {
        var side = MathF.Min(area.Width, area.Height);
        return Rect.FromSize(area.Center - new Vector2(side * 0.5f), new Vector2(side));
    }

    private static string ClockLine(DateTimeOffset when, bool twentyFour) =>
        twentyFour
            ? when.ToString("HH:mm", CultureInfo.CurrentCulture)
            : when.ToString("h:mm tt", CultureInfo.CurrentCulture);

    private static string ZoneLabel(DateTimeOffset when)
    {
        var offset = when.Offset;
        var hours = (int)offset.TotalHours;
        var minutes = Math.Abs(offset.Minutes);
        if (minutes == 0)
        {
            return hours == 0 ? "GMT" : "GMT" + hours.ToString("+0;-0", CultureInfo.InvariantCulture);
        }

        return "GMT" + offset.Hours.ToString("+0;-0", CultureInfo.InvariantCulture) + ":" +
               minutes.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string CityOffset(DateTimeOffset local, DateTimeOffset city)
    {
        var hours = (int)Math.Round((city.Offset - local.Offset).TotalHours);
        if (hours == 0)
        {
            return "Same time";
        }

        var day = city.Date.CompareTo(local.Date);
        var when = day > 0 ? "Tomorrow" : day < 0 ? "Yesterday" : "Today";
        var ahead = hours > 0 ? "+" : "";
        return when + " " + ahead + hours.ToString(CultureInfo.InvariantCulture) + " hr";
    }
}
