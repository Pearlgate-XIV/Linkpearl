using System.Globalization;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Linkpearl.Audio;

internal readonly record struct ScannedPort(string Id, string Label, string Note, bool Active);

internal static class WasapiDeviceScan
{
    private delegate bool DsEnumCallback(nint guid, nint description, nint module, nint ctx);

    [DllImport("dsound.dll", CharSet = CharSet.Unicode)]
    private static extern int DirectSoundEnumerateW(DsEnumCallback callback, nint context);

    [DllImport("dsound.dll", CharSet = CharSet.Unicode)]
    private static extern int DirectSoundCaptureEnumerateW(DsEnumCallback callback, nint context);

    public static ScannedPort[] Render() => Scan(DataFlow.Render);

    public static ScannedPort[] Capture() => Scan(DataFlow.Capture);

    private static ScannedPort[] Scan(DataFlow flow)
    {
        var list = new List<ScannedPort>();
        var remote = new List<ScannedPort>();
        if (RunMta(() => FillWasapi(remote, flow)) && remote.Count > 0)
        {
            list.AddRange(remote);
        }
        else
        {
            FillWasapi(list, flow);
        }

        if (flow == DataFlow.Render)
        {
            FillWaveOut(list);
            FillDirectSound(list, capture: false);
            FillAsio(list);
        }
        else
        {
            FillWaveIn(list);
            FillDirectSound(list, capture: true);
        }

        list.Sort(static (left, right) => right.Active.CompareTo(left.Active));
        return list.ToArray();
    }

    private static void FillWasapi(List<ScannedPort> list, DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            AddDefault(list, enumerator, flow, Role.Multimedia, "WASAPI · Multimedia default");
            AddDefault(list, enumerator, flow, Role.Communications, "WASAPI · Communications default");
            var found = enumerator.EnumerateAudioEndPoints(flow, DeviceState.All);
            var count = found.Count;
            for (var index = 0; index < count; index++)
            {
                MMDevice? device = null;
                try
                {
                    device = found[index];
                    var id = (device.ID ?? string.Empty).Trim();
                    if (id.Length == 0 || HasId(list, id))
                    {
                        continue;
                    }

                    list.Add(new ScannedPort(id, ReadName(device, flow, index), Note(flow, device.State),
                        device.State == DeviceState.Active));
                }
                catch (Exception)
                {
                }
                finally
                {
                    try
                    {
                        device?.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static void AddDefault(List<ScannedPort> list, MMDeviceEnumerator enumerator, DataFlow flow, Role role,
        string note)
    {
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(flow, role);
            var id = (device.ID ?? string.Empty).Trim();
            if (id.Length == 0 || HasId(list, id))
            {
                return;
            }

            list.Add(new ScannedPort(id, ReadName(device, flow, 0), note, device.State == DeviceState.Active));
        }
        catch (Exception)
        {
        }
    }

    private static void FillWaveOut(List<ScannedPort> list)
    {
        try
        {
            list.Add(new ScannedPort("waveout:-1", "Windows sound mapper", "MME · Playback mapper", true));
            var count = WaveOut.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var name = WaveOut.GetCapabilities(index).ProductName.Trim();
                if (name.Length == 0)
                {
                    name = "MME playback " + (index + 1).ToString(CultureInfo.InvariantCulture);
                }

                list.Add(new ScannedPort("waveout:" + index.ToString(CultureInfo.InvariantCulture), name,
                    "MME · Playback", true));
            }
        }
        catch (Exception)
        {
        }
    }

    private static void FillWaveIn(List<ScannedPort> list)
    {
        try
        {
            list.Add(new ScannedPort("wavein:-1", "Windows sound mapper", "MME · Recording mapper", true));
            var count = WaveIn.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var name = WaveIn.GetCapabilities(index).ProductName.Trim();
                if (name.Length == 0)
                {
                    name = "MME recording " + (index + 1).ToString(CultureInfo.InvariantCulture);
                }

                list.Add(new ScannedPort("wavein:" + index.ToString(CultureInfo.InvariantCulture), name,
                    "MME · Recording", true));
            }
        }
        catch (Exception)
        {
        }
    }

    private static void FillDirectSound(List<ScannedPort> list, bool capture)
    {
        var extra = new List<ScannedPort>();
        DsEnumCallback callback = (guidPtr, descriptionPtr, _, _) =>
        {
            try
            {
                var guid = guidPtr == IntPtr.Zero ? Guid.Empty : Marshal.PtrToStructure<Guid>(guidPtr);
                var name = Marshal.PtrToStringUni(descriptionPtr)?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    name = capture ? "DirectSound capture" : "DirectSound playback";
                }

                var id = (capture ? "dsin:" : "dsout:") + guid.ToString("N");
                if (!HasId(list, id) && !HasId(extra, id))
                {
                    extra.Add(new ScannedPort(id, name,
                        capture ? "DirectSound · Recording" : "DirectSound · Playback", true));
                }
            }
            catch (Exception)
            {
            }

            return true;
        };

        try
        {
            if (capture)
            {
                DirectSoundCaptureEnumerateW(callback, IntPtr.Zero);
            }
            else
            {
                DirectSoundEnumerateW(callback, IntPtr.Zero);
            }
        }
        catch (Exception)
        {
        }

        GC.KeepAlive(callback);
        list.AddRange(extra);
    }

    private static void FillAsio(List<ScannedPort> list)
    {
        try
        {
            var names = AsioOut.GetDriverNames();
            for (var index = 0; index < names.Length; index++)
            {
                var name = (names[index] ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                var id = "asio:" + name;
                if (!HasId(list, id))
                {
                    list.Add(new ScannedPort(id, name, "ASIO · Playback", true));
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static string ReadName(MMDevice device, DataFlow flow, int index)
    {
        try
        {
            var name = (device.FriendlyName ?? string.Empty).Trim();
            if (name.Length > 0)
            {
                return name;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            var name = (device.DeviceFriendlyName ?? string.Empty).Trim();
            if (name.Length > 0)
            {
                return name;
            }
        }
        catch (Exception)
        {
        }

        return (flow == DataFlow.Render ? "Playback " : "Input ") +
               (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string Note(DataFlow flow, DeviceState state)
    {
        var note = flow == DataFlow.Render ? "WASAPI · Playback loopback" : "WASAPI · Recording";
        if (state == DeviceState.Active)
        {
            return note;
        }

        if (state.HasFlag(DeviceState.Disabled))
        {
            return note + " · disabled in Windows";
        }

        if (state.HasFlag(DeviceState.Unplugged))
        {
            return note + " · unplugged";
        }

        if (state.HasFlag(DeviceState.NotPresent))
        {
            return note + " · not present";
        }

        return note + " · " + state;
    }

    private static bool HasId(List<ScannedPort> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RunMta(Action work)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
        {
            work();
            return true;
        }

        Exception? error = null;
        using var done = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                work();
            }
            catch (Exception failure)
            {
                error = failure;
            }
            finally
            {
                done.Set();
            }
        })
        {
            IsBackground = true,
            Name = "Linkpearl-WASAPI",
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return done.Wait(TimeSpan.FromSeconds(4)) && error is null;
    }
}
