using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>The built-in cucumber-expression parameter kinds the copied steps use.</summary>
internal enum ParameterKind
{
    /// <summary>{int}.</summary>
    Int,

    /// <summary>{float} / {double}.</summary>
    Float,

    /// <summary>{word}.</summary>
    Word,

    /// <summary>{string}: text in double or single quotes (the quotes are removed).</summary>
    String,

    /// <summary>{}: anything.</summary>
    Anonymous,
}

/// <summary>
/// A cucumber expression (the step-definition language of the copied steps) compiled to a
/// regular expression: {int} {float} {double} {word} {string} {} parameters, optional text in
/// parentheses ("pixel(s)"), alternative words separated by a slash, and backslash escapes -
/// the subset Reqnroll implements that the UIReqs steps use.
/// </summary>
internal sealed class CucumberExpression
{
    private CucumberExpression(string source, Regex regex, IReadOnlyList<ParameterKind> parameters)
    {
        Source = source;
        Regex = regex;
        Parameters = parameters;
    }

    /// <summary>The expression text.</summary>
    internal string Source { get; }

    /// <summary>The compiled, anchored regular expression.</summary>
    internal Regex Regex { get; }

    /// <summary>The parameters, in order (one capture group each).</summary>
    internal IReadOnlyList<ParameterKind> Parameters { get; }

    /// <summary>Compiles an expression.</summary>
    internal static CucumberExpression Compile(string expression)
    {
        var pattern = new StringBuilder("^");
        var parameters = new List<ParameterKind>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0)
            {
                return;
            }

            // Alternation: a run of non-space characters containing '/' is a choice of words.
            var text = literal.ToString();
            var token = new StringBuilder();
            foreach (var character in text)
            {
                if (char.IsWhiteSpace(character))
                {
                    AppendToken(pattern, token.ToString());
                    token.Clear();
                    pattern.Append(Regex.Escape(character.ToString()));
                }
                else
                {
                    token.Append(character);
                }
            }

            AppendToken(pattern, token.ToString());
            literal.Clear();
        }

        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c == '\\' && i + 1 < expression.Length)
            {
                literal.Append(expression[++i]);
                continue;
            }

            if (c == '{')
            {
                var end = expression.IndexOf('}', i);
                if (end < 0)
                {
                    throw new FormatException($"Unclosed parameter in \"{expression}\".");
                }

                FlushLiteral();
                var name = expression.Substring(i + 1, end - i - 1);
                var kind = name switch
                {
                    "int" => ParameterKind.Int,
                    "float" or "double" => ParameterKind.Float,
                    "word" => ParameterKind.Word,
                    "string" => ParameterKind.String,
                    "" => ParameterKind.Anonymous,
                    _ => throw new NotSupportedException($"Parameter type {{{name}}} in \"{expression}\" is not supported by the device step runner."),
                };
                parameters.Add(kind);
                pattern.Append(kind switch
                {
                    ParameterKind.Int => @"(-?\d+)",
                    ParameterKind.Float => @"(-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)",
                    ParameterKind.Word => @"([^\s]+)",
                    ParameterKind.String => "(\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*')",
                    _ => "(.*)",
                });
                i = end;
                continue;
            }

            if (c == '(')
            {
                var end = expression.IndexOf(')', i);
                if (end < 0)
                {
                    throw new FormatException($"Unclosed optional text in \"{expression}\".");
                }

                FlushLiteral();
                pattern.Append("(?:").Append(Regex.Escape(expression.Substring(i + 1, end - i - 1))).Append(")?");
                i = end;
                continue;
            }

            literal.Append(c);
        }

        FlushLiteral();
        pattern.Append('$');
        return new CucumberExpression(expression, new Regex(pattern.ToString(), RegexOptions.CultureInvariant), parameters);
    }

    /// <summary>Matches step text; returns the raw parameter texts (string quotes removed) or null.</summary>
    internal string[]? Match(string text)
    {
        var match = Regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var values = new string[Parameters.Count];
        for (var i = 0; i < Parameters.Count; i++)
        {
            var value = match.Groups[i + 1].Value;
            if (Parameters[i] == ParameterKind.String && value.Length >= 2)
            {
                value = value.Substring(1, value.Length - 2).Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\'", "'", StringComparison.Ordinal);
            }

            values[i] = value;
        }

        return values;
    }

    /// <summary>Parses a {float} text.</summary>
    internal static double ParseFloat(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void AppendToken(StringBuilder pattern, string token)
    {
        if (token.Length == 0)
        {
            return;
        }

        if (token.Contains('/'))
        {
            var choices = token.Split('/');
            pattern.Append("(?:");
            for (var i = 0; i < choices.Length; i++)
            {
                if (i > 0)
                {
                    pattern.Append('|');
                }

                pattern.Append(Regex.Escape(choices[i]));
            }

            pattern.Append(')');
        }
        else
        {
            pattern.Append(Regex.Escape(token));
        }
    }
}
