using Algetric.Core;
using Xunit;
namespace Algetric.Tests;

public class BridgeTests
{
    [Fact]
    public void RejectsReplayedAndStaleEdits()
    {
        var gate = new BridgeMessageGate(); var session = gate.Session;
        Assert.True(gate.Accept(session, 1, 0, 0));
        Assert.False(gate.Accept(session, 1, 0, 0));
        Assert.False(gate.Accept(session, 2, 0, 1));
        Assert.False(gate.Accept("other", 3, 1, 1));
        Assert.True(gate.Accept(session, 4, 1, 1));
    }
    [Fact]
    public void NewProjectInvalidatesPreviousSession()
    {
        var gate = new BridgeMessageGate(); var old = gate.Session;
        gate.Reset(); Assert.False(gate.Accept(old, 1, 0, 0));
    }
}
