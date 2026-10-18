using Algetric.Core;
using Algetric.Desktop;
using Xunit;

namespace Algetric.Tests;

public class CoveragePathAsyncTests
{
    private sealed class PausedGenerator : ICoveragePathGenerator, IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public LocalPoint[] Generate(GeometryObject polygon, CoveragePathOptions options)
        {
            Started.SetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            return [options.Start, new(10, 0), options.End];
        }
        public void Dispose() => Release.Dispose();
    }
    [Fact]
    public async Task CancellingWhileTheNativeCallRunsDoesNotInsertAPath()
    {
        using var generator = new PausedGenerator(); var model = new EditorViewModel(generator);
        model.Load(TestProjects.WithLayers().Document);
        var polygon = model.Editor.Add(ShapeKind.Polygon, [new(0, 0), new(20, 0), new(20, 20)]);
        var before = model.Editor.Document;
        using var cancellation = new CancellationTokenSource();
        var generating = model.GenerateCoveragePathAsync(polygon.Id, new(new(0, 0), new(10, 10), 5, 0), cancellation.Token);
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel(); generator.Release.Set();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generating);
        Assert.Same(before, model.Editor.Document);
    }
    [Fact]
    public async Task RemovingAnObstructionDiscardsThePendingGeneration()
    {
        using var generator = new PausedGenerator(); var model = new EditorViewModel(generator);
        model.Load(TestProjects.WithLayers().Document);
        var polygon = model.Editor.Add(ShapeKind.Polygon, [new(0, 0), new(100, 0), new(100, 100), new(0, 100)]);
        var obstruction = model.Editor.Add(ShapeKind.Polygon, [new(20, 20), new(30, 20), new(30, 30), new(20, 30)]);
        var generating = model.GenerateCoveragePathAsync(polygon.Id, new(new(0, 0), new(100, 100), 5, 0, [obstruction]));
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        model.Editor.Select(obstruction.Id); model.Editor.DeleteSelected(); generator.Release.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(() => generating);
        Assert.DoesNotContain(model.Editor.Document.Objects, shape => shape.Kind == ShapeKind.CoveragePath);
    }
    [Fact]
    public async Task LoadingAnotherProjectDiscardsThePendingGeneration()
    {
        using var generator = new PausedGenerator(); var model = new EditorViewModel(generator);
        model.Load(TestProjects.WithLayers().Document);
        var polygon = model.Editor.Add(ShapeKind.Polygon, [new(0, 0), new(20, 0), new(20, 20)]);
        var original = model.Editor.Document;
        var generating = model.GenerateCoveragePathAsync(polygon.Id, new(new(0, 0), new(10, 10), 5, 0));
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); model.Load(original); generator.Release.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(() => generating);
        Assert.DoesNotContain(model.Editor.Document.Objects, shape => shape.Kind == ShapeKind.CoveragePath);
    }
}
