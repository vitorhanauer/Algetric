using System.Text.Json;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class JsonProjectRepository : IProjectRepository
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public async Task<ProjectDocument> LoadAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<ProjectDocument>(stream, Json)
                ?? throw new InvalidDataException("Projeto vazio.");
            Validate(document); return document;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        { throw new InvalidDataException("Arquivo de projeto inválido ou versão incompatível.", error); }
    }
    public async Task SaveAsync(string path, ProjectDocument document)
    {
        Validate(document);
        var temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, document, Json); await stream.FlushAsync(); stream.Flush(true); }
            File.Move(temporary, Path.GetFullPath(path), true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Validate(ProjectDocument document)
    {
        GeometryRules.ValidateDocument(document);
        using var transform = new ProjCoordinateTransform(document.Origin);
        foreach (var shape in document.Objects)
            foreach (var vertex in shape.Vertices) transform.ToGeographic(vertex);
    }
}
