namespace Algetric.Core;

public sealed class BridgeMessageGate
{
    private long lastSequence;
    public string Session { get; private set; } = Guid.NewGuid().ToString("N");
    public bool Accept(string session, long sequence, long revision, long currentRevision)
    {
        if (session != Session || sequence <= lastSequence) return false;
        lastSequence = sequence;
        return revision == currentRevision;
    }
    public void Reset() { Session = Guid.NewGuid().ToString("N"); lastSequence = 0; }
}
