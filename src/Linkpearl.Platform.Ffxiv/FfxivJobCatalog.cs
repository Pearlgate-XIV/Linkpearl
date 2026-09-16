using Dalamud.Plugin.Services;
using ClassJobSheet = Lumina.Excel.Sheets.ClassJob;

namespace Linkpearl.Platform.Ffxiv;

// Every playable ClassJob row, with the official 062xxx icon the client uses next to a job
// name. The textures themselves stay in the game files and are drawn through ITextureSource.
public sealed class FfxivJobCatalog : IJobCatalog
{
    private readonly JobFace[] all;
    private readonly Dictionary<uint, JobFace> byId = new();
    private readonly Dictionary<string, JobFace> byName = new(StringComparer.OrdinalIgnoreCase);

    public FfxivJobCatalog(IDataManager data)
    {
        var sheet = data.GetExcelSheet<ClassJobSheet>();
        var rows = new List<JobFace>();
        foreach (var job in sheet)
        {
            if (job.RowId == 0)
            {
                continue;
            }

            var name = job.Name.ExtractText() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            var face = new JobFace(job.RowId, name, job.Abbreviation.ExtractText() ?? string.Empty,
                JobIconIds.FromJob(job.RowId), job.Role);
            rows.Add(face);
            byId[face.Id] = face;
            byName[face.Name] = face;
            if (face.Abbreviation.Length > 0)
            {
                byName[face.Abbreviation] = face;
            }
        }

        all = rows.ToArray();
    }

    public IReadOnlyList<JobFace> All => all;

    public bool TryGet(uint id, out JobFace job) => byId.TryGetValue(id, out job);

    public bool TryGetByName(string name, out JobFace job)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            job = default;
            return false;
        }

        return byName.TryGetValue(name.Trim(), out job);
    }

    public uint IconFor(uint jobId)
    {
        return byId.TryGetValue(jobId, out var job) ? job.IconId : JobIconIds.FromJob(jobId);
    }
}
