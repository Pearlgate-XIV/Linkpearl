using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Venues;

public sealed class VenuesApplet : IApplet
{
    private static readonly string[] Lanes = ["Open", "Soon", "All", "Saved"];
    private static readonly TimeSpan SoonWindow = TimeSpan.FromHours(6);

    public static readonly AppletManifest Manifest = new()
    {
        Id = "venues",
        DisplayNameKey = "Venues",
        Family = AppletFamily.Life,
        Glyph = "◉",
        Capabilities = AppletCapabilities.RequiresNetwork,
        HomeOrder = 21,
    };

    private readonly IVenuesDesk desk;
    private readonly VenuesBook book;
    private readonly VenuesDiary diary;
    private readonly IGameSession game;
    private readonly ILifestream lifestream;
    private readonly List<VenueSpot> shown = [];
    private readonly List<VenueSpot> picks = [];
    private IReadOnlyList<string> centers = [];
    private string query = string.Empty;
    private string opened = string.Empty;
    private string center = string.Empty;
    private string travelNote = string.Empty;
    private string planNote = string.Empty;
    private string shownKey = "\u0001";
    private bool here;
    private bool sfw;
    private int lane = 2;
    private int quiet;
    private int shownStamp = -1;
    private int filterStamp = -1;
    private float filterWide = -1f;
    private float filterHigh;
    private float scroll;
    private float pickDrag;
    private float pickTravel;
    private string pickSeed = string.Empty;
    private bool pickHeld;

    public VenuesApplet(IVenuesDesk desk, VenuesBook book, VenuesDiary diary, IGameSession game, ILifestream lifestream)
    {
        this.desk = desk;
        this.book = book;
        this.diary = diary;
        this.game = game;
        this.lifestream = lifestream;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => opened.Length > 0 ? opened : "browse";

    public void Enter(AppletEntry entry)
    {
        quiet = 24;
        lane = 2;
        travelNote = string.Empty;
        planNote = string.Empty;
        desk.Refresh();
        scroll = 0f;
        if (entry.RouteHint is { Length: > 0 } hint && desk.Find(hint) is not null)
        {
            opened = hint;
        }
    }

    public void Leave()
    {
    }

    public bool CanGoBack => opened.Length > 0;

    public bool Back()
    {
        if (opened.Length == 0)
        {
            return false;
        }

        opened = string.Empty;
        scroll = 0f;
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        if (quiet > 0)
        {
            quiet--;
        }

        if (desk.FetchedAt is null)
        {
            desk.Refresh();
        }

        VenuesChrome.PaintGround(frame, frame.Content);
        if (opened.Length > 0)
        {
            frame.Paint.PushClip(frame.Content);
            var placeHeight = DrawPlace(frame, frame.Content.Translate(new Vector2(0f, -scroll)));
            frame.Paint.PopClip();
            ScrollSlider.Apply(frame, frame.Content, ref scroll, placeHeight);
            return;
        }

        var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f),
            frame.Units(10f)));
        Collect();
        var chrome = DrawChrome(frame, body);
        var list = new Rect(new Vector2(body.Min.X, body.Min.Y + chrome), body.Max);
        if (list.Height < 8f)
        {
            return;
        }

        frame.Paint.PushClip(list.Inset(new Edges(-frame.Units(2f), 0f, -frame.Units(4f), 0f)));
        var height = DrawList(frame, list);
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, list, ref scroll, height);
    }

    private float DrawChrome(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        DrawTitle(frame, stack.Take(frame.Units(34f)));
        DrawSearch(frame, stack.Take(frame.Units(40f)));
        DrawLanes(frame, stack.Take(frame.Units(34f)));
        DrawFilters(frame, stack.Take(FilterHeight(frame, area.Width)));
        var status = StatusCopy();
        if (status.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), status,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        return area.Height - stack.Remaining.Height;
    }

    private float DrawList(in AppletFrame frame, Rect area)
    {
        var y = area.Min.Y - scroll;
        var rail = picks.Count > 0 && query.Trim().Length == 0 ? PickRailHeight(frame) : 0f;
        if (rail > 0f)
        {
            var band = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, rail));
            if (band.Max.Y > area.Min.Y && band.Min.Y < area.Max.Y)
            {
                DrawPickRail(frame, band);
            }

            y += rail + frame.Units(12f);
        }

        if (shown.Count == 0)
        {
            var empty = EmptyCopy();
            if (empty.Length > 0)
            {
                frame.Text.DrawWrapped(
                    Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, frame.Units(56f))),
                    empty, new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            }

            return rail + frame.Units(rail > 0f ? 68f : 56f);
        }

        var gap = frame.Units(12f);
        var card = frame.Units(156f);
        var stride = card + gap;
        var head = rail > 0f ? rail + frame.Units(12f) : 0f;
        var first = Math.Clamp((int)((scroll - head) / stride) - 1, 0, shown.Count);
        var last = Math.Min(shown.Count, first + (int)MathF.Ceiling(area.Height / stride) + 3);
        for (var index = first; index < last; index++)
        {
            var row = Rect.FromSize(new Vector2(area.Min.X, y + index * stride),
                new Vector2(area.Width - frame.Units(2f), card));
            if (row.Max.Y > area.Min.Y && row.Min.Y < area.Max.Y)
            {
                DrawCard(frame, row, shown[index]);
            }
        }

        for (var index = last; index < Math.Min(shown.Count, last + 2); index++)
        {
            desk.PrefetchBanner(shown[index].BannerUrl);
        }

        var listH = shown.Count * stride - gap;
        return (rail > 0f ? rail + frame.Units(12f) : 0f) + listH;
    }

    private string StatusCopy()
    {
        if (desk.Busy && desk.Spots.Count == 0)
        {
            return "Loading the FFXIV Venues index…";
        }

        return desk.Notice;
    }

    private float DrawPlace(in AppletFrame frame, Rect area)
    {
        var spot = desk.Find(opened);
        var hero = area.TopSlice(frame.Units(196f));
        DrawHero(frame, hero, spot);
        var back = Rect.FromSize(hero.Min + new Vector2(frame.Units(12f), frame.Units(12f)),
            new Vector2(frame.Units(34f), frame.Units(34f)));
        if (VenuesChrome.RoundMark(frame, back, "‹", false))
        {
            opened = string.Empty;
            scroll = 0f;
            return area.Height;
        }

        if (spot is not null)
        {
            var heart = Rect.FromSize(new Vector2(hero.Max.X - frame.Units(46f), hero.Min.Y + frame.Units(12f)),
                new Vector2(frame.Units(34f), frame.Units(34f)));
            if (VenuesChrome.RoundMark(frame, heart, book.Holds(spot.Id) ? "♥" : "♡", book.Holds(spot.Id)))
            {
                book.Toggle(spot.Id);
            }
        }

        var sheet = new Rect(new Vector2(area.Min.X, hero.Max.Y - frame.Units(18f)),
            new Vector2(area.Max.X, area.Max.Y + frame.Units(800f)));
        VenuesChrome.Sheet(frame, sheet);
        var body = new Rect(new Vector2(area.Min.X, hero.Max.Y - frame.Units(18f)), area.Max)
            .Inset(new Edges(frame.Units(16f), frame.Units(16f), frame.Units(16f), frame.Units(12f)));
        var stack = new Stack(body, StackAxis.Vertical, frame.Units(10f));
        if (spot is null)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(36f)), "That venue is no longer on the list.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return area.Height - stack.Remaining.Height;
        }

        var nameH = MathF.Max(frame.Units(28f),
            frame.Text.MeasureWrapped(spot.Name, FontRole.Title, body.Width).Y);
        frame.Text.DrawWrapped(stack.Take(nameH), spot.Name,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        DrawStatus(frame, stack.Take(frame.Units(24f)), spot);
        DrawLocation(frame, ref stack, body.Width, spot);
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), spot.HoursLine,
            new TextStyle(FontRole.CaptionStrong, spot.OpenNow ? VenuesChrome.Open : VenuesChrome.Soon));
        var week = VenueTimes.WeekLine(spot.Week);
        if (week.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(28f)), week,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        DrawPlans(frame, ref stack, body.Width, spot);
        if (planNote.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(28f)), planNote,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (spot.Tags.Count > 0)
        {
            var tagsH = VenuesChrome.ChipWrap(frame, Rect.FromSize(Vector2.Zero, new Vector2(body.Width, 400f)),
                spot.Tags, false);
            VenuesChrome.ChipWrap(frame, stack.Take(tagsH), spot.Tags, true);
        }

        if (spot.Description.Length > 0)
        {
            var wrap = frame.Text.MeasureWrapped(spot.Description, FontRole.Caption, body.Width - frame.Units(20f)).Y;
            var card = stack.Take(wrap + frame.Units(18f));
            frame.Paint.Fill(card, frame.Theme.Palette.SurfaceOverlay, frame.Units(16f));
            frame.Text.DrawWrapped(card.Inset(new Edges(frame.Units(12f), frame.Units(9f), frame.Units(12f),
                frame.Units(9f))), spot.Description,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink));
        }

        if (travelNote.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(28f)), travelNote,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        DrawLinks(frame, ref stack, body.Width, spot);
        VenuesChrome.Footer(frame, stack.Take(frame.Units(18f)));
        return hero.Height - frame.Units(18f) + (body.Height - stack.Remaining.Height) + frame.Units(16f);
    }

    private void DrawTitle(in AppletFrame frame, Rect row)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(72f)), "Venues",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var refresh = row.RightSlice(frame.Units(68f));
        frame.Text.DrawIn(refresh, desk.Busy ? "…" : "Refresh",
            new TextStyle(FontRole.CaptionStrong, VenuesChrome.Night, TextAlign.Right));
        if (frame.Input.ConsumeClick(refresh))
        {
            desk.Refresh(true);
        }
    }

    private void DrawSearch(in AppletFrame frame, Rect field)
    {
        VenuesChrome.SearchWell(frame, field);
        var next = frame.TextField.Draw("venues-query", field.Inset(new Edges(frame.Units(14f), 0f)), query,
            "Search venues");
        if (!string.Equals(next, query, StringComparison.Ordinal))
        {
            query = next;
            scroll = 0f;
        }
    }

    private void DrawLanes(in AppletFrame frame, Rect row)
    {
        var next = VenuesChrome.Segmented(frame, row, Lanes, lane);
        if (quiet <= 0 && next != lane)
        {
            lane = next;
            scroll = 0f;
        }
    }

    private static float PickRailHeight(in AppletFrame frame) =>
        frame.Units(22f) + frame.Units(148f);

    private void DrawPickRail(in AppletFrame frame, Rect area)
    {
        var title = pickSeed.Length > 0 ? "Because you like " + pickSeed : "Because you liked venues";
        frame.Text.DrawEllipsized(area.TopSlice(frame.Units(20f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        var lane = area.BottomSlice(frame.Units(148f));
        var gap = frame.Units(8f);
        var cardW = frame.Units(118f);
        var span = picks.Count * (cardW + gap) - gap;
        var max = MathF.Max(0f, span - lane.Width);
        if (frame.Input.IsHeld() && !pickHeld && frame.Input.IsHovering(lane))
        {
            pickHeld = true;
        }

        if (pickHeld && frame.Input.IsHeld())
        {
            pickTravel += MathF.Abs(frame.Input.PointerDelta.X);
            pickDrag = Math.Clamp(pickDrag - frame.Input.PointerDelta.X, 0f, max);
        }
        else
        {
            pickDrag = Math.Clamp(pickDrag, 0f, max);
        }

        frame.Paint.PushClip(lane.Inset(new Edges(0f, -frame.Units(2f), -frame.Units(4f), -frame.Units(2f))));
        var x = lane.Min.X - pickDrag;
        for (var index = 0; index < picks.Count; index++)
        {
            var cell = Rect.FromSize(new Vector2(x, lane.Min.Y), new Vector2(cardW, lane.Height));
            if (cell.Max.X > lane.Min.X && cell.Min.X < lane.Max.X)
            {
                DrawCard(frame, cell, picks[index]);
            }

            x += cardW + gap;
        }

        frame.Paint.PopClip();
        if (!frame.Input.IsHeld())
        {
            if (pickTravel >= frame.Units(12f))
            {
                frame.Input.ConsumeClick(lane);
            }

            pickTravel = 0f;
            pickHeld = false;
        }
    }

    private void CollectPicks()
    {
        picks.Clear();
        pickSeed = string.Empty;
        var saved = book.Saved;
        if (saved.Count == 0)
        {
            return;
        }

        var liked = new List<VenueSpot>();
        var tags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var worlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var centersLiked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var districts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sfwVotes = 0;
        for (var index = saved.Count - 1; index >= 0; index--)
        {
            var spot = desk.Find(saved[index]);
            if (spot is null)
            {
                continue;
            }

            liked.Add(spot);
            if (pickSeed.Length == 0)
            {
                pickSeed = spot.Name;
            }

            if (spot.Sfw)
            {
                sfwVotes++;
            }

            if (spot.World.Length > 0)
            {
                worlds.Add(spot.World);
            }

            if (spot.DataCenter.Length > 0)
            {
                centersLiked.Add(spot.DataCenter);
            }

            if (spot.District.Length > 0)
            {
                districts.Add(spot.District);
            }

            for (var tag = 0; tag < spot.Tags.Count; tag++)
            {
                var label = spot.Tags[tag];
                if (label.Length == 0)
                {
                    continue;
                }

                tags[label] = tags.TryGetValue(label, out var count) ? count + 1 : 1;
            }
        }

        if (liked.Count == 0)
        {
            pickSeed = string.Empty;
            return;
        }

        var preferSfw = sfwVotes * 2 >= liked.Count;
        var needTags = tags.Count > 0;
        var ranked = new List<(VenueSpot Spot, int Score)>();
        var spots = desk.Spots;
        for (var index = 0; index < spots.Count; index++)
        {
            var spot = spots[index];
            if (book.Holds(spot.Id))
            {
                continue;
            }

            var score = StyleScore(spot, tags, worlds, centersLiked, districts, preferSfw, needTags);
            if (score <= 0)
            {
                continue;
            }

            ranked.Add((spot, score));
        }

        ranked.Sort((left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            if (left.Spot.OpenNow != right.Spot.OpenNow)
            {
                return left.Spot.OpenNow ? -1 : 1;
            }

            return string.Compare(left.Spot.Name, right.Spot.Name, StringComparison.OrdinalIgnoreCase);
        });

        var take = Math.Min(12, ranked.Count);
        for (var index = 0; index < take; index++)
        {
            picks.Add(ranked[index].Spot);
        }
    }

    private static int StyleScore(VenueSpot spot, Dictionary<string, int> tags,
        HashSet<string> worlds, HashSet<string> centersLiked, HashSet<string> districts, bool preferSfw,
        bool needTags)
    {
        var shared = 0;
        var tagScore = 0;
        for (var index = 0; index < spot.Tags.Count; index++)
        {
            if (tags.TryGetValue(spot.Tags[index], out var weight))
            {
                shared++;
                tagScore += 4 + weight;
            }
        }

        if (needTags && shared == 0)
        {
            return 0;
        }

        var score = tagScore;
        if (spot.Sfw == preferSfw)
        {
            score += 2;
        }

        if (spot.District.Length > 0 && districts.Contains(spot.District))
        {
            score += 2;
        }

        if (spot.World.Length > 0 && worlds.Contains(spot.World))
        {
            score += 1;
        }

        if (spot.DataCenter.Length > 0 && centersLiked.Contains(spot.DataCenter))
        {
            score += 1;
        }

        return needTags ? score : (score >= 3 ? score : 0);
    }

    private float FilterHeight(in AppletFrame frame, float width)
    {
        var stamp = desk.Revision;
        if (stamp == filterStamp && MathF.Abs(filterWide - width) < 0.5f)
        {
            return filterHigh;
        }

        filterStamp = stamp;
        filterWide = width;
        filterHigh = Flow(frame, Rect.FromSize(Vector2.Zero, new Vector2(width, 400f)), FilterLabels(), -1, false);
        return filterHigh;
    }

    private void DrawFilters(in AppletFrame frame, Rect area)
    {
        var labels = FilterLabels();
        var picked = PickedFilter();
        Flow(frame, area, labels, picked, true);
    }

    private string[] FilterLabels()
    {
        var world = game.Character.WorldName.Trim();
        var extra = world.Length > 0 ? 2 : 1;
        var labels = new string[centers.Count + extra];
        var at = 0;
        labels[at++] = "All DCs";
        if (world.Length > 0)
        {
            labels[at++] = "Here";
        }

        for (var index = 0; index < centers.Count; index++)
        {
            labels[at++] = centers[index];
        }

        return labels;
    }

    private int PickedFilter()
    {
        if (here)
        {
            return game.Character.WorldName.Trim().Length > 0 ? 1 : 0;
        }

        if (center.Length == 0)
        {
            return 0;
        }

        var shift = game.Character.WorldName.Trim().Length > 0 ? 2 : 1;
        for (var index = 0; index < centers.Count; index++)
        {
            if (string.Equals(centers[index], center, StringComparison.OrdinalIgnoreCase))
            {
                return index + shift;
            }
        }

        return 0;
    }

    private float Flow(in AppletFrame frame, Rect area, IReadOnlyList<string> labels, int selected, bool paint)
    {
        var x = 0f;
        var y = 0f;
        var height = frame.Units(26f);
        var gap = frame.Units(6f);
        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            var width = MathF.Min(area.Width,
                frame.Text.Measure(label, FontRole.CaptionStrong).X + frame.Units(18f));
            if (x > 0f && x + width > area.Width)
            {
                x = 0f;
                y += height + gap;
            }

            var cell = Rect.FromSize(new Vector2(area.Min.X + x, area.Min.Y + y), new Vector2(width, height));
            if (paint && VenuesChrome.Chip(frame, cell, label, index == selected) && quiet <= 0)
            {
                ApplyFilter(index);
                scroll = 0f;
            }

            x += width + gap;
        }

        var sfwWidth = MathF.Min(area.Width,
            frame.Text.Measure("SFW", FontRole.CaptionStrong).X + frame.Units(18f));
        if (x > 0f && x + sfwWidth > area.Width)
        {
            x = 0f;
            y += height + gap;
        }

        var sfwCell = Rect.FromSize(new Vector2(area.Min.X + x, area.Min.Y + y), new Vector2(sfwWidth, height));
        if (paint && VenuesChrome.Chip(frame, sfwCell, "SFW", sfw) && quiet <= 0)
        {
            sfw = !sfw;
            scroll = 0f;
        }

        return y + height;
    }

    private void ApplyFilter(int index)
    {
        if (index <= 0)
        {
            here = false;
            center = string.Empty;
            return;
        }

        if (game.Character.WorldName.Trim().Length > 0 && index == 1)
        {
            here = true;
            center = string.Empty;
            return;
        }

        here = false;
        var shift = game.Character.WorldName.Trim().Length > 0 ? 2 : 1;
        var at = index - shift;
        center = at >= 0 && at < centers.Count ? centers[at] : string.Empty;
    }

    private void DrawCard(in AppletFrame frame, Rect row, VenueSpot spot)
    {
        var radius = frame.Units(20f);
        VenuesChrome.Card(frame, row, spot.OpenNow);
        if (!VenuesChrome.Banner(frame, desk, row, spot.BannerUrl, radius))
        {
            frame.Paint.Fill(row, new Vector4(0.16f, 0.06f, 0.10f, 0.98f), radius);
        }

        VenuesChrome.PosterScrim(frame, row);
        if (spot.OpenNow)
        {
            frame.Paint.Stroke(row, VenuesChrome.Open with { W = 0.50f }, frame.Units(1.2f), radius);
        }

        var pad = frame.Units(12f);
        if (spot.OpenNow || SoonSoon(spot))
        {
            var badgeW = spot.OpenNow ? frame.Units(46f) : frame.Units(50f);
            VenuesChrome.Badge(frame,
                Rect.FromSize(row.Min + new Vector2(pad, pad), new Vector2(badgeW, frame.Units(22f))),
                spot.OpenNow ? "OPEN" : "SOON",
                spot.OpenNow ? VenuesChrome.Open : VenuesChrome.Soon);
        }

        var heart = Rect.FromSize(new Vector2(row.Max.X - pad - frame.Units(32f), row.Min.Y + pad),
            new Vector2(frame.Units(32f), frame.Units(32f)));
        if (VenuesChrome.RoundMark(frame, heart, book.Holds(spot.Id) ? "♥" : "♡", book.Holds(spot.Id)))
        {
            book.Toggle(spot.Id);
            return;
        }

        var copy = row.Inset(new Edges(pad, 0f, pad, pad)).BottomSlice(frame.Units(58f));
        var stack = new Stack(copy, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(22f)), spot.Name,
            new TextStyle(FontRole.BodyStrong, Vector4.One));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(15f)), PlaceLine(spot),
            new TextStyle(FontRole.Caption, new Vector4(1f, 0.88f, 0.92f, 0.82f)));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(15f)), spot.HoursLine,
            new TextStyle(FontRole.CaptionStrong, spot.OpenNow ? VenuesChrome.Open : VenuesChrome.Soon));

        if (frame.Input.ConsumeClick(row))
        {
            opened = spot.Id;
            scroll = 0f;
        }
    }

    private void DrawHero(in AppletFrame frame, Rect area, VenueSpot? spot)
    {
        if (spot is not null && VenuesChrome.Banner(frame, desk, area, spot.BannerUrl, 0f))
        {
            frame.Paint.FillGradient(area.BottomSlice(frame.Units(72f)), new Vector4(0f, 0f, 0f, 0f),
                new Vector4(0.06f, 0.02f, 0.04f, 0.72f), GradientAxis.Vertical);
            return;
        }

        frame.Paint.Fill(area, new Vector4(0.16f, 0.06f, 0.10f, 1f));
        frame.Text.DrawIn(area, "No photo yet",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }

    private void DrawLocation(in AppletFrame frame, ref Stack stack, float width, VenueSpot spot)
    {
        var address = spot.Address.Length > 0 ? spot.Address : PlaceLine(spot);
        if (address.Length == 0 && !spot.CanTeleport)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), "No housing address listed.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return;
        }

        var goW = spot.CanTeleport ? frame.Units(56f) : 0f;
        var gap = spot.CanTeleport ? frame.Units(10f) : 0f;
        var textW = MathF.Max(1f, width - goW - gap);
        var copy = address.Length > 0 ? address : "Housing address";
        var textH = MathF.Max(frame.Units(36f), frame.Text.MeasureWrapped(copy, FontRole.Caption, textW).Y);
        var row = stack.Take(textH);
        if (address.Length > 0)
        {
            frame.Text.DrawWrapped(row.Inset(new Edges(0f, 0f, goW + gap, 0f)), address,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (spot.CanTeleport &&
            VenuesChrome.GoPill(frame, row.RightSlice(goW).TopSlice(frame.Units(36f)), lifestream.Ready))
        {
            Travel(spot);
        }
    }

    private void DrawPlans(in AppletFrame frame, ref Stack stack, float width, VenueSpot spot)
    {
        var canNotify = !spot.OpenNow && (spot.NextOpen is not null || spot.Week.Count > 0);
        var canSchedule = spot.Week.Count > 0 || spot.NextOpen is not null;
        if (!canNotify && !canSchedule)
        {
            return;
        }

        var gap = frame.Units(8f);
        var height = frame.Units(36f);
        var row = stack.Take(height);
        if (canNotify && canSchedule)
        {
            var half = (width - gap) * 0.5f;
            var watching = diary.Notifies(spot.Id);
            if (VenuesChrome.Button(frame, row.LeftSlice(half), watching ? "Notified" : "Notify", watching))
            {
                planNote = diary.ToggleNotify(spot);
            }

            var pinned = diary.Schedules(spot.Id);
            if (VenuesChrome.Button(frame, row.RightSlice(half), pinned ? "Scheduled" : "Schedule", pinned))
            {
                planNote = diary.ToggleSchedule(spot);
            }

            return;
        }

        if (canNotify)
        {
            var watching = diary.Notifies(spot.Id);
            if (VenuesChrome.Button(frame, row, watching ? "Notified" : "Notify", watching))
            {
                planNote = diary.ToggleNotify(spot);
            }

            return;
        }

        var scheduled = diary.Schedules(spot.Id);
        if (VenuesChrome.Button(frame, row, scheduled ? "Scheduled" : "Schedule", scheduled))
        {
            planNote = diary.ToggleSchedule(spot);
        }
    }

    private void DrawLinks(in AppletFrame frame, ref Stack stack, float width, VenueSpot spot)
    {
        var saved = book.Holds(spot.Id);
        var gap = frame.Units(8f);
        var height = frame.Units(36f);
        var half = (width - gap) * 0.5f;
        var first = stack.Take(height);
        if (VenuesChrome.Button(frame, first.LeftSlice(half), saved ? "Saved" : "Save", saved))
        {
            book.Toggle(spot.Id);
        }

        if (spot.Website.Length > 0)
        {
            if (VenuesChrome.Button(frame, first.RightSlice(half), "Website", false))
            {
                VenuesChrome.OpenUrl(spot.Website);
            }
        }
        else if (spot.Discord.Length > 0)
        {
            if (VenuesChrome.Button(frame, first.RightSlice(half), "Discord", false))
            {
                VenuesChrome.OpenUrl(spot.Discord);
            }
        }
        else if (VenuesChrome.Button(frame, first.RightSlice(half), "FFXIV Venues", false))
        {
            VenuesChrome.OpenUrl(spot.DirectoryUrl);
            return;
        }

        var second = stack.Take(height);
        var leftUsed = false;
        if (spot.Website.Length > 0 && spot.Discord.Length > 0)
        {
            if (VenuesChrome.Button(frame, second.LeftSlice(half), "Discord", false))
            {
                VenuesChrome.OpenUrl(spot.Discord);
            }

            leftUsed = true;
        }

        var venuesCell = leftUsed ? second.RightSlice(half) : second.LeftSlice(half);
        if (VenuesChrome.Button(frame, venuesCell, "FFXIV Venues", false))
        {
            VenuesChrome.OpenUrl(spot.DirectoryUrl);
        }
    }

    private void Travel(VenueSpot spot)
    {
        if (!spot.CanTeleport)
        {
            travelNote = "This venue has no ward and plot to travel to.";
            return;
        }

        if (!lifestream.Ready)
        {
            travelNote = "Install and enable Lifestream, then try again.";
            return;
        }

        if (lifestream.TryGoHome(spot.World, spot.District, spot.Ward, spot.Plot, spot.Apartment, spot.Subdivision))
        {
            travelNote = "Lifestream is taking you to " + spot.Address + ".";
            return;
        }

        travelNote = "Lifestream could not start that trip. Try again in a moment.";
    }

    private static void DrawStatus(in AppletFrame frame, Rect row, VenueSpot spot)
    {
        var x = 0f;
        var gap = frame.Units(6f);
        if (spot.OpenNow || SoonSoon(spot))
        {
            var width = spot.OpenNow ? frame.Units(46f) : frame.Units(50f);
            VenuesChrome.Badge(frame, Rect.FromSize(row.Min, new Vector2(width, row.Height)),
                spot.OpenNow ? "OPEN" : "SOON",
                spot.OpenNow ? VenuesChrome.Open : VenuesChrome.Soon);
            x += width + gap;
        }

        var mark = spot.Sfw ? "SFW" : "18+";
        var markW = frame.Units(40f);
        VenuesChrome.Badge(frame, Rect.FromSize(new Vector2(row.Min.X + x, row.Min.Y), new Vector2(markW, row.Height)),
            mark, spot.Sfw ? VenuesChrome.Open : VenuesChrome.Night);
        x += markW + gap;
        var meta = PlaceLine(spot);
        if (meta.Length > 0)
        {
            frame.Text.DrawIn(new Rect(new Vector2(row.Min.X + x, row.Min.Y), row.Max), meta,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
    }

    private void Collect()
    {
        var needle = query.Trim();
        var world = game.Character.WorldName.Trim();
        var now = DateTimeOffset.UtcNow;
        var stamp = desk.Revision;
        var bucket = lane is 0 or 1 ? (now.ToUnixTimeSeconds() / 30).ToString(CultureInfo.InvariantCulture) : string.Empty;
        var key = lane + "|" + (sfw ? "1" : "0") + "|" + (here ? "1" : "0") + "|" + center + "|" + needle + "|" +
                  world + "|" + book.Revision + "|" + bucket;
        if (stamp == shownStamp && string.Equals(key, shownKey, StringComparison.Ordinal))
        {
            return;
        }

        shownStamp = stamp;
        shownKey = key;
        filterStamp = -1;
        shown.Clear();
        CollectPicks();
        var spots = desk.Spots;
        centers = desk.DataCenters;
        for (var index = 0; index < spots.Count; index++)
        {
            var spot = spots[index];
            if (sfw && !spot.Sfw)
            {
                continue;
            }

            if (here && world.Length > 0 &&
                !string.Equals(spot.World, world, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!here && center.Length > 0 &&
                !string.Equals(spot.DataCenter, center, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lane == 0 && !spot.OpenNow)
            {
                continue;
            }

            if (lane == 1 && (spot.OpenNow || !SoonSoon(spot, now)))
            {
                continue;
            }

            if (lane == 3 && !book.Holds(spot.Id))
            {
                continue;
            }

            if (needle.Length > 0 && !Hit(spot, needle))
            {
                continue;
            }

            shown.Add(spot);
        }
    }

    private static bool Hit(VenueSpot spot, string needle)
    {
        return Contains(spot.Name, needle) || Contains(spot.World, needle) || Contains(spot.District, needle) ||
               Contains(spot.DataCenter, needle) || Contains(spot.Address, needle) || TagsHit(spot, needle);
    }

    private static bool TagsHit(VenueSpot spot, string needle)
    {
        for (var index = 0; index < spot.Tags.Count; index++)
        {
            if (Contains(spot.Tags[index], needle))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string hay, string needle) =>
        hay.Length > 0 && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool SoonSoon(VenueSpot spot, DateTimeOffset? now = null)
    {
        if (spot.OpenNow || spot.NextOpen is not { } next)
        {
            return false;
        }

        return next - (now ?? DateTimeOffset.UtcNow) <= SoonWindow;
    }

    private static string PlaceLine(VenueSpot spot)
    {
        if (spot.World.Length == 0)
        {
            return spot.DataCenter;
        }

        if (spot.District.Length == 0)
        {
            return spot.DataCenter.Length > 0 ? spot.World + " · " + spot.DataCenter : spot.World;
        }

        return spot.World + " · " + spot.District;
    }

    private string EmptyCopy()
    {
        if (desk.Spots.Count == 0)
        {
            return desk.Busy
                ? "First load can take a minute. Tap Refresh if this stays empty."
                : desk.Notice + " If this stays empty, tap Refresh.";
        }

        return lane switch
        {
            0 => "Nothing is open right now. Try Soon or All.",
            1 => "Nothing opening in the next few hours. Try All.",
            3 => "Save a venue to keep it here.",
            _ => "No venues match those filters.",
        };
    }
}
