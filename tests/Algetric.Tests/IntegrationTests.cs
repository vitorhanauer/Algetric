using Algetric.Core;
using Algetric.Infrastructure;
using Algetric.Desktop;
using System.Text.Json;
using Xunit;
namespace Algetric.Tests;

public class IntegrationTests
{
    [Fact]
    public void GeographicEditsUpdateSharedDocumentAndCannotReplay()
    {
        var model = new EditorViewModel(); model.Load(TestProjects.WithLayers().Document); model.SetTool(EditorTool.Point);
        using var snapshot = JsonDocument.Parse(model.MapState());
        var message = JsonSerializer.Serialize(new
        {
            type = "create",
            session = snapshot.RootElement.GetProperty("session").GetString(),
            sequence = 1,
            revision = model.Revision,
            vertices = new[] { new GeographicPoint(-23.55, -46.63) }
        }, JsonProjectRepository.Json);
        model.HandleMessage(message); model.HandleMessage(message);
        var shape = Assert.Single(model.Editor.Document.Objects);
        Assert.InRange(Math.Abs(shape.Vertices[0].X), 0, 0.001);
        Assert.InRange(Math.Abs(shape.Vertices[0].Y), 0, 0.001);
        model.Editor.Undo(); Assert.Empty(model.Editor.Document.Objects);
    }
    [Fact]
    public async Task DiskSpaceFailureDoesNotStartDownloadOrRegisterRegion()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var store = new SqliteTileStore(path); var downloader = new RegionDownloadService(store, new HttpClient(), _ => false);
            var provider = new TileProvider("test", "Test", "https://example.com/{z}/{x}/{y}", 0, 10, "Test", true);
            await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(provider,
                new(Guid.NewGuid(), "Test", "test", 0, 0, 1, 1, 0, 0), null, CancellationToken.None));
            Assert.Empty(await store.ListRegionsAsync());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Fact]
    public async Task RemovingOneRegionPreservesTilesSharedWithAnother()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var store = new SqliteTileStore(path);
            var a = new OfflineRegion(Guid.NewGuid(), "A", "test", 0, 0, 1, 1, 0, 0); var b = a with { Id = Guid.NewGuid(), Name = "B" };
            await store.SaveRegionAsync(a); await store.SaveRegionAsync(b);
            await store.PutAsync("test", new(0, 0, 0), [1], "image/png", a.Id);
            await store.PutAsync("test", new(0, 0, 0), [1], "image/png", b.Id);
            await store.SaveRegionAsync(a); // Resuming a region must retain its existing membership.
            await store.RemoveRegionAsync(b.Id);
            Assert.NotNull(await store.GetAsync("test", new(0, 0, 0)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [Theory]
    [InlineData("http://example.com/{z}/{x}/{y}")]
    [InlineData("https://example.com/{z}/{x}/{y}?token=secret")]
    [InlineData("https://user:password@example.com/{z}/{x}/{y}")]
    public void ProviderRejectsInsecureOrEmbeddedCredentials(string template)
        => Assert.Throws<ArgumentException>(() => new TileProvider("t", "T", template, 0, 18, "T", true).Validate());

    [Fact]
    public async Task FailedSaveLeavesExistingProjectIntact()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".algetric");
        try
        {
            var repository = new JsonProjectRepository(); var document = ProjectDocument.Empty();
            await repository.SaveAsync(path, document);
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(path, document with { Version = 999 }));
            Assert.Equal(document.Origin, (await repository.LoadAsync(path)).Origin);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".*.tmp"));
        }
        finally { File.Delete(path); }
    }
}
