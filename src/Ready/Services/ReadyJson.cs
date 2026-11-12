using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ready.Models;

namespace Ready.Services;

/// <summary>
/// Source-generated serializer context. Android Release builds run with
/// <c>AndroidLinkMode=SdkOnly</c>, and the reflection-based <c>JsonSerializer</c> overloads are
/// annotated <c>RequiresUnreferencedCode</c>: under trimming the linker may drop members it
/// cannot prove are used, which for a serializer means quietly writing fewer fields or
/// reading them back as defaults. Every model that is persisted or exported therefore goes
/// through this context, so the shape of the JSON is decided at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppDocument))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(IdeaExport))]
[JsonSerializable(typeof(IdeaRecord))]
[JsonSerializable(typeof(IdeaDraft))]
[JsonSerializable(typeof(IdeaData))]
[JsonSerializable(typeof(List<IdeaRecord>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
public partial class ReadyJsonContext : JsonSerializerContext;

/// <summary>
/// The store's single entry point to JSON. Keeping the options here means the on-disk
/// format lives in one place: indented output, case-insensitive reads, and the source
/// generator's metadata, never a reflection-built contract.
/// </summary>
public static class ReadyJson
{
    public static JsonSerializerOptions Options { get; } = new(ReadyJsonContext.Default.Options);

    public static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)ReadyJsonContext.Default.GetTypeInfo(typeof(T))!;

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Info<T>());

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize(json, Info<T>());
}
