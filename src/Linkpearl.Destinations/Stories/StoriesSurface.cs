using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Stories;

internal sealed class StoriesSurface
{
    private readonly IPearlHub pearl;
    private readonly IFilePicker files;
    private readonly IClock clock;
    private string selectedAuthor = string.Empty;
    private int slideIndex;
    private bool composing;
    private string draft = string.Empty;
    private string mediaPath = string.Empty;

    public StoriesSurface(IPearlHub pearl, IFilePicker files, IClock clock)
    {
        this.pearl = pearl;
        this.files = files;
        this.clock = clock;
    }

    public bool OverlayOpen => composing || selectedAuthor.Length > 0;

    public void Close()
    {
        composing = false;
        selectedAuthor = string.Empty;
        slideIndex = 0;
        draft = string.Empty;
        mediaPath = string.Empty;
    }

    public void Open(string authorId)
    {
        composing = false;
        selectedAuthor = authorId ?? string.Empty;
        slideIndex = 0;
        if (selectedAuthor.Length > 0)
        {
            pearl.WatchStory(selectedAuthor);
        }
    }

    public void OpenCompose()
    {
        selectedAuthor = string.Empty;
        composing = true;
        draft = string.Empty;
        mediaPath = string.Empty;
    }

    public bool Back()
    {
        if (composing)
        {
            composing = false;
            draft = string.Empty;
            mediaPath = string.Empty;
            return true;
        }

        if (selectedAuthor.Length > 0)
        {
            selectedAuthor = string.Empty;
            slideIndex = 0;
            return true;
        }

        return false;
    }

    public float ComposeOverlay(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new LayoutFlow(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;
        if (composing)
        {
            DrawCompose(frame, ref stack, snapshot);
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        DrawViewer(frame, ref stack, snapshot);
        return content.Height + inset * 2f;
    }

    public void DrawList(in AppletFrame frame, ref LayoutFlow stack, PearlSnapshot snapshot)
    {
        TakePickedPhoto();
        if (!snapshot.SignedIn)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "Sign in from You to load stories.");
            return;
        }

        if (!snapshot.StoriesLive)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)),
                "Pearlgate is not hosting stories on this server.");
            return;
        }

        DrawOwnRow(frame, stack.Take(frame.Units(72f)), snapshot);
        var rings = snapshot.Stories;
        var drew = false;
        for (var index = 0; index < rings.Length; index++)
        {
            var ring = rings[index];
            if (string.Equals(ring.AuthorId, snapshot.MeId, StringComparison.Ordinal))
            {
                continue;
            }

            var row = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, row);
            DrawRing(frame, row.Inset(frame.Units(12f)), ring, () => Open(ring.AuthorId));
            drew = true;
        }

        if (!drew && OwnRing(snapshot) is null)
        {
            DrawEmpty(frame, stack.Take(frame.Units(56f)),
                "No stories from you or your contacts yet.");
        }
    }

    public static void DrawRail(in AppletFrame frame, Rect row, PearlSnapshot snapshot, Action<string> open,
        Action? compose)
    {
        if (!snapshot.SignedIn)
        {
            DrawEmpty(frame, row, "Sign in from You to load stories.");
            return;
        }

        if (!snapshot.StoriesLive)
        {
            DrawEmpty(frame, row, "Stories are off on this Pearlgate.");
            return;
        }

        var cell = frame.Units(64f);
        var gap = frame.Units(8f);
        var x = row.Min.X;
        var add = Rect.FromSize(new Vector2(x, row.Min.Y), new Vector2(cell, row.Height));
        DrawRailAdd(frame, add, compose);
        x += cell + gap;
        for (var index = 0; index < snapshot.Stories.Length; index++)
        {
            var ring = snapshot.Stories[index];
            var cellRect = Rect.FromSize(new Vector2(x, row.Min.Y), new Vector2(cell, row.Height));
            if (cellRect.Min.X >= row.Max.X)
            {
                break;
            }

            DrawRailRing(frame, cellRect, ring);
            if (frame.Input.ConsumeClick(cellRect))
            {
                open(ring.AuthorId);
            }

            x += cell + gap;
        }

        if (snapshot.Stories.Length == 0)
        {
            var rest = new Rect(new Vector2(x, row.Min.Y), row.Max);
            frame.Text.DrawEllipsized(rest, "No stories yet",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
    }

    private void DrawOwnRow(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(frame.Units(12f));
        var own = OwnRing(snapshot);
        if (own is { } ring)
        {
            DrawRing(frame, inset, ring with { AuthorName = "Your story" }, () => Open(ring.AuthorId));
            return;
        }

        var stack = new LayoutFlow(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)), "Your story", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), "Add to stories",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var canPost = CanPost(snapshot);
        frame.Text.DrawIn(stack.Take(frame.Units(18f)),
            canPost ? "Photo or a line, then post" : PostBlocked(snapshot),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (canPost && frame.Input.ConsumeClick(row))
        {
            OpenCompose();
        }
    }

    private static void DrawRing(in AppletFrame frame, Rect inset, PearlStory story, Action open)
    {
        var stack = new LayoutFlow(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)),
            story.HasUnseen ? "Unseen" : "Story", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)),
            story.AuthorName.Length > 0 ? story.AuthorName : "Someone",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var count = story.Count > 0 ? story.Count : story.Items.Length;
        var detail = count <= 0
            ? "Open to view"
            : count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
              (count == 1 ? " story" : " stories");
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (frame.Input.ConsumeClick(inset))
        {
            open();
        }
    }

    private void DrawViewer(in AppletFrame frame, ref LayoutFlow stack, PearlSnapshot snapshot)
    {
        var ring = Find(snapshot.Stories, selectedAuthor);
        frame.Text.DrawIn(stack.Take(frame.Units(30f)), ring?.AuthorName ?? "Story",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (Chip(frame, stack.Take(frame.Units(32f)), "Back"))
        {
            Back();
            return;
        }

        if (ring is not PearlStory found)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "That story ring is gone.");
            return;
        }

        pearl.WatchStory(found.AuthorId);
        var slides = found.Items;
        if (slides.Length == 0)
        {
            var count = found.Count;
            DrawEmpty(frame, stack.TakeRemaining(),
                count > 0
                    ? "This ring has stories, but Pearlgate did not send the slides."
                    : "Nothing in this ring yet.");
            return;
        }

        slideIndex = Math.Clamp(slideIndex, 0, slides.Length - 1);
        var slide = slides[slideIndex];
        var media = stack.Take(frame.Units(220f));
        CardChrome.Draw(frame, media);
        DrawMedia(frame, media.Inset(frame.Units(8f)), slide.MediaUrl);
        var when = AnnouncementChrome.Ago(slide.CreatedAtUnix, clock.Now);
        if (when.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), when,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (slide.Body.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(80f)), slide.Body,
                new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        }

        var nav = stack.Take(frame.Units(36f));
        var half = nav.Width * 0.5f;
        if (slides.Length > 1 && Chip(frame, nav.LeftSlice(half - frame.Units(4f)), "Prev") && slideIndex > 0)
        {
            slideIndex--;
        }

        if (slides.Length > 1 && Chip(frame, nav.RightSlice(half - frame.Units(4f)), "Next") &&
            slideIndex < slides.Length - 1)
        {
            slideIndex++;
        }
    }

    private void DrawCompose(in AppletFrame frame, ref LayoutFlow stack, PearlSnapshot snapshot)
    {
        TakePickedPhoto();
        frame.Text.DrawIn(stack.Take(frame.Units(30f)), "New story",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (!CanPost(snapshot))
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), PostBlocked(snapshot));
            if (Chip(frame, stack.Take(frame.Units(32f)), "Back"))
            {
                Back();
            }

            return;
        }

        draft = frame.TextField.Write("story-draft", stack.Take(frame.Units(88f)), draft, "What are you up to?",
            280);
        var photo = stack.Take(frame.Units(120f));
        CardChrome.Draw(frame, photo);
        if (mediaPath.Length > 0)
        {
            DrawFile(frame, photo.Inset(frame.Units(8f)), mediaPath);
        }
        else
        {
            frame.Text.DrawIn(photo, "Photo optional",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        var actions = stack.Take(frame.Units(36f));
        var third = actions.Width / 3f;
        if (Chip(frame, actions.LeftSlice(third - frame.Units(4f)), "Photo"))
        {
            files.BeginImagePick();
        }

        if (Chip(frame, Rect.FromSize(new Vector2(actions.Min.X + third, actions.Min.Y),
                new Vector2(third - frame.Units(4f), actions.Height)), "Clear") && mediaPath.Length > 0)
        {
            mediaPath = string.Empty;
        }

        var ready = draft.Trim().Length > 0 || mediaPath.Length > 0;
        if (Chip(frame, actions.RightSlice(third - frame.Units(4f)), ready ? "Post" : "Need text") && ready)
        {
            pearl.PublishStory(draft, mediaPath);
            composing = false;
            draft = string.Empty;
            mediaPath = string.Empty;
        }

        if (Chip(frame, stack.Take(frame.Units(32f)), "Cancel"))
        {
            Back();
        }
    }

    private void TakePickedPhoto()
    {
        if (!files.TryTakeImages(out var picked) || picked.Count == 0)
        {
            return;
        }

        mediaPath = picked[0];
    }

    private void DrawMedia(in AppletFrame frame, Rect area, string url)
    {
        if (url.Length == 0)
        {
            frame.Text.DrawIn(area, "Text story",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
            return;
        }

        pearl.PrefetchMedia(url);
        var local = pearl.LocalMedia(url);
        if (local is { Length: > 0 })
        {
            DrawFile(frame, area, local);
            return;
        }

        frame.Text.DrawIn(area, "Loading photo…",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }

    private static void DrawFile(in AppletFrame frame, Rect area, string path)
    {
        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised, frame.Units(8f));
            return;
        }

        frame.Paint.ImageRounded(texture, area, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(8f));
    }

    private static void DrawRailAdd(in AppletFrame frame, Rect cell, Action? compose)
    {
        var pip = new Vector2(cell.Center.X, cell.Min.Y + cell.Width * 0.38f);
        frame.Paint.FillCircle(pip, cell.Width * 0.28f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(pip, cell.Width * 0.28f, frame.Theme.Palette.WarmAccent, frame.Units(1.5f));
        frame.Text.DrawIn(cell.BottomSlice(frame.Units(18f)), "Yours",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (compose is not null && frame.Input.ConsumeClick(cell))
        {
            compose();
        }
    }

    private static void DrawRailRing(in AppletFrame frame, Rect cell, PearlStory ring)
    {
        var accent = ring.HasUnseen ? frame.Theme.Palette.WarmAccent : frame.Theme.Palette.InkFaint;
        var pip = new Vector2(cell.Center.X, cell.Min.Y + cell.Width * 0.38f);
        frame.Paint.FillCircle(pip, cell.Width * 0.28f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(pip, cell.Width * 0.28f, accent, frame.Units(1.5f));
        var name = ring.AuthorName.Length > 0 ? ring.AuthorName : "Someone";
        var space = name.IndexOf(' ');
        var given = space < 0 ? name : name[..space];
        frame.Text.DrawEllipsized(cell.BottomSlice(frame.Units(18f)), given,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
    }

    private static PearlStory? OwnRing(PearlSnapshot snapshot)
    {
        if (snapshot.MeId.Length == 0)
        {
            return null;
        }

        return Find(snapshot.Stories, snapshot.MeId);
    }

    private static PearlStory? Find(PearlStory[] rings, string authorId)
    {
        if (authorId.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < rings.Length; index++)
        {
            if (string.Equals(rings[index].AuthorId, authorId, StringComparison.Ordinal))
            {
                return rings[index];
            }
        }

        return null;
    }

    private static bool CanPost(PearlSnapshot snapshot) =>
        snapshot.SignedIn && snapshot.StoriesLive && !snapshot.Muted && !snapshot.Banned;

    private static string PostBlocked(PearlSnapshot snapshot)
    {
        if (!snapshot.SignedIn)
        {
            return "Sign in from You to post a story.";
        }

        if (snapshot.Banned)
        {
            return "This account is suspended.";
        }

        if (snapshot.Muted)
        {
            return "Staff muted this handset.";
        }

        return "Pearlgate is not hosting stories on this server.";
    }

    private static bool Chip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.Caption, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(MathF.Min(area.Height, frame.Units(80f))), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}
