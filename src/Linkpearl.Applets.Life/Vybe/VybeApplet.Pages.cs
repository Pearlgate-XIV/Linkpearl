using System;
using System.Globalization;
using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Applets.Life.Camera;
using Linkpearl.Chat;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Vybe;

public sealed partial class VybeApplet
{
    private void DrawAuth(in AppletFrame frame, Rect area)
    {
        if (state.Page == NightPage.AuthLogin)
        {
            DrawAuthLogin(frame, area);
            return;
        }

        if (state.Page == NightPage.AuthCreate)
        {
            DrawAuthCreate(frame, area);
            return;
        }

        DrawAuthWelcome(frame, area);
    }

    private void DrawAuthWelcome(in AppletFrame frame, Rect area)
    {
        var tone = VybeChrome.Night;
        VybeChrome.Wheel(frame, area, state, frame.Units(720f + book.Seats.Count * 40f));
        var stack = new Stack(area.Inset(new Edges(0f, frame.Units(8f), 0f, 0f)).Translate(new Vector2(0f, -state.Scroll)),
            StackAxis.Vertical, frame.Units(8f));
        VybeChrome.LockMark(frame, stack.Take(frame.Units(72f)));
        var title = stack.Take(frame.Units(40f));
        frame.Text.DrawIn(title.TopSlice(frame.Units(20f)), "Welcome to",
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(title.BottomSlice(frame.Units(20f)), "VYBE",
            new TextStyle(FontRole.Title, tone.Accent, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Same community. Your account.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));

        var card = stack.Take(frame.Units(80f));
        VybeChrome.Plate(frame, card, frame.Units(14f), true);
        var rows = new Stack(card.Inset(frame.Units(10f)), StackAxis.Vertical, frame.Units(4f));
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "◌", "Use your Pearlgate sign-in on this handset");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "♥", "No VYBE password. Sign in on Pearlgate first");

        var have = book.Seats.Count > 0;
        var enter = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, enter, have ? "Log in" : "Create account", true);
        if (frame.Input.ConsumeClick(enter))
        {
            state.AuthNote = string.Empty;
            state.Page = have ? NightPage.AuthLogin : NightPage.AuthCreate;
            state.Scroll = 0f;
            return;
        }

        var other = stack.Take(frame.Units(44f));
        VybeChrome.Ghost(frame, other, have ? "Create account" : "I already have an account", true);
        if (frame.Input.ConsumeClick(other))
        {
            state.AuthNote = string.Empty;
            state.Page = have ? NightPage.AuthCreate : NightPage.AuthLogin;
            state.Scroll = 0f;
            return;
        }

        if (have)
        {
            DrawHandsetSeats(frame, ref stack, remove: false);
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
            "Accounts stay on this handset. Sign out whenever you want to come back.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private void DrawAuthLogin(in AppletFrame frame, Rect area)
    {
        var tone = VybeChrome.Night;
        VybeChrome.Wheel(frame, area, state, frame.Units(900f));
        var stack = new Stack(area.Translate(new Vector2(0f, -state.Scroll)), StackAxis.Vertical, frame.Units(10f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Log in", true))
        {
            state.AuthNote = string.Empty;
            state.Page = NightPage.Auth;
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "Use a handle you already made on this handset.", true);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "HANDLE", true);
        state.EnterHandle = frame.TextField.Draw("vybe-in-handle",
            VybeChrome.FieldWell(frame, stack.Take(frame.Units(44f)), true), state.EnterHandle, "@handle");
        DrawAuthNote(frame, ref stack, tone);
        var go = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, go, "Log in", true);
        if (frame.Input.ConsumeClick(go))
        {
            EnterVybe();
            return;
        }

        var swap = stack.Take(frame.Units(28f));
        frame.Text.DrawIn(swap, "Create account",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(swap))
        {
            state.AuthNote = string.Empty;
            state.Page = NightPage.AuthCreate;
        }
    }

    private void DrawAuthCreate(in AppletFrame frame, Rect area)
    {
        var tone = VybeChrome.Night;
        var dock = area.BottomSlice(frame.Units(state.AuthNote.Length > 0 ? 118f : 84f));
        var body = new Rect(area.Min, new Vector2(area.Max.X, dock.Min.Y - frame.Units(8f)));
        VybeChrome.Wheel(frame, body, state, frame.Units(720f + book.Seats.Count * 52f));
        var stack = new Stack(body.Translate(new Vector2(0f, -state.Scroll)), StackAxis.Vertical, frame.Units(10f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Create account", true))
        {
            state.AuthNote = string.Empty;
            state.DropSeatId = string.Empty;
            state.Page = NightPage.Auth;
            return;
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
            "Pick a handle. No password for now — Pearlgate sign-in is enough.",
            new TextStyle(FontRole.Caption, tone.Mute));
        DrawHandsetSeats(frame, ref stack, true);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "DISPLAY NAME", true);
        state.JoinName = frame.TextField.Draw("vybe-join-name",
            VybeChrome.FieldWell(frame, stack.Take(frame.Units(44f)), true), state.JoinName, "Display name");
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "TITLE", true);
        state.JoinTitle = frame.TextField.Draw("vybe-join-title",
            VybeChrome.FieldWell(frame, stack.Take(frame.Units(44f)), true), state.JoinTitle, "Title");
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "HANDLE", true);
        state.JoinHandle = frame.TextField.Draw("vybe-join-handle",
            VybeChrome.FieldWell(frame, stack.Take(frame.Units(44f)), true), state.JoinHandle, "@handle");

        var steps = new Stack(dock, StackAxis.Vertical, frame.Units(6f));
        DrawAuthNote(frame, ref steps, tone);
        var go = steps.Take(frame.Units(44f));
        VybeChrome.Primary(frame, go, "Create account", true);
        if (frame.Input.ConsumeClick(go))
        {
            JoinVybe();
            return;
        }

        var swap = steps.Take(frame.Units(24f));
        frame.Text.DrawIn(swap, "I already have an account",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(swap))
        {
            state.AuthNote = string.Empty;
            state.Page = NightPage.AuthLogin;
        }
    }

    private void DrawAuthNote(in AppletFrame frame, ref Stack stack, NightPalette tone)
    {
        if (state.AuthNote.Length == 0)
        {
            return;
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(32f)), state.AuthNote,
            new TextStyle(FontRole.Caption, tone.Danger, TextAlign.Center));
    }

    private void EnterVybe()
    {
        var pass = book.TryEnter(state.EnterHandle, state.EnterSecret);
        if (!pass.Ok || pass.Seat is null)
        {
            state.AuthNote = pass.Note;
            return;
        }

        profileStamp++;
        state.EnterSeat(pass.Seat);
        state.Save(paths);
    }

    private void JoinVybe()
    {
        var handle = state.JoinHandle.Trim().Length > 0 ? state.JoinHandle : state.JoinName;
        var pass = book.TryJoin(state.JoinName, handle);
        if (!pass.Ok || pass.Seat is null)
        {
            state.AuthNote = pass.Note;
            state.Scroll = 0f;
            return;
        }

        pass.Seat.Face.Honorific = ShownName.ClampTitle(state.JoinTitle);
        book.Save(paths);
        profileStamp++;
        state.EnterSeat(pass.Seat);
        state.Save(paths);
    }

    private void SignOutVybe()
    {
        if (state.HasAccount)
        {
            book.Keep(state);
            book.Save(paths);
        }

        state.ClearSession();
        state.Save(paths);
    }

    private void DropVybe()
    {
        if (!state.HasAccount)
        {
            return;
        }

        if (!state.DropConfirm)
        {
            state.DropConfirm = true;
            return;
        }

        book.Drop(state.AccountId);
        book.Save(paths);
        state.ClearSession();
        state.Save(paths);
    }

    private void ForgetSeat(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        if (!string.Equals(state.DropSeatId, id, StringComparison.Ordinal))
        {
            state.DropSeatId = id;
            return;
        }

        var tag = VybeBook.Tag(state.EnterHandle);
        book.TrySeat(id, out var gone);
        book.Drop(id);
        book.Save(paths);
        state.DropSeatId = string.Empty;
        if (gone is not null && string.Equals(VybeBook.Tag(gone.Handle), tag, StringComparison.Ordinal))
        {
            state.EnterHandle = string.Empty;
        }
    }

    private void DrawHandsetSeats(in AppletFrame frame, ref Stack stack, bool remove)
    {
        if (book.Seats.Count == 0)
        {
            return;
        }

        var tone = VybeChrome.Night;
        VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "ON THIS HANDSET", true);
        foreach (var seat in book.Seats.ToArray())
        {
            var row = stack.Take(frame.Units(40f));
            VybeChrome.Plate(frame, row, frame.Units(12f), true);
            var inner = row.Inset(new Edges(frame.Units(10f), 0f));
            var kill = inner.RightSlice(frame.Units(64f));
            frame.Text.DrawEllipsized(inner.Inset(new Edges(0f, 0f, frame.Units(70f), 0f)),
                seat.DisplayName.Length > 0 ? seat.DisplayName + "  " + seat.Handle : seat.Handle,
                new TextStyle(FontRole.CaptionStrong, tone.Ink));
            if (!remove)
            {
                if (frame.Input.ConsumeClick(row))
                {
                    profileStamp++;
                    state.EnterSeat(seat);
                    state.Save(paths);
                }

                continue;
            }

            var warn = string.Equals(state.DropSeatId, seat.Id, StringComparison.Ordinal);
            frame.Text.DrawIn(kill, warn ? "Sure?" : "Delete",
                new TextStyle(FontRole.CaptionStrong, tone.Danger, TextAlign.Center));
            if (frame.Input.ConsumeClick(kill))
            {
                ForgetSeat(seat.Id);
                return;
            }

            if (frame.Input.ConsumeClick(inner.Inset(new Edges(0f, 0f, frame.Units(70f), 0f))))
            {
                state.DropSeatId = string.Empty;
                profileStamp++;
                state.EnterSeat(seat);
                state.Save(paths);
                return;
            }
        }
    }

    private void DrawGate(in AppletFrame frame, Rect area)
    {
        var tone = VybeChrome.Night;
        var blocked = state.PlusBlocked;
        var close = area.TopSlice(frame.Units(22f)).RightSlice(frame.Units(28f));
        frame.Text.DrawIn(close, "×", new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(close) || frame.Input.ConsumeClick(area.TopSlice(frame.Units(22f)).RightSlice(frame.Units(40f))))
        {
            LeavePlusGate();
            return;
        }

        var stack = new Stack(area.Inset(new Edges(0f, frame.Units(8f), 0f, 0f)), StackAxis.Vertical, frame.Units(8f));
        VybeChrome.LockMark(frame, stack.Take(frame.Units(72f)));

        var title = stack.Take(frame.Units(40f));
        frame.Text.DrawIn(title.TopSlice(frame.Units(20f)), "Unlock More with",
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(title.BottomSlice(frame.Units(20f)), "VYBE+",
            new TextStyle(FontRole.Title, tone.Accent, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Same community. More freedom.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));

        var card = stack.Take(frame.Units(132f));
        VybeChrome.Plate(frame, card, frame.Units(14f), true);
        var rows = new Stack(card.Inset(frame.Units(10f)), StackAxis.Vertical, frame.Units(4f));
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "♥", "Opt-in to view and share NSFW content (18+)");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "☺", "Your profile stays the same");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "◌", "NSFW content is hidden from SFW users");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "⚙", "Switch anytime on your profile");

        if (blocked)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
                "VYBE+ is not available on Lalafell characters.",
                new TextStyle(FontRole.Caption, tone.Danger, TextAlign.Center));
        }

        var enter = stack.Take(frame.Units(44f));
        VybeChrome.PlusButton(frame, enter, "Enable NSFW Mode", !blocked);
        if (!blocked && frame.Input.ConsumeClick(enter))
        {
            var back = state.ReturnTo;
            state.AgreePlus();
            if (back == NightPage.OnboardIdentity)
            {
                state.Page = NightPage.OnboardIdentity;
            }
            else
            {
                state.Page = state.Onboarded ? NightPage.Tabs : NightPage.OnboardIdentity;
                state.Tab = NightTab.Profile;
            }

            state.Save(paths);
            return;
        }

        var leave = stack.Take(frame.Units(28f));
        frame.Text.DrawIn(leave, "Not Now",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(leave))
        {
            LeavePlusGate();
            return;
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
            "You must be 18+ to enable NSFW mode. We take safety and privacy seriously.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private static void DrawGateFact(in AppletFrame frame, Rect row, string mark, string copy)
    {
        var tone = VybeChrome.Night;
        frame.Text.DrawIn(row.LeftSlice(frame.Units(22f)), mark,
            new TextStyle(FontRole.BodyStrong, tone.Accent, TextAlign.Center));
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(26f), 0f, 0f, 0f)), copy,
            new TextStyle(FontRole.Caption, tone.Ink));
    }

    private void LeavePlusGate()
    {
        state.Mode = SocialMode.Daylight;
        state.Page = state.ReturnTo is NightPage.Settings or NightPage.Tabs or NightPage.OnboardIdentity
            ? state.ReturnTo
            : NightPage.Tabs;
        if (state.Page == NightPage.Gate)
        {
            state.Page = NightPage.Tabs;
        }

        state.Save(paths);
    }

    private void DrawRules(in AppletFrame frame, Rect area)
    {
        VybeChrome.Wheel(frame, area, state, frame.Units(720f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "VYBE+ Community Rules", true))
        {
            state.Page = NightPage.Gate;
            state.Scroll = 0f;
            return;
        }

        string[] blocks =
        {
            "Adults only (18+). Content involving minors or child-like characters is banned.",
            "Consent first. No unsolicited explicit messages, pressure, or contacting people who declined.",
            "Respect boundaries. Do not bypass blocks or ask others to reach someone for you.",
            "Illegal content is a permanent ban. Fantasy is not an exemption.",
            "Protect privacy. Doxxing is a permanent ban.",
            "Respect creators. Do not post stolen, leaked, or claimed work.",
            "No spam, scams, or venue ads here — use classifieds for that.",
            "Be respectful. Harassment, hate, threats, and stalking are out.",
            "Respect moderation. Do not evade bans. Appeals are welcome if they stay civil.",
        };
        for (var index = 0; index < blocks.Length; index++)
        {
            var card = stack.Take(frame.Units(64f));
            VybeChrome.Plate(frame, card, frame.Units(10f), true);
            frame.Text.DrawWrapped(card.Inset(frame.Units(8f)), blocks[index],
                new TextStyle(FontRole.Caption, VybeChrome.Night.Ink));
        }
    }

    private void DrawOnboard(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var wheel = state.Page switch
        {
            NightPage.OnboardIntent => 720f,
            NightPage.OnboardAbout => 900f,
            NightPage.OnboardReady => 780f,
            _ => state.PlusAgreed ? 2480f : 2200f,
        };
        VybeChrome.Wheel(frame, area, state, frame.Units(wheel));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(10f));
        var title = state.Page switch
        {
            NightPage.OnboardIntent => "Choose your interests",
            NightPage.OnboardAbout => "Say hello",
            NightPage.OnboardReady => "VYBE+",
            _ => state.Onboarded ? "Edit profile" : "Account created",
        };
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
        {
            if (state.Onboarded)
            {
                state.Page = NightPage.Tabs;
            }
            else if (state.Page == NightPage.OnboardIdentity)
            {
                SignOutVybe();
            }
            else
            {
                state.Page = NightPage.OnboardIdentity;
                state.Scroll = 0f;
            }

            return;
        }

        if (!state.Onboarded && state.Page == NightPage.OnboardIdentity)
        {
            var leave = stack.Take(frame.Units(22f));
            frame.Text.DrawIn(leave, "Sign out",
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Accent, TextAlign.Center));
            if (frame.Input.ConsumeClick(leave))
            {
                SignOutVybe();
                return;
            }
        }

        if (state.Page == NightPage.OnboardIdentity)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                state.Onboarded
                    ? "This is the first thing people see in Discover."
                    : "Your account is ready. Finish how you show up in Discover.", night);
            var heroArea = stack.Take(frame.Units(188f));
            var hero = DrawProfileHero(frame, heroArea, night, OwnBannerPath(),
                VybeChrome.Tone(night).AccentDim, string.Empty, OwnFacePath(),
                VybeChrome.Tone(night).Accent, own: true, back: false, tools: false);
            if (hero.Face)
            {
                OpenOwnStill(face: true);
            }
            else if (hero.Banner)
            {
                OpenOwnStill(face: false);
            }

            var shown = EditableName();
            var next = frame.TextField.Draw("ad-name-" + profileStamp, stack.Take(frame.Units(34f)), shown,
                "Display name");
            if (next != shown)
            {
                state.DisplayName = next;
            }

            var honor = ProfileHonorific();
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "TITLE", night);
            var honorNext = frame.TextField.Draw("ad-honor-" + profileStamp, stack.Take(frame.Units(34f)), honor,
                "Title");
            if (honorNext != honor)
            {
                state.Honorific = ShownName.ClampTitle(honorNext);
            }

            state.Handle = frame.TextField.Draw("ad-handle", stack.Take(frame.Units(34f)), state.Handle, "Handle");
            state.Pronouns = frame.TextField.Draw("ad-pronouns", stack.Take(frame.Units(34f)), state.Pronouns,
                "Pronouns");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "BIO", night);
            state.About = frame.TextField.Draw("ad-bio", stack.Take(frame.Units(34f)), state.About,
                "Write a short bio");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "TIME ZONE", night);
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                ZoneClock.Line(display.OwnTimeZoneId, display.Use24HourClock) +
                "  ·  Change this on the Settings profile.", night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "RACE", night);
            VybeChrome.FlowOne(frame, ref stack, SceneBook.Races, state.Race, value =>
            {
                state.Race = value;
            }, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "GENDER", night);
            VybeChrome.FlowMany(frame, ref stack, SceneBook.Genders, state.Genders, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "SEXUALITY", night);
            VybeChrome.FlowMany(frame, ref stack, SceneBook.Sexualities, state.Sexualities, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "DMS", night);
            var dms = VybeChrome.Pair(frame, stack.Take(frame.Units(34f)), "Open", "Closed",
                state.DmsOpen ? 0 : 1, night);
            if (dms >= 0)
            {
                state.DmsOpen = dms == 0;
            }

            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "INTERESTS", night);
            var intents = night ? SceneBook.Intents : SceneBook.DayIntents;
            VybeChrome.FlowMany(frame, ref stack, intents, state.Intents, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "RELATIONSHIP", night);
            VybeChrome.FlowOne(frame, ref stack, SceneBook.Relationships, state.Relationship, value =>
            {
                state.Relationship = value;
            }, night);
            if (state.Onboarded)
            {
                DrawPlusSettings(frame, ref stack, night);
                DrawLaneEntry(frame, ref stack, night);
            }
        }
        else if (state.Page == NightPage.OnboardIntent)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "Choose everything that fits. It shapes who finds you.", night);
            var intents = night ? SceneBook.Intents : SceneBook.DayIntents;
            VybeChrome.FlowMany(frame, ref stack, intents, state.Intents, night);
        }
        else if (state.Page == NightPage.OnboardAbout)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(20f)), "A line or two goes a long way.", night);
            state.About = frame.TextField.Draw("ad-about", stack.Take(frame.Units(64f)), state.About,
                "Introduce yourself");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "VIBE", night);
            VybeChrome.FlowMany(frame, ref stack, SceneBook.Tone, state.Tags, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "ORIENTATION", night);
            VybeChrome.FlowMany(frame, ref stack, SceneBook.Sexualities, state.Sexualities, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(16f)), "STATUS", night);
            VybeChrome.FlowOne(frame, ref stack, SceneBook.Relationships, state.Relationship, value =>
            {
                state.Relationship = value;
            }, night);
        }
        else
        {
            DrawPlusAbout(frame, ref stack, night);
        }

        var go = stack.Take(frame.Units(44f));
        var goLabel = state.Onboarded && state.Page == NightPage.OnboardIdentity
            ? "Save"
            : state.Page == NightPage.OnboardReady
                ? "Enter VYBE"
                : "Continue";
        VybeChrome.Primary(frame, go, goLabel, night);
        if (frame.Input.ConsumeClick(go))
        {
            AdvanceOnboard();
        }
    }

    private void DrawPlusAbout(in AppletFrame frame, ref Stack stack, bool night)
    {
        var tone = VybeChrome.Tone(night);
        VybeChrome.LockMark(frame, stack.Take(frame.Units(72f)));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), "What is VYBE+?",
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Same community. More freedom.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
        var card = stack.Take(frame.Units(158f));
        VybeChrome.Plate(frame, card, frame.Units(14f), night);
        var rows = new Stack(card.Inset(frame.Units(10f)), StackAxis.Vertical, frame.Units(4f));
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "♥", "An 18+ space to view and share NSFW content");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "☺", "Your VYBE profile stays the same");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "◌", "NSFW stays hidden from SFW browsers");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "⚙", "You opt in later from Edit profile");
        DrawGateFact(frame, rows.Take(frame.Units(26f)), "✦", "Unlock a role survey after VYBE+ is on");
        frame.Text.DrawWrapped(stack.Take(frame.Units(48f)),
            "You cannot turn VYBE+ on during setup. After you enter VYBE, open Profile, tap Edit profile, and unlock it there.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private void StepOnboard(NightPage page)
    {
        state.Page = page;
        state.Scroll = 0f;
        state.Save(paths);
    }

    private void AdvanceOnboard()
    {
        if (state.Page == NightPage.OnboardIdentity)
        {
            if (state.Onboarded)
            {
                state.Scroll = 0f;
                state.Page = NightPage.Tabs;
                state.Tab = NightTab.Profile;
                state.Save(paths);
                return;
            }

            StepOnboard(NightPage.OnboardReady);
            return;
        }

        if (state.Page is NightPage.OnboardIntent or NightPage.OnboardAbout)
        {
            StepOnboard(NightPage.OnboardReady);
            return;
        }

        state.Onboarded = true;
        state.Scroll = 0f;
        state.Page = NightPage.Tabs;
        state.Save(paths);
    }

    private void DrawStack(in AppletFrame frame, Rect area)
    {
        switch (state.Page)
        {
            case NightPage.FeedWall:
                DrawFeed(frame, area);
                break;
            case NightPage.Person:
                DrawPerson(frame, area);
                break;
            case NightPage.Post:
                DrawPost(frame, area);
                break;
            case NightPage.Compose:
                DrawCompose(frame, area);
                break;
            case NightPage.Filters:
                DrawPeopleFilters(frame, area);
                break;
            case NightPage.Chat:
                DrawChat(frame, area);
                break;
            case NightPage.Requests:
                DrawRequests(frame, area);
                break;
            case NightPage.Settings:
                DrawSettings(frame, area);
                break;
            case NightPage.Gallery:
                DrawGallery(frame, area);
                break;
            case NightPage.Likes:
                DrawLikes(frame, area);
                break;
            case NightPage.Search:
                DrawSearch(frame, area);
                break;
            case NightPage.Story:
                DrawStory(frame, area);
                break;
            case NightPage.StoryComments:
                DrawStoryComments(frame, area);
                break;
            case NightPage.PostComments:
                DrawPostComments(frame, area);
                break;
            case NightPage.Club:
                DrawClubPage(frame, area, state.ClubKey, state.Night);
                break;
            case NightPage.ClubCreate:
                DrawClubCreate(frame, area);
                break;
            case NightPage.ClubEvent:
                DrawClubEvent(frame, area);
                break;
            case NightPage.StoryCompose:
                DrawStoryCompose(frame, area);
                break;
            case NightPage.Following:
                DrawPeopleList(frame, area, "Following", connected: true);
                break;
            case NightPage.Followers:
                DrawPeopleList(frame, area, "Followers", connected: false);
                break;
            case NightPage.PhotoPick:
                DrawPhotoPick(frame, area);
                break;
            case NightPage.PlacePhoto:
                DrawPlacePhoto(frame, area);
                break;
            case NightPage.PhotoView:
                DrawPhotoView(frame, area);
                break;
            case NightPage.ShareSend:
                DrawShareSend(frame, area);
                break;
            case NightPage.Inbox:
                DrawMessages(frame, area);
                break;
            case NightPage.Alerts:
                DrawAlerts(frame, area);
                break;
            case NightPage.Saves:
                DrawSaves(frame, area);
                break;
            case NightPage.LaneSurvey:
                DrawLaneSurvey(frame, area);
                break;
            default:
                DrawBlocked(frame, area);
                break;
        }
    }

    private void DrawSearch(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Search", night))
        {
            state.Back();
            return;
        }

        state.Search = frame.TextField.Draw("ad-search", stack.Take(frame.Units(36f)), state.Search,
            "People, posts, handles");
        if (state.Search.Length >= 2)
        {
            pearl.NoteQuery(state.Search);
        }
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "PEOPLE", night);
        var people = 0;
        for (var index = 0; index < state.Roster.Count && people < 4; index++)
        {
            var person = state.Roster[index];
            if (!state.Passes(person))
            {
                continue;
            }

            DrawMayKnow(frame, stack.Take(frame.Units(56f)), person);
            people++;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
        var posts = 0;
        foreach (var wallPost in VisibleFeed(OpenBoard()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            posts++;
        }

        if (people == 0 && posts == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No matches yet.", night);
        }
    }

    private void DrawStory(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        if (!TryStoryPerson(out var person))
        {
            state.Back();
            return;
        }

        var still = person.Id < 0 ? state.StoryMedia : VybeDemo.StoryStill(paths, person.GateId);
        frame.Paint.Fill(area, person.Wash with { W = 1f }, frame.Units(16f));
        if (still.Length > 0)
        {
            DrawStill(frame, area, still, true);
        }

        frame.Paint.FillGradient(area.TopSlice(frame.Units(120f)), new Vector4(0f, 0f, 0f, 0.58f),
            new Vector4(0f, 0f, 0f, 0f), GradientAxis.Vertical);
        frame.Paint.FillGradient(area.BottomSlice(frame.Units(120f)), new Vector4(0f, 0f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 0.72f), GradientAxis.Vertical);

        var post = StoryLinkedPost(person);
        var liked = state.StoryHearts.Contains(person.Id);
        var likes = StoryLikeCount(person);
        var comments = state.StoryCommentsFor(person.Id).Count;
        var kept = post is { } savedPost
            ? state.KeptPosts.Contains(savedPost.Id)
            : still.Length > 0 && state.KeptShots.Contains(still);
        var reposted = StoryAlreadyReposted(person);
        var showMore = true;

        DrawStoryProgress(frame, area.TopSlice(frame.Units(10f)).Inset(new Edges(frame.Units(10f), frame.Units(6f),
            frame.Units(10f), 0f)));

        var head = area.Inset(new Edges(frame.Units(12f), frame.Units(16f), frame.Units(12f), 0f))
            .TopSlice(frame.Units(36f));
        var close = head.RightSlice(frame.Units(32f));
        frame.Text.DrawIn(close, "✕", new TextStyle(FontRole.Title, Vector4.One, TextAlign.Center));
        if (frame.Input.ConsumeClick(close))
        {
            state.Back();
            return;
        }

        var faceR = frame.Units(14f);
        var faceHit = head.LeftSlice(faceR * 2.2f);
        DrawFace(frame, faceHit.Center, faceR, person.AvatarUrl, person.Wash, true);
        var following = person.Id >= 0 && state.Connected.Contains(person.Id);
        if (person.Id >= 0 && !following)
        {
            var plus = Rect.FromSize(faceHit.Center + new Vector2(faceR * 0.35f, faceR * 0.35f),
                new Vector2(frame.Units(14f), frame.Units(14f)));
            frame.Paint.FillCircle(plus.Center, frame.Units(6f), VybeChrome.Night.Accent);
            frame.Text.DrawIn(plus, "+", new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
            if (frame.Input.ConsumeClick(plus))
            {
                state.FollowPerson(person, pearl, true);
                state.Save(paths);
                return;
            }
        }

        var who = head.Inset(new Edges(faceR * 2.4f, 0f, frame.Units(40f), 0f));
        frame.Text.DrawEllipsized(who.TopSlice(frame.Units(18f)), person.Name,
            new TextStyle(FontRole.CaptionStrong, Vector4.One));
        frame.Text.DrawEllipsized(who.BottomSlice(frame.Units(16f)),
            person.Handle.Length > 0 ? person.Handle : person.World,
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.72f)));
        if (frame.Input.ConsumeClick(who) || frame.Input.ConsumeClick(faceHit))
        {
            if (person.Id >= 0)
            {
                OpenPerson(person.GateId);
            }
            else
            {
                state.Back();
            }

            return;
        }

        var copy = area.BottomSlice(frame.Units(86f)).Inset(new Edges(frame.Units(14f), 0f, frame.Units(58f),
            frame.Units(6f)));
        var handle = person.Handle.Length > 0
            ? (person.Handle.StartsWith('@') ? person.Handle : "@" + person.Handle)
            : person.Name;
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(16f)), handle,
            new TextStyle(FontRole.CaptionStrong, Vector4.One));
        frame.Text.DrawWrapped(copy.Inset(new Edges(0f, frame.Units(18f), 0f, frame.Units(18f))),
            person.Line.Length > 0 ? person.Line : "No caption yet.",
            new TextStyle(FontRole.Caption, Vector4.One));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            StoryPlace(person) + " · " + StoryWhen(person),
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.72f)));

        var slotH = frame.Units(46f);
        var railW = frame.Units(48f);
        var slots = 4 + (showMore ? 1 : 0);
        var railH = slotH * slots;
        var rail = Rect.FromSize(
            new Vector2(area.Max.X - railW - frame.Units(6f), area.Max.Y - frame.Units(8f) - railH),
            new Vector2(railW, railH));
        var slotY = rail.Min.Y;
        Rect NextSlot()
        {
            var hit = Rect.FromSize(new Vector2(rail.Min.X, slotY), new Vector2(rail.Width, slotH));
            slotY += slotH;
            return hit;
        }

        if (showMore && VybeChrome.StoryAction(frame, NextSlot(), "···", "More", state.StoryMoreOpen))
        {
            state.StoryMoreOpen = !state.StoryMoreOpen;
            return;
        }

        if (VybeChrome.StoryGlyphAction(frame, NextSlot(), VybeChrome.LikeGlyph, CompactCount(likes), liked, "♥"))
        {
            LikeStory(person);
            return;
        }

        if (VybeChrome.StoryGlyphAction(frame, NextSlot(), VybeChrome.CommentGlyph, CompactCount(comments), false,
                "💬"))
        {
            state.CommentDraft = string.Empty;
            state.Scroll = 0f;
            state.Open(NightPage.StoryComments);
            return;
        }

        if (VybeChrome.StoryGlyphAction(frame, NextSlot(), VybeChrome.RepostGlyph,
                CompactCount(Math.Max(reposted ? 1 : 0, post?.Reposts ?? 0)),
                reposted || post is { Reposted: true }, "↻"))
        {
            RepostStory(person, still, post);
            return;
        }

        if (VybeChrome.StoryGlyphAction(frame, NextSlot(), VybeChrome.SaveGlyph, kept ? "1" : "Save", kept, "⇩"))
        {
            SaveStory(still, post);
            return;
        }

        if (state.StoryMoreOpen)
        {
            DrawStoryMore(frame, area, person);
            return;
        }

        var tap = new Rect(new Vector2(area.Min.X, head.Max.Y + frame.Units(8f)),
            new Vector2(rail.Min.X, copy.Min.Y - frame.Units(4f)));
        if (frame.Input.ConsumeClick(tap.RightSlice(tap.Width * 0.55f)))
        {
            StepStory(1);
            return;
        }

        if (frame.Input.ConsumeClick(tap.LeftSlice(tap.Width * 0.45f)))
        {
            StepStory(-1);
        }
    }

    private void DrawStoryMore(in AppletFrame frame, Rect area, in ScenePerson person)
    {
        var night = state.Night;
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.52f));
        var rows = (person.Id >= 0 ? 1 : 0) + (person.Id < 0 ? 1 : 0) + 1;
        var plate = area.Inset(new Edges(frame.Units(10f), 0f, frame.Units(10f), frame.Units(10f)))
            .BottomSlice(frame.Units(16f) + frame.Units(44f) * rows);
        VybeChrome.PostSheet(frame, plate);
        var stack = new Stack(plate.Inset(new Edges(frame.Units(12f), frame.Units(10f))), StackAxis.Vertical,
            frame.Units(8f));
        if (person.Id >= 0)
        {
            var report = stack.Take(frame.Units(40f));
            VybeChrome.Primary(frame, report, "Report", night);
            if (frame.Input.ConsumeClick(report))
            {
                state.StoryMoreOpen = false;
                OpenStaffReport("vybe_story",
                    person.GateId.Length > 0 ? person.GateId : person.Id.ToString(CultureInfo.InvariantCulture),
                    person.Name + " story");
                return;
            }
        }

        if (person.Id < 0)
        {
            var remove = stack.Take(frame.Units(40f));
            VybeChrome.Ghost(frame, remove, "Remove story", night);
            if (frame.Input.ConsumeClick(remove))
            {
                state.ClearOwnStory();
                state.Save(paths);
                state.Back();
                return;
            }
        }

        var cancel = stack.Take(frame.Units(40f));
        VybeChrome.Ghost(frame, cancel, "Cancel", night);
        if (frame.Input.ConsumeClick(cancel) ||
            (!plate.Contains(frame.Input.Pointer) && frame.Input.ConsumeClick(area)))
        {
            state.StoryMoreOpen = false;
        }
    }

    private void DrawStoryComments(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        if (!TryStoryPerson(out var person))
        {
            state.Back();
            return;
        }

        var still = person.Id < 0 ? state.StoryMedia : VybeDemo.StoryStill(paths, person.GateId);
        frame.Paint.Fill(area, person.Wash with { W = 1f }, frame.Units(16f));
        if (still.Length > 0)
        {
            DrawStill(frame, area, still, true);
        }

        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.42f), frame.Units(16f));
        var sheet = area.BottomSlice(area.Height * 0.78f);
        frame.Paint.Fill(sheet, new Vector4(0.06f, 0.06f, 0.07f, 0.97f), frame.Units(18f));
        frame.Paint.Stroke(sheet, new Vector4(1f, 1f, 1f, 0.08f), frame.Units(1f), frame.Units(18f));

        var peek = new Rect(area.Min, new Vector2(area.Max.X, sheet.Min.Y));
        if (frame.Input.ConsumeClick(peek))
        {
            state.Back();
            return;
        }

        var head = sheet.TopSlice(frame.Units(48f)).Inset(new Edges(frame.Units(12f), frame.Units(10f),
            frame.Units(12f), 0f));
        var close = head.RightSlice(frame.Units(32f));
        frame.Text.DrawIn(close, "✕", new TextStyle(FontRole.Title, Vector4.One, TextAlign.Center));
        if (VybeChrome.Back(frame, head.Inset(new Edges(0f, 0f, frame.Units(36f), 0f)), "Comments", night) ||
            frame.Input.ConsumeClick(close))
        {
            state.Back();
            return;
        }

        var lines = state.StoryCommentsFor(person.Id);
        DrawCommentThread(frame, sheet, head, lines, "ad-story-comment", night, () =>
        {
            state.AddStoryComment(person.Id, ProfileName(), state.CommentDraft.Trim());
            state.Save(paths);
        });
        _ = tone;
    }

    private void DrawPostComments(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        pearl.WatchPost(state.PostKey);
        frame.Paint.Fill(area, new Vector4(0.04f, 0.04f, 0.045f, 1f));
        var sheet = area.Inset(new Edges(frame.Units(14f), frame.Units(8f), frame.Units(14f), frame.Units(10f)));
        var head = sheet.TopSlice(frame.Units(40f));
        var close = head.RightSlice(frame.Units(32f));
        frame.Text.DrawIn(close, "×", new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
        var back = head.Inset(new Edges(0f, 0f, frame.Units(36f), 0f));
        frame.Text.DrawIn(back.LeftSlice(frame.Units(22f)), "‹",
            new TextStyle(FontRole.Title, tone.Accent, TextAlign.Center));
        frame.Text.DrawEllipsized(back.Inset(new Edges(frame.Units(26f), 0f, 0f, 0f)), "Comments",
            new TextStyle(FontRole.Title, tone.Ink));
        if (frame.Input.ConsumeClick(close) || frame.Input.ConsumeClick(back.LeftSlice(frame.Units(120f))))
        {
            state.Back();
            return;
        }

        var lines = LivePostComments(state.PostKey);
        DrawCommentThread(frame, sheet, head, lines, "ad-post-comment", night, () =>
        {
            var text = state.CommentDraft.Trim();
            state.AddPostComment(state.PostKey, ProfileName(), text);
            pearl.CommentOn(state.PostKey, text);
            state.Save(paths);
        }, BoardPost(state.PostKey));
    }

    private void DrawCommentThread(in AppletFrame frame, Rect sheet, Rect head, IReadOnlyList<PearlComment> lines,
        string fieldId, bool night, Action sent, PearlPost? post = null)
    {
        var composer = sheet.BottomSlice(frame.Units(62f));
        var list = new Rect(new Vector2(sheet.Min.X, head.Max.Y + frame.Units(6f)),
            new Vector2(sheet.Max.X, composer.Min.Y - frame.Units(10f)));
        var gap = frame.Units(10f);
        var peekH = 0f;
        if (post is { } shown)
        {
            peekH = CommentPostHeight(frame, shown, list.Width);
        }

        var content = peekH;
        if (lines.Count == 0)
        {
            content += frame.Units(48f);
        }
        else
        {
            for (var index = 0; index < lines.Count; index++)
            {
                content += CommentRowHeight(frame, lines[index], list.Width) + gap;
            }
        }

        VybeChrome.Wheel(frame, list, state, content + list.Height * 0.12f);
        frame.Paint.PushClip(list);
        var y = list.Min.Y - state.Scroll;
        if (post is { } peek)
        {
            var card = Rect.FromSize(new Vector2(list.Min.X, y), new Vector2(list.Width, peekH));
            if (card.Max.Y > list.Min.Y && card.Min.Y < list.Max.Y)
            {
                DrawCommentPost(frame, card, peek, night);
            }

            y += peekH + gap;
        }

        if (lines.Count == 0)
        {
            var empty = Rect.FromSize(new Vector2(list.Min.X, y), new Vector2(list.Width, frame.Units(36f)));
            if (empty.Max.Y > list.Min.Y && empty.Min.Y < list.Max.Y)
            {
                VybeChrome.Mute(frame, empty, "No comments yet. Be the first.", night);
            }
        }
        else
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var rowH = CommentRowHeight(frame, lines[index], list.Width);
                var row = Rect.FromSize(new Vector2(list.Min.X, y), new Vector2(list.Width, rowH));
                if (row.Max.Y > list.Min.Y && row.Min.Y < list.Max.Y)
                {
                    DrawCommentCard(frame, row, lines[index], night);
                }

                y += rowH + gap;
            }
        }

        frame.Paint.PopClip();
        DrawCommentComposer(frame, composer, fieldId, night, sent);
    }

    private float CommentPostHeight(in AppletFrame frame, PearlPost post, float width)
    {
        var pad = frame.Units(12f);
        var wrap = MathF.Max(1f, width - pad * 2f);
        var caption = CardCaption(post);
        var textH = caption.Length > 0
            ? frame.Text.MeasureWrapped(caption, FontRole.Caption, wrap).Y
            : 0f;
        var mediaH = post.Media.Length > 0 ? MathF.Min(frame.Units(168f), width * 0.62f) : 0f;
        return frame.Units(46f) + textH + mediaH + frame.Units(16f);
    }

    private void DrawCommentPost(in AppletFrame frame, Rect area, PearlPost post, bool night)
    {
        var tone = VybeChrome.Tone(night);
        VybeChrome.PostSheet(frame, area, flush: true);
        var inner = area.Inset(new Edges(frame.Units(12f), frame.Units(10f)));
        var wash = tone.AccentDim;
        if (state.TryFindGate(post.AuthorId, out var author))
        {
            wash = author.Wash;
        }

        var faceR = frame.Units(12f);
        DrawFace(frame, new Vector2(inner.Min.X + faceR, inner.Min.Y + faceR), faceR, post.AuthorAvatarUrl, wash,
            night);
        var head = inner.Inset(new Edges(faceR * 2.2f, 0f, 0f, 0f)).TopSlice(frame.Units(28f));
        var who = post.AuthorHandle.Length > 0
            ? post.AuthorName + "  " + (post.AuthorHandle.StartsWith('@') ? post.AuthorHandle : "@" + post.AuthorHandle)
            : post.AuthorName;
        frame.Text.DrawEllipsized(head.TopSlice(frame.Units(16f)), who,
            new TextStyle(FontRole.CaptionStrong, tone.Ink));
        frame.Text.DrawEllipsized(head.BottomSlice(frame.Units(12f)), post.When,
            new TextStyle(FontRole.Caption, tone.Mute));
        var rest = inner.Inset(new Edges(0f, frame.Units(32f), 0f, 0f));
        var caption = CardCaption(post);
        if (caption.Length > 0)
        {
            var captionH = frame.Text.MeasureWrapped(caption, FontRole.Caption, rest.Width).Y;
            frame.Text.DrawWrapped(rest.TopSlice(captionH), caption, new TextStyle(FontRole.Caption, tone.Ink));
            rest = rest.Inset(new Edges(0f, captionH + frame.Units(6f), 0f, 0f));
        }

        if (post.Media.Length > 0 && rest.Height > frame.Units(24f))
        {
            DrawStill(frame, rest, post.Media[0].Url, night);
        }
    }

    private float CommentRowHeight(in AppletFrame frame, PearlComment comment, float width)
    {
        var pad = frame.Units(12f);
        var face = frame.Units(16f) * 2.6f;
        var wrap = MathF.Max(1f, width - face - pad);
        var bodyH = frame.Text.MeasureWrapped(comment.Body ?? string.Empty, FontRole.Caption, wrap).Y;
        return MathF.Max(frame.Units(62f), frame.Units(36f) + bodyH);
    }

    private void DrawCommentCard(in AppletFrame frame, Rect area, PearlComment comment, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, new Vector4(0.10f, 0.10f, 0.11f, 1f), frame.Units(18f));
        var faceR = frame.Units(16f);
        var face = area.LeftSlice(faceR * 2.5f);
        ResolveStoryCommentFace(comment, out var url, out var wash);
        DrawFace(frame, face.Center, faceR, url, wash, night,
            comment.Mine ? state.ProfileFacePath : string.Empty);
        var copy = area.Inset(new Edges(faceR * 2.6f, frame.Units(10f), frame.Units(36f), frame.Units(10f)));
        var who = copy.TopSlice(frame.Units(16f));
        frame.Text.DrawEllipsized(who.LeftSlice(who.Width * 0.70f), comment.Author,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        frame.Text.DrawEllipsized(who.RightSlice(who.Width * 0.30f), comment.When,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Right));
        frame.Text.DrawWrapped(copy.Inset(new Edges(0f, frame.Units(18f), 0f, 0f)), comment.Body,
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.86f)));
        if (!comment.Mine)
        {
            var flag = area.RightSlice(frame.Units(28f)).TopSlice(frame.Units(28f));
            VybeChrome.ReportFlag(frame, flag, tone.Mute);
            if (frame.Input.ConsumeClick(flag.Expand(frame.Units(6f))))
            {
                OpenStaffReport("vybe_comment",
                    comment.Id.Length > 0 ? comment.Id : state.PostKey,
                    comment.Author + ": " + comment.Body);
            }
        }
    }

    private void DrawCommentComposer(in AppletFrame frame, Rect area, string fieldId, bool night, Action sent)
    {
        var well = area.Inset(new Edges(0f, frame.Units(6f), 0f, 0f));
        frame.Paint.Fill(well, new Vector4(0.10f, 0.10f, 0.11f, 1f), well.Height * 0.5f);
        var send = well.RightSlice(well.Height).Inset(frame.Units(6f));
        var field = well.Inset(new Edges(frame.Units(14f), frame.Units(6f), well.Height + frame.Units(4f),
            frame.Units(6f)));
        state.CommentDraft = frame.TextField.Draw(fieldId, field, state.CommentDraft, "Reply...");
        var ready = state.CommentDraft.Trim().Length > 0;
        if (ready)
        {
            VybeChrome.WashFill(frame, send, send.Width * 0.5f);
        }
        else
        {
            frame.Paint.Fill(send, VybeChrome.Tone(night).CardHi, send.Width * 0.5f);
        }

        frame.Text.DrawIn(send, "Send",
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center, 1f, 0.86f));
        if (ready && frame.Input.ConsumeClick(send))
        {
            sent();
            state.CommentDraft = string.Empty;
            state.Scroll = float.MaxValue;
        }
    }

    private void ResolveStoryCommentFace(PearlComment comment, out string url, out Vector4 wash)
    {
        wash = VybeChrome.Tone(state.Night).Accent;
        if (comment.Mine)
        {
            url = pearl.Current.MeAvatarUrl;
            return;
        }

        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (string.Equals(person.Name, comment.Author, StringComparison.Ordinal))
            {
                url = person.AvatarUrl;
                wash = person.Wash;
                return;
            }
        }

        url = string.Empty;
    }

    private bool TryStoryPerson(out ScenePerson person)
    {
        if (state.StoryIndex < 0)
        {
            person = new ScenePerson(-1, string.Empty, ProfileName(), state.Handle, "You", state.OwnStory, true, 0,
                false, VybeChrome.Tone(state.Night).Accent, Array.Empty<string>(), Array.Empty<string>(),
                pearl.Current.MeAvatarUrl);
            return state.HasOwnStory;
        }

        return state.TryFind(state.StoryIndex, out person);
    }

    private string StoryPlace(in ScenePerson person)
    {
        if (person.Id < 0)
        {
            if (state.OwnStoryPlace.Length > 0)
            {
                return state.OwnStoryPlace;
            }

            var zone = game.ZoneName.Trim();
            return zone.Length > 0 ? zone : "Location";
        }

        return person.World.Length > 0 ? person.World : "Location";
    }

    private string StoryWhen(in ScenePerson person)
    {
        if (person.Id < 0 && state.OwnStoryAtUnix > 0)
        {
            var posted = DateTimeOffset.FromUnixTimeSeconds(state.OwnStoryAtUnix);
            var stamp = ZoneClock.Stamp(display.OwnTimeZoneId, display.Use24HourClock, posted, posted);
            return stamp.Length > 0 ? stamp : "now";
        }

        var zone = person.TimeZoneId.Length > 0 ? person.TimeZoneId : display.OwnTimeZoneId;
        var clock = ZoneClock.Stamp(zone, display.Use24HourClock);
        return clock.Length > 0 ? clock : "now";
    }

    private void LikeStory(in ScenePerson person)
    {
        if (!state.StoryHearts.Add(person.Id))
        {
            state.StoryHearts.Remove(person.Id);
        }

        if (StoryLinkedPost(person) is { } linked &&
            state.PostHearted(linked) != state.StoryHearts.Contains(person.Id))
        {
            HeartPost(linked.Id, linked.Liked);
        }

        state.Save(paths);
    }

    private int StoryLikeCount(in ScenePerson person)
    {
        var extra = state.StoryHearts.Contains(person.Id) ? 1 : 0;
        if (person.Id < 0)
        {
            return extra;
        }

        return 3 + Math.Abs(person.Id % 17) + extra;
    }

    private string StoryRepostId(in ScenePerson person) =>
        "story-repost-" + (person.Id < 0 ? "me" : person.Id.ToString(CultureInfo.InvariantCulture));

    private bool StoryAlreadyReposted(in ScenePerson person)
    {
        var id = StoryRepostId(person);
        for (var index = 0; index < state.StoryReposts.Count; index++)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void RepostStory(in ScenePerson person, string still, PearlPost? post)
    {
        var id = StoryRepostId(person);
        for (var index = 0; index < state.StoryReposts.Count; index++)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                state.StoryReposts.RemoveAt(index);
                state.Save(paths);
                return;
            }
        }

        var caption = person.Line.Length > 0 ? person.Line : "Reposted a story";
        var media = still.Length > 0
            ? new[] { new PearlMedia(id + "-media", still, 0, 0) }
            : Array.Empty<PearlMedia>();
        state.StoryReposts.Insert(0, new PearlPost(
            id,
            "me",
            ProfileName(),
            state.Handle,
            OwnFacePath().Length > 0 ? OwnFacePath() : pearl.Current.MeAvatarUrl,
            caption,
            "now",
            true,
            false,
            0,
            0,
            1,
            true,
            id,
            person.Name,
            caption,
            media,
            "story-repost"));
        if (post is { } shared)
        {
            pearl.Repost(shared.Id);
        }

        state.Save(paths);
    }

    private void SaveStory(string still, PearlPost? post)
    {
        if (post is { } keep)
        {
            KeepPost(keep);
            return;
        }

        if (still.Length == 0)
        {
            return;
        }

        for (var index = 0; index < state.StorySaves.Count; index++)
        {
            if (state.StorySaves[index].Media.Length > 0 &&
                string.Equals(state.StorySaves[index].Media[0].Url, still, StringComparison.Ordinal))
            {
                KeepPost(state.StorySaves[index]);
                return;
            }
        }

        var id = "story-save-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var saved = new PearlPost(
            id,
            "me",
            ProfileName(),
            state.Handle,
            OwnFacePath().Length > 0 ? OwnFacePath() : pearl.Current.MeAvatarUrl,
            "Saved story",
            "now",
            true,
            false,
            0,
            0,
            0,
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            new[] { new PearlMedia(id + "-media", still, 0, 0) },
            "story-save");
        state.StorySaves.Insert(0, saved);
        KeepPost(saved);
    }

    private PearlPost? StoryLinkedPost(in ScenePerson person)
    {
        var author = person.Id < 0 ? "me" : person.GateId;
        foreach (var post in OpenBoard())
        {
            if ((author.Length > 0 && string.Equals(post.AuthorId, author, StringComparison.Ordinal)) ||
                (person.Id < 0 && post.Mine) ||
                string.Equals(post.AuthorName, person.Name, StringComparison.Ordinal))
            {
                return post;
            }
        }

        return null;
    }

    private void DrawStoryProgress(in AppletFrame frame, Rect area)
    {
        var people = StoryPeople();
        var own = state.HasOwnStory ? 1 : 0;
        var total = Math.Max(1, people.Count + own);
        var current = 0;
        if (state.StoryIndex >= 0)
        {
            current = own;
            for (var index = 0; index < people.Count; index++)
            {
                if (people[index].Id == state.StoryIndex)
                {
                    current = own + index;
                    break;
                }
            }
        }

        var gap = frame.Units(3f);
        var wide = (area.Width - gap * Math.Max(0, total - 1)) / total;
        for (var index = 0; index < total; index++)
        {
            var tick = Rect.FromSize(new Vector2(area.Min.X + (wide + gap) * index, area.Min.Y),
                new Vector2(wide, area.Height));
            frame.Paint.Fill(tick, index <= current ? Vector4.One : new Vector4(1f, 1f, 1f, 0.28f),
                tick.Height * 0.5f);
        }
    }

    private void StepStory(int step)
    {
        var people = StoryPeople();
        if (people.Count == 0)
        {
            state.Back();
            return;
        }

        var index = -1;
        for (var i = 0; i < people.Count; i++)
        {
            if (people[i].Id == state.StoryIndex)
            {
                index = i;
                break;
            }
        }

        var next = state.StoryIndex < 0 ? (step > 0 ? 0 : people.Count - 1) : index + step;
        if (next < 0 || next >= people.Count)
        {
            state.Back();
            return;
        }

        state.StoryIndex = people[next].Id;
        state.ViewedStories.Add(people[next].Id);
        state.StoryMoreOpen = false;
        state.Save(paths);
    }

    private void DrawStoryCompose(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Your story", night))
        {
            state.Back();
            return;
        }

        var story = stack.Take(frame.Units(96f));
        state.Caption = frame.TextField.Write("ad-story", VybeChrome.FieldWell(frame, story, night), state.Caption,
            "What are you up to?", 400);
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(32f)),
                state.StoryMedia.Length > 0 ? "Photo attached" : "Add photo", state.StoryMedia.Length > 0, night))
        {
            OpenComposePhoto();
        }

        VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
            "Deletes after 24 hours, including comments.", night);

        var post = stack.Take(frame.Units(44f));
        var hasStory = state.Caption.Trim().Length > 0 || state.StoryMedia.Length > 0;
        VybeChrome.Primary(frame, post, hasStory ? "Share to stories" : "Remove story", night);
        if (frame.Input.ConsumeClick(post))
        {
            if (hasStory)
            {
                CommitStory();
                return;
            }

            state.ClearOwnStory();
            state.Page = NightPage.Tabs;
            state.Save(paths);
        }
    }

    private void DrawPerson(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        if (!state.TryFind(state.PersonIndex, out var person))
        {
            state.Back();
            return;
        }

        var tone = VybeChrome.Tone(night);
        var hero = DrawProfileHero(frame, area, night, string.Empty, person.Wash with { W = 0.92f },
            person.AvatarUrl, person.AvatarUrl, person.Wash, own: false, back: true);
        if (hero.Back)
        {
            state.Back();
            return;
        }

        if (hero.Block)
        {
            state.BlockPerson(person);
            state.Save(paths);
            state.Back();
            return;
        }

        if (hero.Flag && !state.ReportOpen)
        {
            OpenProfileReport(person.GateId.Length > 0 ? person.GateId : person.Id.ToString(CultureInfo.InvariantCulture),
                person.Name);
        }

        var pad = frame.Units(14f);
        var stack = new Stack(
            new Rect(new Vector2(area.Min.X + pad, hero.InfoTop), new Vector2(area.Max.X - pad, area.Max.Y)),
            StackAxis.Vertical, frame.Units(6f));
        var status = person.Online ? "Online" : "Away";
        var meta = person.World.Length > 0 ? person.World + " · " + status : status;
        var zone = WorldZones.ForPerson(person.TimeZoneId, person.GateId);
        DrawProfileIdentity(frame, ref stack, person.Name, person.Handle, meta, person.Line, night,
            VybeDemo.HasPlusAccount(person),
            zone.Length > 0 ? ZoneClock.Line(zone, display.Use24HourClock) : string.Empty);
        DrawPersonFacts(frame, ref stack, person, night);

        var linked = state.Connected.Contains(person.Id);
        var acts = DrawProfileActions(frame, ref stack, linked, PersonAllowsContact(person), night);
        if (acts.Follow)
        {
            state.FollowPerson(person, pearl, !linked);
        }

        if (acts.Contact)
        {
            state.ChatIndex = person.Id;
            state.Open(NightPage.Chat);
        }
        if (person.GateId.Length > 0)
        {
            pearl.WatchProfile(person.GateId);
        }

        DrawProfileShelf(frame, ref stack, PersonBoard(person.GateId), own: false, night);
    }

    private void DrawPersonFacts(in AppletFrame frame, ref Stack stack, ScenePerson person, bool night)
    {
        var gender = person.Gender;
        var sexuality = person.Sexuality;
        var relationship = person.Relationship;
        var dms = person.DmsOpen;
        var intent = JoinShown(person.Intents, night);
        var race = person.Race;
        if (TryPeopleCard(person, out var card))
        {
            if (gender.Length == 0)
            {
                gender = card.Gender;
            }

            if (sexuality.Length == 0)
            {
                sexuality = card.Sexuality;
            }

            if (relationship.Length == 0)
            {
                relationship = card.Relationship;
            }

            if (race.Length == 0)
            {
                race = card.Race;
            }

            dms ??= card.DmsOpen;
            if (intent.Length == 0)
            {
                intent = JoinShown(card.LookingFor, night);
            }
        }

        DrawProfileFacts(frame, ref stack, gender, sexuality, dms, intent, relationship, night, race);
        if (night && TryPeopleCard(person, out var pulse) && VybeLaneMap.Ready(pulse.Pulse))
        {
            DrawLaneFold(frame, ref stack, pulse.Pulse, night, pulse.GateId.Length > 0 ? pulse.GateId : pulse.Id.ToString(CultureInfo.InvariantCulture));
        }
    }

    private bool TryPeopleCard(ScenePerson person, out PeopleCard card)
    {
        if (findDeck.Length == 0)
        {
            findDeck = PeopleFindBook.Deck(paths);
        }

        for (var index = 0; index < findDeck.Length; index++)
        {
            var hit = findDeck[index];
            if (hit.Id == person.Id ||
                string.Equals(hit.GateId, person.GateId, StringComparison.Ordinal))
            {
                card = hit;
                return true;
            }
        }

        card = default;
        return false;
    }

    private bool PersonAllowsContact(ScenePerson person)
    {
        if (person.DmsOpen is { } open)
        {
            return open;
        }

        return !TryPeopleCard(person, out var card) || card.DmsOpen;
    }

    private void DrawPost(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        pearl.WatchPost(state.PostKey);
        var post = BoardPost(state.PostKey);
        if (post is null)
        {
            VybeChrome.Mute(frame, area.TopSlice(frame.Units(40f)), "That post is not on Pearlgate yet.", night);
            if (VybeChrome.Back(frame, area.TopSlice(frame.Units(28f)), "Post", night))
            {
                state.Back();
            }

            return;
        }

        var ready = post.Value;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Post", night))
        {
            state.Back();
            return;
        }

        DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, ready))), ready);
        var open = stack.Take(frame.Units(40f));
        VybeChrome.Primary(frame, open, "View comments", night);
        if (frame.Input.ConsumeClick(open))
        {
            OpenPostComments(ready.Id);
        }
    }

    private void DrawCompose(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var plus = state.DraftPlus;
        var story = state.DraftStory;
        frame.Paint.Fill(area, new Vector4(0.045f, 0.045f, 0.05f, 1f), frame.Units(16f));
        var pad = frame.Units(12f);
        var sheet = area.Inset(new Edges(pad, frame.Units(6f), pad, frame.Units(6f)));
        var head = sheet.TopSlice(frame.Units(34f));
        var foot = sheet.BottomSlice(frame.Units(52f));
        var pane = new Rect(new Vector2(sheet.Min.X, head.Max.Y + frame.Units(8f)),
            new Vector2(sheet.Max.X, foot.Min.Y - frame.Units(8f)));

        var live = state.DraftSheet == ComposeSheet.None;
        var cancel = head.LeftSlice(frame.Units(58f));
        frame.Text.DrawIn(cancel, "Cancel", new TextStyle(FontRole.Caption, Vector4.One));
        frame.Text.DrawIn(head, "Create Post",
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        if (live && frame.Input.ConsumeClick(cancel))
        {
            state.Back();
            return;
        }

        var quoted = state.QuoteOf.Length > 0 ? BoardPost(state.QuoteOf) : null;
        if (quoted is { } plusQuote && VybeDemo.IsPlusPost(plusQuote))
        {
            state.DraftPlus = CanPostPlus();
            state.DraftStory = false;
            plus = state.DraftPlus;
            story = false;
        }

        var quotingPlus = quoted is { } locked && VybeDemo.IsPlusPost(locked);
        var offerPlus = CanPostPlus();
        var hasContent = state.Caption.Length > 0 || state.DraftMedia.Count > 0 ||
                         (!story && state.QuoteOf.Length > 0);
        var tagged = story || VybePostTags.HasAny(state.DraftTags);
        var rated = story || !plus || VybePostMark.PlusRatingPicked(state.DraftRating);
        var ready = hasContent && tagged && rated && (story || !plus || offerPlus);
        var postLabel = story ? "Share Story" : plus ? "Post to VYBE+" : "Post to VYBE";
        if (VybeChrome.ComposeSend(frame, foot.Inset(new Edges(0f, frame.Units(6f))), postLabel, ready, plus, live))
        {
            state.DraftSfwOk = false;
            state.DraftSheet = story
                ? ComposeSheet.ConfirmStory
                : plus ? ComposeSheet.ConfirmPlus : ComposeSheet.ConfirmVybe;
        }

        var hashes = VybePostTags.Lane(plus);
        var hashH = VybePostTags.WrapHeight(frame, pane, hashes);
        var descH = state.DraftDescOpen
            ? VybePostTags.WrapHeight(frame, pane, DescriptorSlugs())
            : 0f;
        frame.Paint.PushClip(pane);
        var stack = new Stack(pane.Translate(new Vector2(0f, -state.Scroll)), StackAxis.Vertical, frame.Units(10f));
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "WHERE ARE YOU POSTING?", night);
        var dest = stack.Take(frame.Units(56f));
        if (quotingPlus)
        {
            VybeChrome.DestCard(frame, dest, "VYBE+", "18+ Community", true, true, live);
        }
        else
        {
            DrawComposeDestinations(frame, dest, offerPlus, plus, story, live);
        }

        if (story)
        {
            state.DraftStoryPermanent = false;
            VybeChrome.StoryBadge(frame, stack.Take(frame.Units(28f)));
            VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
                "Deletes after 24 hours, including comments.",
                night);
        }
        else
        {
            VybeChrome.LaneBadge(frame, stack.Take(frame.Units(28f)), plus);
            if (plus)
            {
                VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
                    "You are posting to the adult VYBE+ community.", night);
            }
        }

        var heroH = state.DraftMedia.Count > 0
            ? ComposeStillHeight(frame, pane.Width, state.DraftMedia[0])
            : frame.Units(110f);
        var hero = stack.Take(heroH);
        if (state.DraftMedia.Count > 0)
        {
            DrawStillWhole(frame, hero, state.DraftMedia[0], night);
            if (live && frame.Input.ConsumeClick(hero))
            {
                frame.Paint.PopClip();
                OpenComposePhoto();
                return;
            }
        }
        else
        {
            frame.Paint.Fill(hero, new Vector4(1f, 1f, 1f, 0.04f), frame.Units(16f));
            frame.Paint.Stroke(hero, new Vector4(1f, 1f, 1f, 0.12f), frame.Units(1.2f), frame.Units(16f));
            frame.Text.DrawIn(hero, "Tap to add a photo",
                new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
            if (live && frame.Input.ConsumeClick(hero))
            {
                frame.Paint.PopClip();
                OpenComposePhoto();
                return;
            }
        }

        if (state.DraftMedia.Count > 1)
        {
            var row = stack.Take(frame.Units(48f));
            var cell = row.Width / Math.Min(4, state.DraftMedia.Count);
            for (var index = 0; index < state.DraftMedia.Count && index < 4; index++)
            {
                var shot = Rect.FromSize(new Vector2(row.Min.X + cell * index + frame.Units(2f), row.Min.Y),
                    new Vector2(cell - frame.Units(4f), row.Height));
                DrawStill(frame, shot, state.DraftMedia[index], night);
            }
        }

        if (state.QuoteOf.Length > 0)
        {
            var quote = stack.Take(frame.Units(48f));
            frame.Paint.Fill(quote.LeftSlice(frame.Units(2.5f)), tone.Accent with { W = 0.85f }, frame.Units(1.2f));
            var copy = quote.Inset(new Edges(frame.Units(10f), frame.Units(4f), 0f, frame.Units(4f)));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)),
                quoted is { } q ? q.AuthorName : "Quote",
                new TextStyle(FontRole.CaptionStrong, Vector4.One));
            VybeChrome.Mute(frame, copy.BottomSlice(frame.Units(18f)),
                quoted is { } bodyQuote ? bodyQuote.Body : state.QuoteOf, night);
        }

        var captionBox = stack.Take(frame.Units(88f));
        var hint = story ? "What are you up to?" : "What's the Vybe?";
        if (live)
        {
            state.Caption = frame.TextField.Write("ad-caption", captionBox, state.Caption, hint, 400);
        }
        else
        {
            frame.Text.DrawWrapped(captionBox, state.Caption.Length > 0 ? state.Caption : hint,
                new TextStyle(FontRole.Body, Vector4.One));
        }

        if (story)
        {
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "YOUR STORY", night);
            VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
                "Add a photo or a line. Anyone who opens your ring can see it.", night);
        }
        else
        {
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "DESCRIBE YOUR POST", night);
        VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
            "Choose at least one hashtag so people can discover your post.", night);
        var bar = stack.Take(frame.Units(34f));
        frame.Paint.Fill(bar, new Vector4(1f, 1f, 1f, 0.05f), bar.Height * 0.5f);
        frame.Paint.Stroke(bar, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1f), bar.Height * 0.5f);
        if (live)
        {
            var typed = frame.TextField.Draw("ad-hashtag", bar.Inset(new Edges(frame.Units(12f), frame.Units(4f))),
                state.DraftTagDraft, "# add another hashtag", 32, out var submitted);
            if (submitted || typed.EndsWith(' ') || typed.EndsWith(','))
            {
                VybePostTags.AbsorbTyped(state.DraftTags, typed);
                state.DraftTagDraft = string.Empty;
            }
            else
            {
                state.DraftTagDraft = typed;
            }
        }
        else
        {
            frame.Text.DrawEllipsized(bar.Inset(new Edges(frame.Units(12f), frame.Units(4f))),
                state.DraftTagDraft.Length > 0 ? state.DraftTagDraft : "# add another hashtag",
                new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.55f)));
        }

        DrawHashWrap(frame, stack.Take(hashH), hashes, night, live);
        if (!tagged)
        {
            VybeChrome.Note(frame, stack.Take(frame.Units(16f)), "Choose at least one hashtag.", night);
        }

        if (plus)
        {
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "CONTENT RATING", night);
            VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
                "Choose the rating that best describes this post.", night);
            for (var index = 0; index < VybePostMark.PlusRatings.Length; index++)
            {
                var row = VybePostMark.PlusRatings[index];
                var cell = stack.Take(frame.Units(44f));
                if (VybeChrome.CheckRow(frame, cell.TopSlice(frame.Units(22f)), row.Title,
                        state.DraftRating == row.Rating, true, live))
                {
                    state.DraftRating = row.Rating;
                }

                frame.Text.DrawEllipsized(cell.BottomSlice(frame.Units(18f)).Inset(new Edges(frame.Units(24f), 0f)),
                    row.Line, new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.62f)));
            }

            if (!rated)
            {
                VybeChrome.Note(frame, stack.Take(frame.Units(16f)),
                    "Choose a content rating before posting to VYBE+.", night);
            }

            var descHead = stack.Take(frame.Units(18f));
            VybeChrome.Kicker(frame, descHead, state.DraftDescOpen ? "CONTENT DESCRIPTORS ▾" : "CONTENT DESCRIPTORS ▸",
                night);
            if (live && frame.Input.ConsumeClick(descHead))
            {
                state.DraftDescOpen = !state.DraftDescOpen;
            }

            if (state.DraftDescOpen)
            {
                VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
                    "Add optional descriptors so viewers know what type of adult content is included.", night);
                DrawDescriptorWrap(frame, stack.Take(MathF.Max(descH, frame.Units(26f))), live);
            }
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "WHO CAN SEE THIS", night);
        var crowd = night ? "Connections" : "Following";
        if (VybeChrome.CheckRow(frame, stack.Take(frame.Units(30f)), "Everyone", state.AudienceEveryone, plus, live))
        {
            state.AudienceEveryone = true;
        }

        if (VybeChrome.CheckRow(frame, stack.Take(frame.Units(30f)), crowd, !state.AudienceEveryone, plus, live))
        {
            state.AudienceEveryone = false;
        }
        }

        stack.Take(frame.Units(8f));
        var content = stack.Remaining.Min.Y - (pane.Min.Y - state.Scroll);
        if (live)
        {
            VybeChrome.Wheel(frame, pane, state, content);
        }

        frame.Paint.PopClip();
        if (!live)
        {
            DrawComposeSheet(frame, area, night);
        }
    }

    private void OpenComposePhoto()
    {
        state.PickingAvatar = false;
        state.PickingBanner = false;
        state.PickingClubFace = false;
        state.Open(NightPage.PhotoPick);
    }

    private void DrawComposeDestinations(in AppletFrame frame, Rect dest, bool offerPlus, bool plus, bool story,
        bool live)
    {
        var gap = frame.Units(6f);
        var count = offerPlus ? 3 : 2;
        var wide = (dest.Width - gap * (count - 1)) / count;
        var vybe = dest.LeftSlice(wide);
        if (VybeChrome.DestCard(frame, vybe, "VYBE", "SFW feed", !plus && !story, false, live) && (plus || story))
        {
            if (plus)
            {
                state.DraftSheet = ComposeSheet.SwitchVybe;
            }
            else
            {
                state.DraftStory = false;
            }
        }

        var rest = dest.Inset(new Edges(wide + gap, 0f, 0f, 0f));
        if (offerPlus)
        {
            var storyHit = rest.LeftSlice(wide);
            var plusHit = rest.RightSlice(wide);
            if (VybeChrome.DestCard(frame, storyHit, "Stories", "24 hours", story, false, live) && !story)
            {
                state.DraftStory = true;
                state.DraftStoryPermanent = false;
                state.DraftPlus = false;
                state.DraftRating = ContentRating.Sfw;
            }

            if (VybeChrome.DestCard(frame, plusHit, "VYBE+", "18+ feed", plus, true, live) && !plus)
            {
                state.DraftStory = false;
                state.DraftPlus = true;
                state.DraftRating = ContentRating.None;
            }

            return;
        }

        if (VybeChrome.DestCard(frame, rest, "Stories", "24 hours", story, false, live) && !story)
        {
            state.DraftStory = true;
            state.DraftStoryPermanent = false;
            state.DraftPlus = false;
        }
    }

    private void DrawHashWrap(in AppletFrame frame, Rect area, string[] options, bool night, bool live = true)
    {
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var x = area.Min.X;
        var y = area.Min.Y;
        for (var index = 0; index < options.Length; index++)
        {
            var shown = options[index];
            var slug = VybePostTags.Normalize(shown);
            var label = VybePostTags.Format(shown);
            var wide = MathF.Min(area.Width, frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > area.Min.X && x + wide > area.Max.X)
            {
                x = area.Min.X;
                y += rowH + gap;
            }

            var cell = Rect.FromSize(new Vector2(x, y), new Vector2(wide, rowH));
            var on = state.DraftTags.Contains(slug, StringComparer.Ordinal);
            if (VybeChrome.OutlineChip(frame, cell, label, on, state.DraftPlus, live) &&
                !state.DraftTags.Remove(slug))
            {
                state.DraftTags.Add(slug);
            }

            x += wide + gap;
        }

        _ = night;
    }

    private static string[] DescriptorSlugs()
    {
        var slugs = new string[VybePostMark.Descriptors.Length];
        for (var index = 0; index < slugs.Length; index++)
        {
            slugs[index] = VybePostTags.Normalize(VybePostMark.Descriptors[index]);
        }

        return slugs;
    }

    private void DrawDescriptorWrap(in AppletFrame frame, Rect area, bool live = true)
    {
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var x = area.Min.X;
        var y = area.Min.Y;
        for (var index = 0; index < VybePostMark.Descriptors.Length; index++)
        {
            var label = VybePostMark.Descriptors[index];
            var wide = MathF.Min(area.Width, frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > area.Min.X && x + wide > area.Max.X)
            {
                x = area.Min.X;
                y += rowH + gap;
            }

            var cell = Rect.FromSize(new Vector2(x, y), new Vector2(wide, rowH));
            var on = state.DraftDescriptors.Contains(label, StringComparer.Ordinal);
            if (VybeChrome.OutlineChip(frame, cell, label, on, true, live) &&
                !state.DraftDescriptors.Remove(label))
            {
                state.DraftDescriptors.Add(label);
            }

            x += wide + gap;
        }
    }

    private void DrawComposeSheet(in AppletFrame frame, Rect area, bool night)
    {
        VybeChrome.ComposeVeil(frame, area);
        var plus = state.DraftSheet == ComposeSheet.ConfirmPlus;
        var story = state.DraftSheet == ComposeSheet.ConfirmStory;
        var switchLane = state.DraftSheet == ComposeSheet.SwitchVybe;
        var sendH = frame.Units(40f);
        var headH = frame.Units(34f);
        var gap = frame.Units(8f);
        var maxH = MathF.Min(area.Height - frame.Units(12f),
            plus ? frame.Units(260f) : story || switchLane ? frame.Units(200f) : frame.Units(240f));
        var plate = area.Inset(new Edges(frame.Units(10f), frame.Units(72f), frame.Units(10f), frame.Units(16f)))
            .BottomSlice(maxH);
        VybeChrome.PostSheet(frame, plate);
        var inner = plate.Inset(new Edges(frame.Units(14f), frame.Units(10f)));
        var head = inner.TopSlice(headH);
        var send = inner.BottomSlice(sendH);
        var body = new Rect(new Vector2(inner.Min.X, head.Max.Y + gap),
            new Vector2(inner.Max.X, send.Min.Y - gap));
        var dismiss = head.LeftSlice(frame.Units(64f));
        frame.Text.DrawIn(dismiss, "Cancel", new TextStyle(FontRole.CaptionStrong, Vector4.One));
        var title = switchLane
            ? "Switch to VYBE?"
            : story ? "Share Story?" : plus ? "Post to VYBE+?" : "Post to VYBE?";
        frame.Text.DrawIn(head, title,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        if (switchLane)
        {
            VybeChrome.Note(frame, body,
                "VYBE only allows SFW content. Make sure this post is appropriate for the general community before publishing.",
                night);
            if (VybeChrome.ComposeSend(frame, send, "Switch to VYBE", true, false) &&
                !frame.Input.IsHovering(dismiss))
            {
                state.DraftPlus = false;
                state.DraftStory = false;
                state.DraftRating = ContentRating.Sfw;
                state.DraftDescriptors.Clear();
                state.DraftSheet = ComposeSheet.None;
                frame.Input.Claim(area);
                return;
            }
        }
        else if (story)
        {
            VybeChrome.Note(frame, body,
                "This goes on your story ring for 24 hours. Comments disappear with it.",
                night);
            if (VybeChrome.ComposeSend(frame, send, "Share Story", true, false) &&
                !frame.Input.IsHovering(dismiss))
            {
                CommitStory();
                frame.Input.Claim(area);
                return;
            }
        }
        else if (state.DraftSheet == ComposeSheet.ConfirmVybe)
        {
            var stack = new Stack(body, StackAxis.Vertical, gap);
            VybeChrome.Note(frame, stack.Take(frame.Units(36f)),
                "VYBE is a SFW community. Confirm this post has no nudity or explicit content.", night);
            DrawComposeSummary(frame, stack.Take(frame.Units(24f)), false, night);
            if (VybeChrome.CheckWrap(frame, stack.TakeRemaining(),
                    "I confirm this post is appropriate for VYBE.", state.DraftSfwOk, false))
            {
                state.DraftSfwOk = !state.DraftSfwOk;
            }

            if (VybeChrome.ComposeSend(frame, send, "Post to VYBE", state.DraftSfwOk, false) &&
                !frame.Input.IsHovering(dismiss))
            {
                CommitPost();
                frame.Input.Claim(area);
                return;
            }
        }
        else
        {
            DrawComposeSummary(frame, body, true, night);
            if (VybeChrome.ComposeSend(frame, send, "Post to VYBE+", true, true) &&
                !frame.Input.IsHovering(dismiss))
            {
                CommitPost();
                frame.Input.Claim(area);
                return;
            }
        }

        if (frame.Input.ConsumeClick(dismiss) || frame.Input.PressedInside(dismiss) ||
            (!frame.Input.IsHovering(plate) && frame.Input.ConsumeClick(area)))
        {
            state.DraftSheet = ComposeSheet.None;
            state.DraftSfwOk = false;
        }

        frame.Input.Claim(area);
    }

    private void DrawComposeSummary(in AppletFrame frame, Rect area, bool plus, bool night)
    {
        var tags = VybePostTags.Join(state.DraftTags);
        var line = plus
            ? "VYBE+  ·  " + VybePostMark.Label(state.DraftRating) +
              (state.DraftDescriptors.Count > 0 ? "  ·  " + string.Join(", ", state.DraftDescriptors) : "") +
              (tags.Length > 0 ? "  ·  " + tags : "")
            : "VYBE  ·  SFW" + (tags.Length > 0 ? "  ·  " + tags : "");
        VybeChrome.Note(frame, area, line, night);
    }

    private void DrawFilters(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        VybeChrome.Wheel(frame, area, state, frame.Units(520f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Filters", night))
        {
            state.Back();
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(32f)),
            "Chips cycle through neutral, include, and exclude.", night);
        foreach (var tag in SceneBook.FilterTags(night))
        {
            var pole = state.Pole(tag);
            var row = stack.Take(frame.Units(32f));
            var on = pole != FilterPole.Neutral;
            VybeChrome.Glow(frame, row, frame.Units(10f), on, night);
            var mark = pole == FilterPole.Include ? "+" : pole == FilterPole.Exclude ? "−" : "·";
            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(12f), 0f)), mark + "  " + tag,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink));
            if (frame.Input.ConsumeClick(row))
            {
                state.CycleFilter(tag);
            }
        }

        var clear = stack.Take(frame.Units(36f));
        VybeChrome.Primary(frame, clear, "Clear all", night);
        if (frame.Input.ConsumeClick(clear))
        {
            state.Filters.Clear();
            state.Vibe = 0;
            state.Hashtag = string.Empty;
            state.Save(paths);
        }
    }

    private void DrawChat(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        if (state.ChatKey.Length > 0)
        {
            DrawPearlChat(frame, area, night);
            return;
        }

        if (!state.TryFind(state.ChatIndex, out var person))
        {
            state.Back();
            return;
        }

        var thread = state.Thread(person.Id);
        talkTray.TickFiles(files, body => PushTalk(thread, body, person));
        if (DrawTalkRoom(frame, area, person.Name, person.Online ? "Active now" : person.World, person.AvatarUrl,
                person.Wash, person.Online, night, thread, "ad-dm", body => PushTalk(thread, body, person),
                () => OpenPerson(person.GateId)))
        {
            return;
        }

        if (state.Draft.Trim().Length == 0)
        {
            return;
        }

        PushTalk(thread, state.Draft.Trim(), person);
        state.Draft = string.Empty;
    }

    private void PushTalk(List<ChatLine> thread, string body, ScenePerson person)
    {
        if (body.Length == 0)
        {
            return;
        }

        var outgoing = marks.Seal(TalkId(), body);
        var when = TalkStamp();
        thread.Add(new ChatLine(true, outgoing, when));
        marks.CatchSent(TalkId(), "me", when, outgoing);
        state.FollowPerson(person, pearl, true);
        state.Save(paths);
        state.Scroll = float.MaxValue;
    }

    private void SendPearl(string body)
    {
        if (body.Length == 0 || state.ChatKey.Length == 0)
        {
            return;
        }

        var outgoing = marks.Seal(TalkId(), body);
        pearl.SendChat(state.ChatKey, outgoing);
        marks.CatchSent(TalkId(), "me", string.Empty, outgoing);
        state.Scroll = float.MaxValue;
    }

    private void DrawPearlChat(in AppletFrame frame, Rect area, bool night)
    {
        pearl.WatchChat(state.ChatKey);
        var title = "Chat";
        var other = "";
        foreach (var chat in pearl.Current.Chats)
        {
            if (!string.Equals(chat.Id, state.ChatKey, StringComparison.Ordinal))
            {
                continue;
            }

            title = chat.Title;
            other = chat.OtherUserId;
            break;
        }

        var face = "";
        if (other.Length > 0 && state.TryFindGate(other, out var person))
        {
            face = person.AvatarUrl;
        }

        var lines = new List<ChatLine>();
        foreach (var line in pearl.LinesFor(state.ChatKey))
        {
            lines.Add(new ChatLine(line.Mine, line.Body, line.When));
        }

        talkTray.TickFiles(files, body =>
        {
            SendPearl(body);
        });
        if (DrawTalkRoom(frame, area, title, other.Length > 0 ? "Pearlgate" : "", face, VybeChrome.Tone(night).Accent,
                false, night, lines, "ad-pearl-dm", body =>
                {
                    SendPearl(body);
                }, other.Length > 0 ? () => OpenPerson(other) : null))
        {
            return;
        }

        if (state.Draft.Trim().Length == 0)
        {
            return;
        }

        SendPearl(state.Draft.Trim());
        state.Draft = string.Empty;
    }

    private bool DrawTalkRoom(in AppletFrame frame, Rect area, string title, string status, string avatar, Vector4 wash,
        bool live, bool night, IReadOnlyList<ChatLine> lines, string fieldId, Action<string> sendBit,
        Action? openProfile = null)
    {
        if (talkLook.Length > 0)
        {
            DrawTalkLook(frame, area);
            return true;
        }

        if (talkAlbum)
        {
            DrawTalkAlbum(frame, area, sendBit);
            return true;
        }

        var tone = VybeChrome.Tone(night);
        var head = area.TopSlice(frame.Units(56f));
        var sheetH = talkTray.SheetHeight(frame);
        var quoteH = marks.ComposerHeight(frame, TalkId());
        var composer = area.BottomSlice(frame.Units(56f) + sheetH + quoteH);
        var quote = quoteH > 0f ? composer.TopSlice(quoteH) : default;
        var rest = quoteH > 0f ? composer.Inset(new Edges(0f, quoteH, 0f, 0f)) : composer;
        var bar = rest.TopSlice(frame.Units(56f));
        var sheet = rest.BottomSlice(sheetH);
        var thread = new Rect(new Vector2(area.Min.X, head.Max.Y + frame.Units(4f)),
            new Vector2(area.Max.X, bar.Min.Y - frame.Units(4f)));

        if (DrawTalkHead(frame, head, title, status, avatar, wash, live, night, openProfile))
        {
            return true;
        }

        DrawTalkThread(frame, thread, title, avatar, wash, night, lines);
        if (quoteH > 0f)
        {
            marks.DrawReply(frame, quote, TalkId(), tone.Ink, tone.Mute, tone.Card);
        }

        var hold = DrawTalkComposer(frame, bar, fieldId, tone, sendBit);
        var fields = frame.TextField;
        talkTray.DrawSheet(frame, sheet, tone.Ink, tone.Mute, tone.Accent, tone.Card, files, TalkGallery(),
            glyph =>
            {
                state.Draft = fields.Insert(fieldId, state.Draft, glyph);
                fields.Focus(fieldId);
            }, sendBit, () =>
            {
                talkAlbum = true;
                talkAlbumLock = true;
                talkTray.Close();
            }, gifs);
        return hold;
    }

    private string TalkGallery()
    {
        var library = PhotoLibrary.Load(paths);
        if (library.GposeFolderReady())
        {
            return library.GposeFolder;
        }

        return paths.State("photos");
    }

    private void DrawTalkAlbum(in AppletFrame frame, Rect area, Action<string> sendBit)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var pick = !talkAlbumLock;
        if (talkAlbumLock && !frame.Input.IsHeld())
        {
            talkAlbumLock = false;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Gallery", night))
        {
            talkAlbum = false;
            talkAlbumLock = false;
            return;
        }

        var shots = GalleryFiles.List(paths);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
                "No photos in Camera yet. Take a still, then pick it here.", night);
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(16f)), "Pick a photo to send", night);
        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(frame.Units(96f));
            var gap = frame.Units(6f);
            var cell = (row.Width - gap * 2f) / 3f;
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + gap) * col, row.Min.Y),
                    new Vector2(cell, cell));
                DrawStill(frame, shot, shots[index + col].Path, night);
                if (!pick || !frame.Input.ConsumeClick(shot))
                {
                    continue;
                }

                sendBit(ChatBits.Pic(shots[index + col].Path));
                talkAlbum = false;
                talkAlbumLock = false;
                return;
            }
        }
    }

    private string TalkLocation()
    {
        var zone = game.ZoneName.Length > 0 ? game.ZoneName : "Unknown zone";
        var world = game.Character.WorldName;
        var map = game.MapCoords;
        var aetheryte = lifestream.NearestAetheryte(game.TerritoryId);
        return ChatBits.Location(zone, world, game.TerritoryId, map.X, map.Y, aetheryte);
    }

    private bool DrawTalkHead(in AppletFrame frame, Rect area, string title, string status, string avatar, Vector4 wash,
        bool live, bool night, Action? openProfile)
    {
        var tone = VybeChrome.Tone(night);
        var back = area.LeftSlice(frame.Units(32f));
        DrawTalkBack(frame, back, tone.Ink);
        if (frame.Input.ConsumeClick(back))
        {
            state.ChatKey = string.Empty;
            state.Back();
            return true;
        }

        var face = area.Inset(new Edges(frame.Units(36f), frame.Units(8f), 0f, frame.Units(8f))).LeftSlice(frame.Units(40f));
        DrawFace(frame, face.Center, frame.Units(16f), avatar, wash, night);
        VybeChrome.LivePip(frame, face.Center, frame.Units(16f), live);

        var copy = area.Inset(new Edges(frame.Units(82f), frame.Units(8f), frame.Units(36f), frame.Units(8f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)), title,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        if (status.Length > 0)
        {
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), status,
                new TextStyle(FontRole.Caption, live ? VybeChrome.Online : tone.Mute));
        }

        if (state.ChatKey.Length > 0)
        {
            var flag = area.RightSlice(frame.Units(28f));
            VybeChrome.ReportFlag(frame, flag, tone.Mute);
            if (frame.Input.ConsumeClick(flag.Expand(frame.Units(6f))))
            {
                OpenStaffReport("chat", state.ChatKey, title);
                return true;
            }
        }

        if (openProfile is not null &&
            (frame.Input.ConsumeClick(face) || frame.Input.ConsumeClick(copy)))
        {
            openProfile();
            return true;
        }

        return false;
    }

    private void DrawTalkThread(in AppletFrame frame, Rect area, string title, string avatar, Vector4 wash, bool night,
        IReadOnlyList<ChatLine> lines)
    {
        var key = state.ChatKey.Length > 0 ? "p:" + state.ChatKey : "l:" + state.ChatIndex.ToString(CultureInfo.InvariantCulture);
        var fresh = !string.Equals(chatAnchor, key, StringComparison.Ordinal);
        if (fresh)
        {
            chatAnchor = key;
            state.Scroll = float.MaxValue;
            frame.TextField.Focus(state.ChatKey.Length > 0 ? "ad-pearl-dm" : "ad-dm");
        }

        if (lines.Count == 0)
        {
            DrawTalkEmpty(frame, area, title, avatar, wash, night);
            return;
        }

        var gap = frame.Units(6f);
        var heights = new float[lines.Count];
        var total = frame.Units(10f);
        for (var index = 0; index < lines.Count; index++)
        {
            var lineKey = TalkLineKey(lines[index], title);
            heights[index] = ChatBits.NamedRowHeight(frame, area.Width, lines[index].Body,
                                 marks.Cite(lineKey, TalkId(), lines[index].Body, lines[index].Mine)) +
                             marks.Band(frame, lineKey) + frame.Units(14f);
            total += heights[index] + gap;
        }

        var maxScroll = MathF.Max(0f, total - area.Height);
        if (fresh || state.Scroll > maxScroll)
        {
            state.Scroll = maxScroll;
        }

        VybeChrome.Wheel(frame, area, state, total);
        frame.Paint.PushClip(area);
        try
        {
            var cursor = area.Min.Y - state.Scroll + frame.Units(8f);
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var row = Rect.FromSize(new Vector2(area.Min.X, cursor), new Vector2(area.Width, heights[index]));
                var grouped = index + 1 < lines.Count && lines[index + 1].Mine == line.Mine;
                DrawTalkBubble(frame, row, line, title, avatar, wash, grouped, night);
                cursor += heights[index] + gap;
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private void DrawTalkEmpty(in AppletFrame frame, Rect area, string title, string avatar, Vector4 wash, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var face = area.Center - new Vector2(0f, frame.Units(18f));
        DrawFace(frame, face, frame.Units(28f), avatar, wash, night);
        frame.Text.DrawIn(Rect.FromSize(new Vector2(area.Min.X, face.Y + frame.Units(36f)),
                new Vector2(area.Width, frame.Units(22f))), title,
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(Rect.FromSize(new Vector2(area.Min.X, face.Y + frame.Units(58f)),
                new Vector2(area.Width, frame.Units(18f))), "Say hello to start the chat",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private string TalkId() =>
        state.ChatKey.Length > 0
            ? "pearl:" + state.ChatKey
            : "vybe:" + state.ChatIndex.ToString(CultureInfo.InvariantCulture);

    private string TalkLineKey(ChatLine line, string title) =>
        ChatMarks.Key(TalkId(), line.Mine ? "me" : title, line.When, line.Body);

    private void DrawTalkBubble(in AppletFrame frame, Rect row, ChatLine line, string title, string avatar, Vector4 wash,
        bool grouped, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var key = TalkLineKey(line, title);
        var band = marks.Band(frame, key);
        var cite = marks.Cite(key, TalkId(), line.Body, line.Mine);
        var bit = ChatBits.Read(line.Body);
        var who = line.Mine ? "You" : title.Length > 0 ? title : "Them";
        var incoming = new Vector4(0.165f, 0.175f, 0.210f, 0.96f);
        var bubbleW = row.Width * 0.78f;
        Rect lane;
        if (line.Mine)
        {
            lane = row.RightSlice(bubbleW);
        }
        else
        {
            var face = row.LeftSlice(frame.Units(28f));
            if (!grouped)
            {
                DrawFace(frame, new Vector2(face.Center.X, row.Max.Y - frame.Units(14f)), frame.Units(10f), avatar,
                    wash, night);
            }

            lane = Rect.FromSize(new Vector2(row.Min.X + frame.Units(32f), row.Min.Y),
                new Vector2(MathF.Min(bubbleW, row.Width - frame.Units(32f)), row.Height));
        }

        var nameRow = lane.TopSlice(frame.Units(14f));
        frame.Text.DrawEllipsized(nameRow, who,
            new TextStyle(FontRole.CaptionStrong, tone.Accent,
                line.Mine ? TextAlign.Right : TextAlign.Left));
        var top = lane.Inset(new Edges(0f, frame.Units(16f), 0f, frame.Units(14f)));
        frame.Paint.Fill(top, line.Mine ? tone.Accent : incoming, frame.Units(16f));
        var copy = top.Inset(new Edges(frame.Units(10f), frame.Units(7f), frame.Units(10f),
            frame.Units(7f) + band));
        frame.Paint.PushClip(copy);
        try
        {
            ChatBits.Draw(frame, copy, line.Body ?? string.Empty,
                line.Mine ? tone.AccentInk : tone.Ink,
                line.Mine ? tone.AccentInk with { W = 0.72f } : tone.Mute, lifestream, gifs, cite);
        }
        finally
        {
            frame.Paint.PopClip();
        }

        if (band > 0f)
        {
            marks.DrawBand(frame, top.BottomSlice(band).Inset(new Edges(frame.Units(8f), 0f)), key);
        }

        frame.Text.DrawIn(lane.BottomSlice(frame.Units(14f)).Inset(new Edges(frame.Units(10f), 0f)),
            ShowTalkWhen(line.When),
            new TextStyle(FontRole.Caption, line.Mine ? tone.AccentInk with { W = 0.72f } : tone.Mute,
                line.Mine ? TextAlign.Right : TextAlign.Left, 1f, 0.85f));
        OpenTalkPic(frame, top, bit);
        OfferTalkMark(frame, lane, key, who, line.Body);
    }

    private void OfferTalkMark(in AppletFrame frame, Rect bubble, string key, string who, string body)
    {
        if (!marks.Busy && frame.Input.ConsumeClick(bubble, PointerButton.Secondary))
        {
            marks.Offer(TalkId(), key, who, ChatBits.Snippet(body ?? string.Empty), frame.Input.Pointer);
        }
    }

    private void OpenTalkPic(in AppletFrame frame, Rect bubble, ChatBit bit)
    {
        if (bit.Kind != ChatBitKind.Pic || bit.Path.Length == 0)
        {
            return;
        }

        if (frame.Input.ConsumeClick(bubble))
        {
            talkLook = bit.Path;
        }
    }

    private void DrawTalkLook(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, tone.Ground);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Photo", night))
        {
            talkLook = string.Empty;
            return;
        }

        var stage = stack.Remaining.Inset(frame.Units(8f));
        if (talkLook.Length == 0 || !File.Exists(talkLook))
        {
            VybeChrome.Mute(frame, stage.TopSlice(frame.Units(36f)), "Photo is gone.", night);
            return;
        }

        var texture = frame.Textures.FromFile(talkLook);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var dest = CoverFit.Contained(texture.Size, stage);
        frame.Paint.ImageRounded(texture, dest, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(12f));
    }

    private bool DrawTalkComposer(in AppletFrame frame, Rect area, string fieldId, NightPalette tone,
        Action<string> sendBit)
    {
        var idle = tone.Mute;
        var slot = frame.Units(32f);
        var gap = frame.Units(2f);
        var mid = area.Center.Y;
        var top = mid - slot * 0.5f;
        Rect Slot(float x) => Rect.FromSize(new Vector2(x, top), new Vector2(slot, slot));
        var plus = Slot(area.Min.X);
        var send = Slot(area.Max.X - slot);
        var faces = Slot(send.Min.X - gap - slot);
        var pin = Slot(faces.Min.X - gap - slot);
        talkTray.DrawPlus(frame, plus, idle);
        if (talkTray.DrawPlace(frame, pin, idle))
        {
            sendBit(TalkLocation());
        }

        talkTray.DrawFaces(frame, faces, idle);
        var field = new Rect(new Vector2(plus.Max.X + gap, top),
            new Vector2(pin.Min.X - gap, top + slot));
        var live = frame.TextField.Owns(fieldId);
        frame.Paint.Fill(field, live
            ? new Vector4(0.14f, 0.14f, 0.17f, 0.98f)
            : new Vector4(0.10f, 0.10f, 0.12f, 0.94f), field.Height * 0.5f);
        if (live)
        {
            frame.Paint.Stroke(field, tone.Ink with { W = 0.55f }, frame.Units(1.2f), field.Height * 0.5f);
        }

        state.Draft = frame.TextField.Draw(fieldId, field, state.Draft, "Message", 400, out var submitted, true);
        var ready = state.Draft.Trim().Length > 0;
        frame.Paint.FillCircle(send.Center, frame.Units(14f), ready ? tone.Accent : tone.CardHi);
        DrawTalkSend(frame.Paint, send.Center, frame.Units(5.5f), ready ? tone.AccentInk : tone.Mute);
        var go = submitted || frame.Input.ConsumeClick(send);
        frame.Input.Claim(area);
        return !go || !ready;
    }

    private static void DrawTalkBack(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = frame.Units(7f);
        var stroke = frame.Units(1.8f);
        frame.Paint.Line(c + new Vector2(s * 0.35f, -s), c + new Vector2(-s * 0.65f, 0f), ink, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.65f, 0f), c + new Vector2(s * 0.35f, s), ink, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.65f, 0f), c + new Vector2(s * 0.85f, 0f), ink, stroke);
    }

    private static void DrawTalkSend(IPaintSurface paint, Vector2 center, float size, Vector4 ink)
    {
        var tip = center + new Vector2(size * 1.15f, 0f);
        var top = center + new Vector2(-size, -size * 0.85f);
        var bot = center + new Vector2(-size, size * 0.85f);
        var mid = center + new Vector2(-size * 0.12f, 0f);
        paint.Line(tip, top, ink, size * 0.28f);
        paint.Line(tip, bot, ink, size * 0.28f);
        paint.Line(top, mid, ink, size * 0.24f);
        paint.Line(bot, mid, ink, size * 0.24f);
    }

    private static string TalkStamp() => DateTimeOffset.Now.ToString("O");

    private static string ShowTalkWhen(string when)
    {
        if (string.IsNullOrEmpty(when) || string.Equals(when, "now", StringComparison.OrdinalIgnoreCase))
        {
            return "Now";
        }

        if (DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
        {
            return at.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        return when;
    }

    private void DrawRequests(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Requests", night))
        {
            state.Back();
            return;
        }

        if (state.Incoming.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                night ? "No requests. Intros you receive land here." : "No follow requests right now.", night);
            return;
        }

        foreach (var id in state.Incoming.ToArray())
        {
            if (!state.TryFind(id, out var person))
            {
                continue;
            }

            if (!night && person.NightOnly)
            {
                continue;
            }

            var row = stack.Take(frame.Units(72f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(8f));
            VybeChrome.Title(frame, inner.TopSlice(frame.Units(20f)),
                person.Name + (night ? " wants to connect" : " followed you"), night);
            var btns = inner.BottomSlice(frame.Units(28f));
            if (VybeChrome.Chip(frame, btns.LeftSlice(btns.Width * 0.48f), "Accept", true, night))
            {
                state.Connected.Add(id);
                state.Incoming.Remove(id);
                state.FollowersSeed++;
                state.Save(paths);
            }

            if (VybeChrome.Chip(frame, btns.RightSlice(btns.Width * 0.48f), "Decline", false, night))
            {
                state.Incoming.Remove(id);
                state.Save(paths);
            }
        }
    }

    private void DrawPlusSwitch(in AppletFrame frame, ref Stack stack, bool night)
    {
        if (state.PlusBlocked)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "VYBE+ NSFW is not available on Lalafell characters.", night);
            return;
        }

        if (!state.PlusAgreed)
        {
            VybeChrome.Note(frame, stack.Take(frame.Units(32f)),
                "Opt in to the 18+ community to unlock VYBE+ on your profile.", night);
            if (VybeChrome.ComposeSend(frame, stack.Take(frame.Units(40f)), "Unlock VYBE+", true, true))
            {
                state.Open(NightPage.Gate);
            }

            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "WHERE ARE YOU BROWSING?", night);
        var dest = stack.Take(frame.Units(56f));
        var gap = frame.Units(8f);
        var half = (dest.Width - gap) * 0.5f;
        if (VybeChrome.DestCard(frame, dest.LeftSlice(half), "VYBE", "SFW Community", !night, false) && night)
        {
            state.StartWash(toNight: false);
        }

        if (VybeChrome.DestCard(frame, dest.RightSlice(half), "VYBE+", "18+ Community", night, true) && !night)
        {
            state.StartWash(toNight: true);
        }

        VybeChrome.LaneBadge(frame, stack.Take(frame.Units(28f)), night);
        VybeChrome.Note(frame, stack.Take(frame.Units(28f)),
            "Turn VYBE+ off in Edit profile.", night);
    }

    private void DrawPlusSettings(in AppletFrame frame, ref Stack stack, bool night)
    {
        if (state.PlusBlocked)
        {
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "VYBE+", night);
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "VYBE+ is not available on Lalafell characters.", night);
            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "VYBE+", night);
        if (!state.PlusAgreed)
        {
            if (state.Onboarded && state.Page == NightPage.OnboardIdentity)
            {
                VybeChrome.Mute(frame, stack.Take(frame.Units(32f)),
                    "Agree to the 18+ terms to add the VYBE / VYBE+ toggle on your profile.", night);
                var unlock = stack.Take(frame.Units(40f));
                VybeChrome.PlusButton(frame, unlock, "Unlock VYBE+", true);
                if (frame.Input.ConsumeClick(unlock))
                {
                    state.Open(NightPage.Gate);
                }

                return;
            }

            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "Opt in from Edit profile after you have an account.", night);
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
            "Turning this off leaves NSFW and removes the toggle from your profile.", night);
        var kill = stack.Take(frame.Units(40f));
        VybeChrome.Plate(frame, kill, frame.Units(12f), night);
        frame.Text.DrawIn(kill, "Turn off VYBE+",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Danger, TextAlign.Center));
        if (frame.Input.ConsumeClick(kill))
        {
            state.RevokePlus();
            state.Save(paths);
        }
    }

    private void DrawLaneEntry(in AppletFrame frame, ref Stack stack, bool night)
    {
        if (!state.PlusAgreed || state.PlusBlocked)
        {
            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ROLE SURVEY", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(48f)),
            "Thirty-two private questions. Each one is first-person and about how you like a scene to feel — lead or follow, sting or soothe, rope, hunt, display, keep. Your map stays on this phone.",
            night);
        var go = stack.Take(frame.Units(40f));
        VybeChrome.Primary(frame, go, state.LaneDone ? "Retake role survey" : "Take role survey", night);
        if (frame.Input.ConsumeClick(go))
        {
            OpenLaneSurvey();
        }

        if (state.LaneDone)
        {
            VybeLaneMap.DrawBars(frame, ref stack, state.LaneMarks, night);
        }
    }

    private void OpenLaneSurvey()
    {
        state.LaneAsk = 0;
        state.LanePicks = new int[VybeLaneMap.AskCount];
        for (var index = 0; index < state.LanePicks.Length; index++)
        {
            state.LanePicks[index] = -1;
        }

        state.Open(NightPage.LaneSurvey);
        laneHold = 0f;
    }

    private void DrawLaneSurvey(in AppletFrame frame, Rect area)
    {
        var night = true;
        var asks = VybeLaneMap.Asks;
        var moved = false;
        if (laneHold > 0f)
        {
            laneHold -= frame.DeltaSeconds;
            if (laneHold <= 0f)
            {
                AdvanceLaneAsk();
                moved = true;
            }
        }

        var done = state.LaneAsk >= asks.Length;
        VybeChrome.Wheel(frame, area, state, frame.Units(done ? 720f : 680f));
        var stack = new Stack(area.Translate(new Vector2(0f, -state.Scroll)), StackAxis.Vertical, frame.Units(10f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Role survey", night))
        {
            laneHold = 0f;
            if (state.LaneAsk > 0 && !done)
            {
                state.LaneAsk--;
                return;
            }

            state.Back();
            return;
        }

        if (done)
        {
            laneHold = 0f;
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "Your map is ready. These percentages show on VYBE+ profiles and feed Discover match.", night);
            VybeLaneMap.DrawBars(frame, ref stack, state.LaneMarks, night);
            var finish = stack.Take(frame.Units(44f));
            VybeChrome.Primary(frame, finish, "Save to profile", night);
            if (frame.Input.ConsumeClick(finish))
            {
                state.Save(paths);
                state.Back();
            }

            return;
        }

        if (state.LanePicks.Length != asks.Length)
        {
            state.LanePicks = new int[asks.Length];
            Array.Fill(state.LanePicks, -1);
        }

        var step = Math.Clamp(state.LaneAsk, 0, asks.Length - 1);
        var tone = VybeChrome.Tone(night);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)),
            (step + 1).ToString(CultureInfo.InvariantCulture) + " / " +
            asks.Length.ToString(CultureInfo.InvariantCulture), night);
        frame.Text.DrawWrapped(stack.Take(frame.Units(88f)), asks[step],
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        for (var index = 0; index < VybeLaneMap.Scale.Length; index++)
        {
            var row = stack.Take(frame.Units(36f));
            var on = state.LanePicks[step] == index;
            var hot = on || frame.Input.IsHovering(row);
            DrawLaneChoice(frame, row, VybeLaneMap.Scale[index], on, hot, tone);
            if (!moved && frame.Input.ConsumeClick(row))
            {
                state.LanePicks[step] = index;
                laneHold = 0.28f;
            }
        }
    }

    private void AdvanceLaneAsk()
    {
        laneHold = 0f;
        var asks = VybeLaneMap.AskCount;
        if (state.LaneAsk + 1 >= asks)
        {
            state.LaneMarks = VybeLaneMap.Score(state.LanePicks);
            state.LaneDone = true;
            state.LaneAsk = asks;
            state.Save(paths);
            return;
        }

        state.LaneAsk++;
    }

    private static void DrawLaneChoice(in AppletFrame frame, Rect area, string label, bool on, bool hot,
        NightPalette tone)
    {
        var radius = frame.Units(12f);
        if (on)
        {
            VybeChrome.WashFill(frame, area, radius);
            frame.Paint.Stroke(area, Vector4.One with { W = 0.92f }, frame.Units(1.6f), radius);
            frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
            return;
        }

        if (hot)
        {
            frame.Paint.Fill(area, tone.Accent with { W = 0.28f }, radius);
            frame.Paint.Stroke(area, tone.Accent with { W = 0.95f }, frame.Units(1.4f), radius);
            frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
            return;
        }

        frame.Paint.Fill(area, tone.CardHi, radius);
        frame.Paint.Stroke(area, tone.Faint, frame.Units(1.2f), radius);
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, tone.Mute, TextAlign.Center));
    }

    private void DrawSettings(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Settings", night))
        {
            state.DropConfirm = false;
            state.Back();
            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ACCOUNT", night);
        var tag = SocialHandle(state.Handle);
        VybeChrome.Mute(frame, stack.Take(frame.Units(22f)),
            tag.Length > 0 ? "Signed in as " + tag : "Signed in to VYBE", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
            "Sign out to log in as someone else or create a new account.", night);
        var leave = stack.Take(frame.Units(44f));
        VybeChrome.Ghost(frame, leave, "Sign out", night);
        if (frame.Input.ConsumeClick(leave))
        {
            SignOutVybe();
            return;
        }

        var drop = stack.Take(frame.Units(40f));
        VybeChrome.Plate(frame, drop, frame.Units(12f), night);
        frame.Text.DrawIn(drop, state.DropConfirm ? "Tap again to delete" : "Delete this account",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Danger, TextAlign.Center));
        if (frame.Input.ConsumeClick(drop))
        {
            DropVybe();
            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "GENERAL", night);
        DrawToggle(frame, stack.Take(frame.Units(44f)), "Discoverable", state.Discoverable, night, () =>
        {
            state.Discoverable = !state.Discoverable;
            state.Save(paths);
        });
        DrawToggle(frame, stack.Take(frame.Units(44f)), "Nearby discovery", state.NearbyDiscovery, night, () =>
        {
            state.NearbyDiscovery = !state.NearbyDiscovery;
            state.Save(paths);
        });
        if (night)
        {
            DrawToggle(frame, stack.Take(frame.Units(44f)), "Dating discovery", state.DatingDiscovery, night, () =>
            {
                state.DatingDiscovery = !state.DatingDiscovery;
                state.Save(paths);
            });
        }

        DrawPlusSwitch(frame, ref stack, night);
        DrawPlusSettings(frame, ref stack, night);
        DrawLaneEntry(frame, ref stack, night);

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "PROFILE", night);
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Edit profile", false, night))
        {
            state.Open(NightPage.OnboardIdentity);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Blocked", false, night))
        {
            state.Open(NightPage.Blocked);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Likes", false, night))
        {
            state.Open(NightPage.Likes);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Gallery", false, night))
        {
            state.Open(NightPage.Gallery);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Following", false, night))
        {
            state.Open(NightPage.Following);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)),
                VybeChrome.StillReady(state.ProfileFacePath) ? "Place photo" : "Add photo", false, night))
        {
            OpenOwnStill(face: true);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)),
                VybeChrome.StillReady(state.ProfileBannerPath) ? "Place banner" : "Add banner", false, night))
        {
            OpenOwnStill(face: false);
        }
    }

    private void DrawGallery(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Gallery", night))
        {
            state.Back();
            return;
        }

        if (night)
        {
            var next = DrawPlusSplit(frame, stack.Take(frame.Units(56f)), "Gallery", state.GalleryPlus, night);
            if (next != state.GalleryPlus)
            {
                state.GalleryPlus = next;
                state.Scroll = 0f;
            }
        }

        DrawGallerySearch(frame, stack.Take(frame.Units(36f)), night);
        DrawGalleryRecs(frame, ref stack, night && state.GalleryPlus, night);
        DrawMasonryGallery(frame, stack.TakeRemaining(), GalleryBoard(), night);
    }

    private void DrawLikes(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Likes", night))
        {
            state.Back();
            return;
        }

        var shown = 0;
        foreach (var wallPost in OpenBoard())
        {
            if (!state.PostHearted(wallPost))
            {
                continue;
            }

            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No likes on this feed yet.", night);
        }
    }

    private void DrawSaves(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Saves", night))
        {
            state.Back();
            return;
        }

        var tabs = stack.Take(frame.Units(32f));
        var postsTab = tabs.LeftSlice(tabs.Width * 0.5f);
        var galleryTab = tabs.RightSlice(tabs.Width * 0.5f);
        if (VybeChrome.Segment(frame, postsTab, "Posts", state.SavePane == 0, night))
        {
            state.SavePane = 0;
            state.Scroll = 0f;
        }

        if (VybeChrome.Segment(frame, galleryTab, "Gallery", state.SavePane == 1, night))
        {
            state.SavePane = 1;
            state.Scroll = 0f;
        }

        if (state.SavePane == 1)
        {
            DrawKeptShotGrid(frame, stack, night);
            return;
        }

        var kept = SavedBoard();
        if (kept.Length == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Save a post and it lands here.", night);
            return;
        }

        foreach (var wallPost in kept)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
        }
    }

    private void DrawKeptShotGrid(in AppletFrame frame, Stack stack, bool night)
    {
        var shots = new List<(string Url, string PostId)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var post in SavedBoard())
        {
            foreach (var url in PostShots(post))
            {
                if (seen.Add(url))
                {
                    shots.Add((url, post.Id));
                }
            }
        }

        foreach (var url in state.KeptShots)
        {
            if (url.Length > 0 && seen.Add(url))
            {
                shots.Add((url, string.Empty));
            }
        }

        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Saved photos land here.", night);
            return;
        }

        var gap = frame.Units(3f);
        var cell = (stack.Remaining.Width - gap * 2f) / 3f;
        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(cell);
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + gap) * col, row.Min.Y), new Vector2(cell, cell));
                DrawStill(frame, shot, shots[index + col].Url, night);
                if (frame.Input.ConsumeClick(shot))
                {
                    state.ViewMedia = shots[index + col].Url;
                    state.PostKey = shots[index + col].PostId;
                    state.Open(NightPage.PhotoView);
                }
            }
        }
    }

    private void DrawPhotoPick(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), PhotoPickTitle(), night))
        {
            state.PickingClubFace = false;
            state.Back();
            return;
        }

        var filesRow = stack.Take(frame.Units(40f));
        VybeChrome.Primary(frame, filesRow, "From files", night);
        if (frame.Input.ConsumeClick(filesRow))
        {
            files.BeginImagePick();
            photoWait = true;
            return;
        }

        var shots = GalleryFiles.List(paths);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
                "Take a still in Camera first, or pick From files.", night);
            return;
        }

        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(frame.Units(86f));
            var cell = (row.Width - frame.Units(8f)) / 3f;
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + frame.Units(4f)) * col, row.Min.Y), new Vector2(cell, cell));
                DrawStill(frame, shot, shots[index + col].Path, night);
                if (!frame.Input.ConsumeClick(shot))
                {
                    continue;
                }

                TakePickedPhoto(shots[index + col].Path);
                return;
            }
        }
    }

    private void OpenOwnStill(bool face)
    {
        state.PickingAvatar = face;
        state.PickingBanner = !face;
        state.PickingClubFace = false;
        var path = face ? state.ProfileFacePath : state.ProfileBannerPath;
        if (!VybeChrome.StillReady(path))
        {
            state.Open(NightPage.PhotoPick);
            return;
        }

        state.Open(NightPage.PlacePhoto);
    }

    private void ClearOwnStill(bool face)
    {
        if (face)
        {
            state.ProfileFacePath = string.Empty;
            state.FaceZoom = 1f;
            state.FaceFocusX = 0.5f;
            state.FaceFocusY = 0.5f;
        }
        else
        {
            state.ProfileBannerPath = string.Empty;
            state.BannerZoom = 1f;
            state.BannerFocusX = 0.5f;
            state.BannerFocusY = 0.5f;
        }

        photoDrag = false;
        state.Save(paths);
    }

    private void ShowPlacePhoto()
    {
        if (state.ReturnTo == NightPage.PlacePhoto)
        {
            state.ReturnTo = NightPage.Tabs;
        }

        state.Page = NightPage.PlacePhoto;
        state.Scroll = 0f;
    }

    private void FinishPhotoPick(in AppletFrame frame)
    {
        if (!photoWait || !files.TryTakeImages(out var picked))
        {
            return;
        }

        photoWait = false;
        if (picked.Count == 0)
        {
            return;
        }

        if (state.PickingAvatar || state.PickingBanner)
        {
            if (ImportOwnStill(frame, picked[0]))
            {
                ShowPlacePhoto();
            }

            return;
        }

        var stored = CopyPickedStill(frame, picked[0],
            state.PickingClubFace ? "vybe-club-face" : "vybe-post");
        if (stored.Length == 0)
        {
            return;
        }

        TakePickedPhoto(stored);
    }

    private string PhotoPickTitle()
    {
        if (state.PickingBanner)
        {
            return "Choose banner";
        }

        if (state.PickingAvatar)
        {
            return "Choose a photo";
        }

        return "Add a photo";
    }

    private void TakePickedPhoto(string path)
    {
        if (state.PickingClubFace)
        {
            state.DraftClubFace = path;
            state.PickingClubFace = false;
            state.Back();
            return;
        }

        if (state.PickingAvatar)
        {
            state.ProfileFacePath = path;
            state.FaceZoom = 1f;
            state.FaceFocusX = 0.5f;
            state.FaceFocusY = 0.5f;
            state.Save(paths);
            ShowPlacePhoto();
            return;
        }

        if (state.PickingBanner)
        {
            state.ProfileBannerPath = path;
            state.BannerZoom = 1f;
            state.BannerFocusX = 0.5f;
            state.BannerFocusY = 0.5f;
            state.Save(paths);
            ShowPlacePhoto();
            return;
        }

        if (state.ReturnTo == NightPage.StoryCompose)
        {
            state.StoryMedia = path;
        }
        else if (state.DraftMedia.Count < 4 && !state.DraftMedia.Contains(path))
        {
            state.DraftMedia.Add(path);
        }

        state.Back();
    }

    private string CopyPickedStill(in AppletFrame frame, string sourcePath, string stem)
    {
        try
        {
            var source = sourcePath.Trim().Trim('"');
            if (source.Length == 0 || !File.Exists(source))
            {
                return string.Empty;
            }

            var ext = Path.GetExtension(source);
            if (!PlateFiles.IsImage(ext))
            {
                ext = ".png";
            }

            var dest = paths.State(stem + "-" + Guid.NewGuid().ToString("N") + ext.ToLowerInvariant());
            Directory.CreateDirectory(paths.StateDirectory);
            File.Copy(source, dest, false);
            frame.Textures.ForgetFile(dest);
            return dest;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private bool ImportOwnStill(in AppletFrame frame, string sourcePath)
    {
        try
        {
            var source = sourcePath.Trim().Trim('"');
            if (source.Length == 0 || !File.Exists(source))
            {
                return false;
            }

            var ext = Path.GetExtension(source);
            if (!PlateFiles.IsImage(ext))
            {
                ext = ".png";
            }

            var stem = state.PickingAvatar ? "afterdark-profile-face" : "afterdark-profile-banner";
            var dest = paths.State(stem + "-" + Guid.NewGuid().ToString("N") + ext.ToLowerInvariant());
            Directory.CreateDirectory(paths.StateDirectory);
            File.Copy(source, dest, false);
            var previous = state.PickingAvatar ? state.ProfileFacePath : state.ProfileBannerPath;
            if (state.PickingAvatar)
            {
                state.ProfileFacePath = dest;
                state.FaceZoom = 1f;
                state.FaceFocusX = 0.5f;
                state.FaceFocusY = 0.5f;
            }
            else
            {
                state.ProfileBannerPath = dest;
                state.BannerZoom = 1f;
                state.BannerFocusX = 0.5f;
                state.BannerFocusY = 0.5f;
            }

            frame.Textures.ForgetFile(previous);
            frame.Textures.ForgetFile(dest);
            if (previous.Length > 0 &&
                !string.Equals(previous, dest, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(previous).StartsWith(stem, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(previous);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            state.Save(paths);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void DrawPlacePhoto(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var face = state.PickingAvatar;
        var path = face ? state.ProfileFacePath : state.ProfileBannerPath;
        var hasStill = VybeChrome.StillReady(path);
        frame.Paint.Fill(area, tone.Ground);
        var inner = area.Inset(frame.Units(16f));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(24f)),
            hasStill ? face ? "Place photo" : "Place banner" : face ? "Add photo" : "Add banner",
            new TextStyle(FontRole.Title, tone.Ink));
        frame.Text.DrawWrapped(inner.Inset(new Edges(0f, frame.Units(28f), 0f, 0f)).TopSlice(frame.Units(36f)),
            hasStill
                ? face
                    ? "Drag to move. Scroll to zoom so the whole photo or just part of it sits in the circle."
                    : "Drag to move. Scroll to zoom so the whole photo or just part of it sits in the banner."
                : "Tap the plus, then pick a photo from your gallery.",
            new TextStyle(FontRole.Caption, tone.Mute));

        var actions = inner.BottomSlice(frame.Units(hasStill ? 88f : 44f));
        var pair = hasStill ? actions.BottomSlice(frame.Units(44f)) : actions;
        var preview = new Rect(inner.Min + new Vector2(0f, frame.Units(72f)),
            new Vector2(inner.Max.X, actions.Min.Y - frame.Units(12f)));
        if (face)
        {
            var side = MathF.Min(preview.Width, preview.Height);
            preview = Rect.FromSize(preview.Center - new Vector2(side * 0.5f, side * 0.5f),
                new Vector2(side, side));
            if (hasStill)
            {
                DrawFace(frame, preview.Center, side * 0.5f, string.Empty, tone.Accent, night, path);
                frame.Paint.StrokeCircle(preview.Center, side * 0.5f, tone.Accent,
                    MathF.Max(1.6f, frame.Units(2f)));
            }
            else
            {
                VybeChrome.EmptyPortrait(frame, preview.Center, side * 0.5f, night);
                frame.Paint.StrokeCircle(preview.Center, side * 0.5f, tone.Mute,
                    MathF.Max(1.6f, frame.Units(2f)));
                VybeChrome.AddPlus(frame, preview.Center, side * 0.16f, night);
            }
        }
        else
        {
            var height = MathF.Min(preview.Height, preview.Width * 0.42f);
            preview = Rect.FromSize(new Vector2(preview.Min.X, preview.Center.Y - height * 0.5f),
                new Vector2(preview.Width, height));
            if (!DrawBleedStill(frame, preview, path))
            {
                VybeChrome.EmptyBanner(frame, preview, night);
                VybeChrome.AddPlus(frame, preview.Center, frame.Units(16f), night);
            }

            frame.Paint.Stroke(preview, hasStill ? tone.Accent : tone.Faint, MathF.Max(1.6f, frame.Units(2f)), 0f);
        }

        if (!hasStill && frame.Input.ConsumeClick(preview))
        {
            state.Page = NightPage.PhotoPick;
            state.Scroll = 0f;
            return;
        }

        TickPhotoPlace(frame, preview, path);

        if (hasStill)
        {
            var drop = actions.TopSlice(frame.Units(36f));
            VybeChrome.Plate(frame, drop, frame.Units(12f), night);
            frame.Text.DrawIn(drop, face ? "Remove photo" : "Remove banner",
                new TextStyle(FontRole.CaptionStrong, tone.Danger, TextAlign.Center));
            if (frame.Input.ConsumeClick(drop))
            {
                ClearOwnStill(face);
                state.Back();
                return;
            }
        }

        var other = pair.LeftSlice(pair.Width * 0.48f);
        var done = pair.RightSlice(pair.Width * 0.48f);
        VybeChrome.Plate(frame, other, frame.Units(12f), night);
        frame.Text.DrawIn(other, hasStill ? "Choose another" : "Choose photo",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Center));
        VybeChrome.Primary(frame, done, "Done", night);
        if (frame.Input.ConsumeClick(other))
        {
            state.Page = NightPage.PhotoPick;
            state.Scroll = 0f;
            return;
        }

        if (frame.Input.ConsumeClick(done))
        {
            photoDrag = false;
            state.Save(paths);
            state.Back();
        }
    }

    private void TickPhotoPlace(in AppletFrame frame, Rect preview, string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var zoom = state.PickingAvatar ? state.FaceZoom : state.BannerZoom;
        var focus = state.PickingAvatar
            ? new Vector2(state.FaceFocusX, state.FaceFocusY)
            : new Vector2(state.BannerFocusX, state.BannerFocusY);
        if (frame.Input.WasPressed(preview))
        {
            photoDrag = true;
        }

        if (photoDrag && frame.Input.IsHeld())
        {
            var cover = CoverFit.Framed(texture.Size, preview.Size, zoom, focus);
            var visible = cover.Max - cover.Min;
            focus -= new Vector2(
                preview.Width > 1f ? frame.Input.PointerDelta.X / preview.Width * visible.X : 0f,
                preview.Height > 1f ? frame.Input.PointerDelta.Y / preview.Height * visible.Y : 0f);
            state.AdjustPlacing(zoom, focus.X, focus.Y);
        }

        if (!frame.Input.IsHeld())
        {
            photoDrag = false;
        }

        if (frame.Input.IsHovering(preview) && MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
        {
            state.AdjustPlacing(zoom * (1f + frame.Input.ScrollDelta * 0.14f), focus.X, focus.Y);
        }
    }

    private void TickViewLook(in AppletFrame frame, Rect dest, ITextureHandle? texture)
    {
        if (!string.Equals(viewLook, state.ViewMedia, StringComparison.Ordinal))
        {
            viewLook = state.ViewMedia;
            viewZoom = 1f;
            viewFocus = new Vector2(0.5f, 0.5f);
            viewDrag = false;
        }

        if (texture is not { IsReady: true } || dest.Width < 1f || dest.Height < 1f)
        {
            return;
        }

        if (frame.Input.IsHovering(dest))
        {
            frame.Input.Claim(dest);
            if (MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
            {
                var before = viewZoom;
                viewZoom = Math.Clamp(viewZoom * (1f + frame.Input.ScrollDelta * 0.18f), 1f, 4.5f);
                var local = new Vector2(
                    Math.Clamp((frame.Input.Pointer.X - dest.Min.X) / dest.Width, 0f, 1f),
                    Math.Clamp((frame.Input.Pointer.Y - dest.Min.Y) / dest.Height, 0f, 1f));
                var oldSpan = 1f / before;
                var img = viewFocus + (local - new Vector2(0.5f, 0.5f)) * oldSpan;
                var span = 1f / viewZoom;
                viewFocus = img - (local - new Vector2(0.5f, 0.5f)) * span;
            }
        }

        if (viewZoom > 1.01f && frame.Input.WasPressed(dest))
        {
            viewDrag = true;
        }

        if (viewDrag && frame.Input.IsHeld() && viewZoom > 1f)
        {
            var span = 1f / viewZoom;
            viewFocus -= new Vector2(
                frame.Input.PointerDelta.X / dest.Width * span,
                frame.Input.PointerDelta.Y / dest.Height * span);
        }

        if (!frame.Input.IsHeld())
        {
            viewDrag = false;
        }

        var half = 0.5f / viewZoom;
        viewFocus = new Vector2(
            Math.Clamp(viewFocus.X, half, 1f - half),
            Math.Clamp(viewFocus.Y, half, 1f - half));
    }

    private void DrawPhotoView(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var post = state.PostKey.Length > 0 ? BoardPost(state.PostKey) : null;
        var plus = post is { } plusPost && VybeDemo.IsPlusPost(plusPost);
        var caption = post is { } captionPost ? CardCaption(captionPost) : string.Empty;
        var marks = post is { } tagPost ? VybePostTags.Collect(tagPost) : [];
        var tagLine = VybePostTags.Join(marks);
        var pad = frame.Units(14f);
        var wrap = MathF.Max(1f, area.Width - pad * 2f);
        var captionH = caption.Length > 0
            ? frame.Text.MeasureWrapped(caption, FontRole.Caption, wrap).Y + frame.Units(4f)
            : 0f;
        var tagH = tagLine.Length > 0
            ? frame.Text.MeasureWrapped(tagLine, FontRole.CaptionStrong, wrap).Y + frame.Units(4f)
            : 0f;
        var gap = frame.Units(4f);
        var barH = frame.Units(32f);
        var metaH = barH + captionH + tagH + frame.Units(8f);
        var maxImage = Rect.FromSize(area.Min,
            new Vector2(area.Width, MathF.Max(frame.Units(180f), area.Height - metaH - gap)));
        var dest = maxImage;
        var texture = StillTexture(frame, state.ViewMedia);
        if (texture is { IsReady: true })
        {
            var fit = CoverFit.Contained(texture.Size, maxImage);
            dest = CoverFit.Snapped(Rect.FromSize(new Vector2(fit.Min.X, maxImage.Min.Y), fit.Size));
        }

        var blockH = dest.Height + gap + metaH;
        var shift = MathF.Max(0f, (area.Height - blockH) * 0.5f);
        dest = dest.Translate(new Vector2(0f, shift));
        TickViewLook(frame, dest, texture);
        if (texture is { IsReady: true })
        {
            var span = 1f / viewZoom;
            var uvMin = new Vector2(viewFocus.X - span * 0.5f, viewFocus.Y - span * 0.5f);
            var uvMax = new Vector2(viewFocus.X + span * 0.5f, viewFocus.Y + span * 0.5f);
            frame.Paint.Image(texture, dest, uvMin, uvMax, Vector4.One);
        }

        var back = Rect.FromSize(new Vector2(area.Min.X + frame.Units(8f), area.Min.Y + frame.Units(8f)),
            new Vector2(frame.Units(32f), frame.Units(28f)));
        frame.Paint.Fill(back, new Vector4(0f, 0f, 0f, 0.42f), back.Height * 0.5f);
        if (VybeChrome.Back(frame, back, string.Empty, true))
        {
            state.Back();
            return;
        }

        var stack = new Stack(
            new Rect(new Vector2(area.Min.X, dest.Max.Y + gap), area.Max),
            StackAxis.Vertical, frame.Units(4f));
        var bar = stack.Take(frame.Units(32f)).Inset(new Edges(pad, 0f, pad, 0f));
        if (post is { } live)
        {
            if (DrawFeedActions(frame, bar, live, night))
            {
                return;
            }
        }
        else
        {
            var liked = state.PostHearted(state.PostKey, false);
            VybeChrome.PostGlyph(frame, bar.LeftSlice(frame.Units(36f)), VybeChrome.LikeGlyph,
                CompactCount(liked ? 1 : 0), liked, tone, "♡");
            if (frame.Input.ConsumeClick(bar.LeftSlice(frame.Units(36f))))
            {
                HeartPost(state.PostKey, false);
                return;
            }
        }

        if (caption.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(captionH).Inset(new Edges(pad, 0f, pad, 0f)), caption,
                new TextStyle(FontRole.Caption, tone.Ink));
        }

        if (tagLine.Length > 0)
        {
            var accent = plus ? VybeChrome.PlusViolet : tone.Accent;
            frame.Text.DrawWrapped(stack.Take(tagH).Inset(new Edges(pad, 0f, pad, 0f)), tagLine,
                new TextStyle(FontRole.CaptionStrong, accent));
        }
    }

    private void DrawShareSend(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Send", night))
        {
            state.SharePostId = string.Empty;
            state.Back();
            return;
        }

        if (!pearl.Current.SignedIn)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Sign in from You to send.", night);
            return;
        }

        var shown = 0;
        foreach (var chat in pearl.Current.Chats)
        {
            var row = stack.Take(frame.Units(48f));
            VybeChrome.Plate(frame, row, frame.Units(10f), night);
            VybeChrome.Title(frame, row.Inset(frame.Units(10f)), chat.Title, night);
            if (frame.Input.ConsumeClick(row))
            {
                var cited = BoardPost(state.SharePostId);
                var preview = cited is { } p
                    ? "post:" + p.Id + " " + (p.Body.Length > 0 ? p.Body : p.AuthorName)
                    : "post:" + state.SharePostId;
                pearl.SendChat(chat.Id, preview);
                state.SharePostId = string.Empty;
                state.ChatKey = chat.Id;
                pearl.WatchChat(chat.Id);
                state.Open(NightPage.Chat);
            }

            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No chats yet.", night);
        }
    }

    private void DrawBlocked(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Blocked", night))
        {
            state.Back();
            return;
        }

        if (state.Blocked.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No one blocked.", night);
            return;
        }

        foreach (var id in state.Blocked.ToArray())
        {
            var name = state.TryFind(id, out var person)
                ? person.Name
                : state.BlockedLabels.GetValueOrDefault(id, "Blocked");
            if (name.Length == 0)
            {
                name = "Blocked";
            }

            var row = stack.Take(frame.Units(44f));
            VybeChrome.Plate(frame, row, frame.Units(10f), night);
            VybeChrome.Title(frame, row.Inset(new Edges(frame.Units(10f), 0f, frame.Units(80f), 0f)), name, night);
            if (VybeChrome.Chip(frame, row.RightSlice(frame.Units(76f)).Inset(frame.Units(6f)), "Unblock", false,
                    night))
            {
                state.Unblock(id);
                state.Save(paths);
            }
        }
    }

    private void DrawPeopleList(in AppletFrame frame, Rect area, string title, bool connected)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
        {
            state.Back();
            return;
        }

        var shown = 0;
        foreach (var person in state.Roster)
        {
            if (connected)
            {
                if (!state.Connected.Contains(person.Id))
                {
                    continue;
                }
            }
            else if (!state.Incoming.Contains(person.Id))
            {
                continue;
            }

            if (!night && person.NightOnly)
            {
                continue;
            }

            DrawMayKnow(frame, stack.Take(frame.Units(56f)), person);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                connected
                    ? "Follow people in Discover and they land here."
                    : "Followers and requests you accept land here.", night);
        }
    }

    private static void DrawToggle(in AppletFrame frame, Rect area, string label, bool on, bool night, Action flip)
    {
        VybeChrome.Plate(frame, area, frame.Units(12f), night);
        VybeChrome.Title(frame, area.Inset(new Edges(frame.Units(12f), 0f, frame.Units(64f), 0f)), label, night);
        var knob = area.RightSlice(frame.Units(52f)).Inset(new Edges(0f, frame.Units(10f), frame.Units(10f),
            frame.Units(10f)));
        VybeChrome.Glow(frame, knob, frame.Units(10f), on, night);
        frame.Text.DrawIn(knob, on ? "On" : "Off",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            flip();
        }
    }

}
