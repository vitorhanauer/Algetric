using Algetric.Core;
using Xunit;

namespace Algetric.Tests;

public class FunctionExpressionTests
{
    [Theory]
    [InlineData("5", 7, 5)]
    [InlineData("2*x^3 - 3*x + 1", 2, 11)]
    [InlineData("2^x", 3, 8)]
    [InlineData("exp(ln(x))", 4, 4)]
    [InlineData("log(100) + log(8, 2)", 0, 5)]
    [InlineData("sin(pi/2) + cos(0) + tan(0)", 0, 2)]
    [InlineData("sen(π/2) + tg(0)", 0, 1)]
    [InlineData("2x + (x+1)(x-1)", 3, 14)]
    [InlineData("2(x+1) + 3sin(x)", 0, 2)]
    [InlineData("-x^2", 2, -4)]
    [InlineData("2^-2", 0, 0.25)]
    [InlineData("2^3^2", 0, 512)]
    [InlineData("f(x) = sqrt(abs(x)) + ln(e)", -4, 3)]
    [InlineData("y = 1.5e-2*x", 100, 1.5)]
    [InlineData("SIN(x)^2 + COS(x)^2", 0.7, 1)]
    public void EvaluatesSupportedFunctionsAndCompositions(string expression, double x, double expected)
        => Assert.Equal(expected, FunctionExpression.Parse(expression).Evaluate(x), 9);

    [Theory]
    [InlineData("")]
    [InlineData("x +")]
    [InlineData("sin(")]
    [InlineData("foo(x)")]
    [InlineData("sin(x, 2)")]
    [InlineData("x; System.IO.File.Delete('a')")]
    [InlineData("2..3")]
    [InlineData("x) + 1")]
    public void RejectsInvalidOrUnsupportedExpressions(string expression)
        => Assert.Throws<ArgumentException>(() => FunctionExpression.Parse(expression));

    [Fact]
    public void UndefinedValuesRemainNonFiniteAndComplexityIsBounded()
    {
        Assert.False(double.IsFinite(FunctionExpression.Parse("1/x").Evaluate(0)));
        Assert.True(double.IsNaN(FunctionExpression.Parse("ln(x)").Evaluate(-1)));
        Assert.Throws<ArgumentException>(() => FunctionExpression.Parse(new string('(', 200) + "x" + new string(')', 200)));
        Assert.Throws<ArgumentException>(() => FunctionExpression.Parse(string.Join("+", Enumerable.Repeat("x", 2000))));
    }
}
