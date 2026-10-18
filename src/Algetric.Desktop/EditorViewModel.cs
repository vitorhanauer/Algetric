using System.ComponentModel;
using System.Text.Json;
using Algetric.Core;
using Algetric.Infrastructure;

namespace Algetric.Desktop;

public enum EditorTool { Select, Point, Segment, Polyline, Polygon, Origin, Move }
public sealed record BridgeMessage(string Type, string Session, long Sequence, long Revision,
    Guid? Id, GeographicPoint[]? Vertices, int Vertex = 0, double Longitude = 0, double Latitude = 0, double Height = 0, Guid[]? Ids = null, EditorTool? Tool = null);
public sealed partial class EditorViewModel : INotifyPropertyChanged
{
    public ProjectEditor Editor { get; } = new();
    private readonly BridgeMessageGate gate = new();
    public EditorTool SelectedTool { get; private set; }
    public bool IsTemporaryMove { get; private set; }
    public EditorTool Tool => IsTemporaryMove ? EditorTool.Move : SelectedTool;
    public int VertexIndex { get; set; }
    public string? FilePath { get; set; }
    private string statusMessage = "Pronto • Coordenadas locais em metros";
    public string Status => UiText.T(statusMessage);
    public TileProvider? Provider { get; set; }
    public long Revision { get; private set; }
    public string Title => $"Algetric — {(FilePath is null ? UiText.T("Novo projeto") : Path.GetFileName(FilePath))}{(Editor.IsDirty ? " *" : "")}";
    public GeometryObject? Selected => Editor.Document.Objects.FirstOrDefault(o => o.Id == Editor.SelectedId);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    public event Action<string>? FileActionRequested;
    public event Action<string>? WarningRequested;
    public event Action<Guid?>? CoveragePathRequested;
    private readonly ICoveragePathGenerator pathGenerator;
    public EditorViewModel(ICoveragePathGenerator? pathGenerator = null) { this.pathGenerator = pathGenerator ?? new BoustrophedonPathGenerator(); Editor.Changed += OnChanged; Editor.ViewChanged += () => PropertyChanged?.Invoke(this, new(null)); }
    private void OnChanged()
    {
        Revision++; PropertyChanged?.Invoke(this, new(null)); Changed?.Invoke();
    }
    public void SetStatus(string value) { statusMessage = value; PropertyChanged?.Invoke(this, new(nameof(Status))); }
    public void SetTool(EditorTool tool) { SelectedTool = tool; IsTemporaryMove = false; OnChanged(); }
    public void BeginTemporaryMove()
    { if (IsTemporaryMove) return; IsTemporaryMove = true; OnChanged(); }
    public void EndTemporaryMove()
    { if (!IsTemporaryMove) return; IsTemporaryMove = false; OnChanged(); }
    public void Load(ProjectDocument document)
    { gate.Reset(); VertexIndex = 0; SelectedTool = EditorTool.Select; IsTemporaryMove = false; Editor.Load(document); }
    public void Try(Action action)
    {
        try { action(); SetStatus("Pronto • Medidas planimétricas"); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        { WarningRequested?.Invoke(error.Message); }
    }
    public bool EnsureActiveLayer()
    {
        if (Editor.ActiveLayerId is not null) return true;
        WarningRequested?.Invoke(ProjectEditor.MissingLayerMessage);
        return false;
    }
    public void Add(ShapeKind kind, IEnumerable<LocalPoint> vertices)
    {
        if (Editor.ActiveLayerId is null) throw new InvalidOperationException(ProjectEditor.MissingLayerMessage);
        var points = vertices.ToArray(); ValidateGeographic(points); VertexIndex = 0; Editor.Add(kind, points);
    }
    public void Replace(Guid id, IEnumerable<LocalPoint> vertices)
    { var points = vertices.ToArray(); ValidateGeographic(points); Editor.ReplaceVertices(id, points); }
    public void UpdateObject(Guid id, string name, IEnumerable<LocalPoint> vertices)
    { var points = vertices.ToArray(); ValidateGeographic(points); Editor.UpdateObject(id, name, points); }
    private void ValidateGeographic(IEnumerable<LocalPoint> points)
    {
        using var transform = new ProjCoordinateTransform(Editor.Document.Origin);
        foreach (var point in points) transform.ToGeographic(point);
    }
    public string MapState()
    {
        using var transform = new ProjCoordinateTransform(Editor.Document.Origin);
        var document = Editor.Document;
        return JsonSerializer.Serialize(new
        {
            session = gate.Session,
            revision = Revision,
            tool = (int)Tool,
            selectedId = Editor.SelectedId,
            selectedIds = Editor.SelectedIds,
            canCreateObject = Editor.ActiveLayerId is not null,
            origin = document.Origin,
            view = document.View,
            status = Status,
            provider = Provider is null ? null : new { Provider.Id, Provider.MinZoom, Provider.MaxZoom, Provider.Attribution },
            objects = document.Objects.Where(o => document.Layers.Any(l => l.Id == o.LayerId && l.Visible)).Select(o => new
            {
                o.Id,
                o.Name,
                kind = (int)o.Kind,
                color = document.Layers.First(l => l.Id == o.LayerId).Color,
                vertices = o.Vertices.Select(transform.ToGeographic).ToArray()
            }).ToArray()
        }, JsonProjectRepository.Json);
    }
    public string HandleMessage(string json)
    {
        Try(() =>
        {
            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonProjectRepository.Json)
                ?? throw new ArgumentException("Mensagem vazia.");
            if (!gate.Accept(message.Session, message.Sequence, message.Revision, Revision))
                throw new ArgumentException("Visualização atualizada; repita a ação.");
            Apply(message);
        });
        return MapState();
    }
    private void Apply(BridgeMessage message)
    {
        if (message.Type is "save" or "open" or "new" or "addLayer") { FileActionRequested?.Invoke(message.Type); return; }
        if (message.Type == "generatePath") { RequestCoveragePath(message.Id); return; }
        if (message.Type == "requireLayer") { EnsureActiveLayer(); return; }
        if (message.Type == "tool" && message.Tool is { } tool)
        {
            if (!EditorShortcuts.Tools.Any(shortcut => shortcut.Tool == tool)) throw new ArgumentException("Ferramenta inválida.");
            SetTool(tool); return;
        }
        if (message.Type == "delete") { Editor.DeleteSelected(); return; }
        if (message.Type == "undo") { Editor.Undo(); return; }
        if (message.Type == "redo") { Editor.Redo(); return; }
        if (message.Type == "select")
        {
            if (message.Id is not null && !Editor.Document.Objects.Any(o => o.Id == message.Id)) throw new ArgumentException("Objeto inexistente.");
            VertexIndex = Math.Max(0, message.Vertex); Editor.Select(message.Id); return;
        }
        if (message.Type == "selectMany")
        {
            if (Tool != EditorTool.Select) throw new ArgumentException("Ação incompatível com a ferramenta atual.");
            VertexIndex = 0; Editor.SelectMany(message.Ids ?? []); return;
        }
        if (message.Type == "camera")
        {
            if (!double.IsFinite(message.Height) || message.Height <= 0 || !double.IsFinite(message.Latitude)
                || Math.Abs(message.Latitude) > 90 || !double.IsFinite(message.Longitude) || Math.Abs(message.Longitude) > 180)
                throw new ArgumentException("Câmera inválida.");
            Editor.UpdateView(Editor.Document.View with { Longitude = message.Longitude, Latitude = message.Latitude, Height = message.Height }); return;
        }
        if (message.Type == "origin")
        { Editor.SetOrigin((message.Vertices ?? []).Single()); return; }
        using var transform = new ProjCoordinateTransform(Editor.Document.Origin);
        var points = (message.Vertices ?? []).Select(transform.ToLocal).ToArray();
        if (message.Type == "create" && Tool is >= EditorTool.Point and <= EditorTool.Polygon)
            Add((ShapeKind)((int)Tool - 1), points);
        else if (message.Type == "replace" && Tool == EditorTool.Select && message.Id is { } id && id == Editor.SelectedId) Replace(id, points);
        else throw new ArgumentException("Ação incompatível com a ferramenta atual.");
    }
}
