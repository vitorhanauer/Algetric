using Algetric.Core;
using Xunit;

namespace Algetric.Tests;

public class FunctionSamplingTests
{
    [Theory]
    [InlineData("5")]
    [InlineData("x^2 + sin(x)")]
    [InlineData("2^x")]
    public void SamplesFollowTheExpressionAndIncludeTheDomainEndpoints(string expression)
    {
        var definition = new FunctionDefinition(expression, -2, 2);
        var segment = Assert.Single(definition.GetSegments());
        var function = FunctionExpression.Parse(expression);
        Assert.Equal(-2, segment[0].X); Assert.Equal(2, segment[^1].X);
        Assert.All(segment, point => Assert.Equal(function.Evaluate(point.X), point.Y, 9));
    }
    [Theory]
    [InlineData("1/(x-0.12345)", 0.12345)]
    [InlineData("tan(x)", 1.5707963267948966)]
    public void SeparateSegmentsDoNotCrossVerticalAsymptotes(string expression, double asymptote)
    {
        var segments = new FunctionDefinition(expression, -2, 2).GetSegments();
        Assert.True(segments.Length >= 2);
        Assert.All(segments, segment => Assert.False(segment[0].X < asymptote && segment[^1].X > asymptote));
        Assert.All(segments.SelectMany(segment => segment), point => Assert.True(double.IsFinite(point.Y) && point.X * point.X + point.Y * point.Y <= 2_500_000_000));
    }
    [Fact]
    public void LogarithmsSkipUndefinedPartsOfTheirDomain()
    {
        var points = new FunctionDefinition("ln(x)", -2, 2).GetSegments().SelectMany(segment => segment).ToArray();
        Assert.NotEmpty(points);
        Assert.All(points, point => { Assert.True(point.X > 0); Assert.Equal(Math.Log(point.X), point.Y, 9); });
    }
    [Theory]
    [InlineData("x", 2, -2)]
    [InlineData("x", 2, 2)]
    [InlineData("x", double.NaN, 2)]
    [InlineData("x", -50001, 2)]
    [InlineData("ln(-abs(x)-1)", -2, 2)]
    public void InvalidOrEntirelyUndefinedDomainsAreRejected(string expression, double minimum, double maximum)
        => Assert.Throws<ArgumentException>(() => new FunctionDefinition(expression, minimum, maximum));
}
