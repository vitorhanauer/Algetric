using System.Globalization;

namespace Algetric.Core;

internal sealed class FunctionParser
{
    private const string Invalid = "Expressão inválida. Verifique os operadores, parênteses e nomes das funções.";
    private readonly string text;
    private int position;
    private int depth;
    private static readonly Dictionary<string, Func<double, double>> Functions = new()
    {
        ["sin"] = Math.Sin, ["sen"] = Math.Sin, ["cos"] = Math.Cos, ["tan"] = Math.Tan, ["tg"] = Math.Tan,
        ["asin"] = Math.Asin, ["acos"] = Math.Acos, ["atan"] = Math.Atan,
        ["exp"] = Math.Exp, ["ln"] = Math.Log, ["log"] = Math.Log10, ["log10"] = Math.Log10,
        ["log2"] = Math.Log2, ["sqrt"] = Math.Sqrt, ["abs"] = Math.Abs
    };
    public FunctionParser(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 1024) throw new ArgumentException(Invalid);
        text = Normalize(expression);
    }
    public FunctionExpression Parse()
    {
        var evaluate = Sum(); SkipSpaces();
        if (position != text.Length) throw new ArgumentException(Invalid);
        return new(text, evaluate);
    }
    private Func<double, double> Sum()
    {
        var result = Product();
        while (true)
        {
            if (Read('+')) { var left = result; var right = Product(); result = x => left(x) + right(x); }
            else if (Read('-')) { var left = result; var right = Product(); result = x => left(x) - right(x); }
            else return result;
        }
    }
    private Func<double, double> Product()
    {
        var result = Unary();
        while (true)
        {
            if (Read('*') || StartsImplicitProduct()) { var left = result; var right = Unary(); result = x => left(x) * right(x); }
            else if (Read('/')) { var left = result; var right = Unary(); result = x => left(x) / right(x); }
            else return result;
        }
    }
    private Func<double, double> Unary()
    {
        if (++depth > 64) throw new ArgumentException(Invalid);
        try
        {
            if (Read('+')) return Unary();
            if (Read('-')) { var operand = Unary(); return x => -operand(x); }
            var value = Atom();
            if (!Read('^')) return value;
            var exponent = Unary(); return x => Math.Pow(value(x), exponent(x));
        }
        finally { depth--; }
    }
    private Func<double, double> Atom()
    {
        SkipSpaces();
        if (Read('(')) { var value = Sum(); Require(')'); return value; }
        if (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.')) return Number();
        var start = position;
        while (position < text.Length && char.IsLetterOrDigit(text[position])) position++;
        var identifier = text[start..position].ToLowerInvariant();
        if (identifier == "x") return x => x;
        if (identifier is "pi" or "π") return _ => Math.PI;
        if (identifier == "e") return _ => Math.E;
        return Call(identifier);
    }
    private Func<double, double> Call(string identifier)
    {
        if (!Functions.TryGetValue(identifier, out var function)) throw new ArgumentException(Invalid);
        Require('('); var argument = Sum();
        if (identifier == "log" && Read(','))
        {
            var basis = Sum(); Require(')'); return x => Math.Log(argument(x), basis(x));
        }
        Require(')'); return x => function(argument(x));
    }
    private Func<double, double> Number()
    {
        var start = position;
        ReadDigits();
        if (position < text.Length && text[position] == '.') { position++; ReadDigits(); }
        if (position < text.Length && text[position] is 'e' or 'E')
        {
            var exponent = position + 1;
            if (exponent < text.Length && text[exponent] is '+' or '-') exponent++;
            if (exponent < text.Length && char.IsDigit(text[exponent])) { position = exponent; ReadDigits(); }
        }
        if (!double.TryParse(text[start..position], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new ArgumentException(Invalid);
        return _ => number;
    }
    private void ReadDigits() { while (position < text.Length && char.IsDigit(text[position])) position++; }
    private void SkipSpaces() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    private bool Read(char character)
    {
        SkipSpaces();
        if (position >= text.Length || text[position] != character) return false;
        position++; return true;
    }
    private void Require(char character) { if (!Read(character)) throw new ArgumentException(Invalid); }
    private bool StartsImplicitProduct()
    {
        SkipSpaces(); return position < text.Length && (text[position] == '(' || char.IsLetter(text[position]));
    }
    private static string Normalize(string expression)
    {
        var text = expression.Trim().Replace('−', '-').Replace('×', '*').Replace('÷', '/');
        var equals = text.IndexOf('=');
        if (equals < 0) return text;
        var prefix = string.Concat(text[..equals].Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();
        if (prefix is not ("f(x)" or "y")) throw new ArgumentException(Invalid);
        return text[(equals + 1)..].Trim();
    }
}
