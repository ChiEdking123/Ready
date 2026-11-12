using System.Text.Json.Serialization;

namespace Ready.Models;

/// <summary>All editable content of an idea. Lists are kept in selection order.</summary>
public class IdeaData
{
    public string Summary { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public List<string> Perspectives { get; set; } = [];
    public List<string> ArtStyles { get; set; } = [];
    public List<string> Settings { get; set; } = [];
    public List<string> Roles { get; set; } = [];
    public string References { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public List<string> Audiences { get; set; } = [];
    public List<string> Durations { get; set; } = [];
    public List<string> LoopDurations { get; set; } = [];
    public List<string> Emotions { get; set; } = [];
    public List<string> Inputs { get; set; } = [];
    public string Scale { get; set; } = "";
    public List<string> Resources { get; set; } = [];
    public List<string> NonGoals { get; set; } = [];
    public List<string> Monetization { get; set; } = [];

    public IdeaData Copy() => new()
    {
        Summary = Summary,
        Genres = [.. Genres], Perspectives = [.. Perspectives], ArtStyles = [.. ArtStyles],
        Settings = [.. Settings], Roles = [.. Roles], References = References,
        Platforms = [.. Platforms], Audiences = [.. Audiences], Durations = [.. Durations],
        LoopDurations = [.. LoopDurations], Emotions = [.. Emotions], Inputs = [.. Inputs],
        Scale = Scale, Resources = [.. Resources], NonGoals = [.. NonGoals],
        Monetization = [.. Monetization]
    };

    public List<string>? TagsFor(string field) => field switch
    {
        "genres" => Genres, "perspectives" => Perspectives, "art" => ArtStyles,
        "settings" => Settings, "roles" => Roles, "platforms" => Platforms,
        "audiences" => Audiences, "durations" => Durations, "loops" => LoopDurations,
        "emotions" => Emotions, "inputs" => Inputs, "resources" => Resources,
        "nongoals" => NonGoals, "monetization" => Monetization, _ => null
    };

    public static readonly string[] TagFields =
        ["genres", "perspectives", "art", "settings", "roles", "platforms", "audiences", "durations", "loops", "emotions", "inputs", "resources", "nongoals", "monetization"];

    public void Normalize()
    {
        Summary = Summary.Trim();
        References = References.Trim();
        foreach (var field in TagFields)
        {
            var tags = TagsFor(field)!;
            var clean = tags.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            tags.Clear(); tags.AddRange(clean);
        }
    }
}

public sealed class IdeaDraft : IdeaData;

public sealed class IdeaRecord : IdeaData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public bool Pinned { get; set; }

    public static IdeaRecord FromDraft(IdeaDraft draft) => From(draft);
    public static IdeaRecord From(IdeaData data)
    {
        var result = new IdeaRecord();
        data.CopyTo(result);
        result.Normalize();
        return result;
    }
}

public static class IdeaDataExtensions
{
    public static void CopyTo(this IdeaData source, IdeaData target)
    {
        target.Summary = source.Summary; target.Genres = [.. source.Genres];
        target.Perspectives = [.. source.Perspectives]; target.ArtStyles = [.. source.ArtStyles];
        target.Settings = [.. source.Settings]; target.Roles = [.. source.Roles];
        target.References = source.References; target.Platforms = [.. source.Platforms];
        target.Audiences = [.. source.Audiences]; target.Durations = [.. source.Durations];
        target.LoopDurations = [.. source.LoopDurations]; target.Emotions = [.. source.Emotions];
        target.Inputs = [.. source.Inputs]; target.Scale = source.Scale;
        target.Resources = [.. source.Resources]; target.NonGoals = [.. source.NonGoals];
        target.Monetization = [.. source.Monetization];
    }
}
