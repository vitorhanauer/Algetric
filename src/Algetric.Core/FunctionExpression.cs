namespace Algetric.Core;

public sealed class FunctionExpression
{
    public string Expression { get; }
    private readonly Func<double, double> evaluate;
    internal FunctionExpression(string expression, Func<double, double> evaluate)
    { Expression = expression; this.evaluate = evaluate; }
    public static FunctionExpression Parse(string expression) => new FunctionParser(expression).Parse();
    public double Evaluate(double x) => evaluate(x);
}
