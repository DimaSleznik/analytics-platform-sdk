using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AnalyticsPlatform
{

public static class StableJson
{
    public static string Stringify(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return Quote(text);
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return Quote(dateTimeOffset.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        }

        if (value is DateTime dateTime)
        {
            return Quote(dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        if (value is IDictionary dictionary)
        {
            return ObjectString(dictionary.Keys.Cast<object>().Select(key => (
                Key: Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty,
                Value: dictionary[key])));
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return $"[{string.Join(",", enumerable.Cast<object?>().Select(Stringify))}]";
        }

        if (IsNumber(value))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        }

        var properties = value.GetType().GetProperties();
        if (properties.Length > 0)
        {
            return ObjectString(properties.Select(property => (property.Name, property.GetValue(value))));
        }

        return Quote(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    public static string ObjectString(IEnumerable<(string Key, object? Value)> entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        foreach (var entry in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(Quote(entry.Key));
            builder.Append(':');
            builder.Append(Stringify(entry.Value));
            first = false;
        }

        builder.Append('}');
        return builder.ToString();
    }

    public static object? Parse(string json)
    {
        return new Parser(json).Parse();
    }

    private static string Quote(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (ch < 32)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static bool IsNumber(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private sealed class Parser
    {
        private readonly string _json;
        private int _index;

        public Parser(string json)
        {
            _json = json;
        }

        public object? Parse()
        {
            var value = ParseValue();
            SkipWhitespace();
            return value;
        }

        private object? ParseValue()
        {
            SkipWhitespace();
            if (_index >= _json.Length)
            {
                return null;
            }

            return _json[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                't' => ReadLiteral("true", true),
                'f' => ReadLiteral("false", false),
                'n' => ReadLiteral("null", null),
                _ => ParseNumber(),
            };
        }

        private Dictionary<string, object?> ParseObject()
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            _index++;
            SkipWhitespace();
            if (TryRead('}'))
            {
                return result;
            }

            while (_index < _json.Length)
            {
                var key = ParseString();
                SkipWhitespace();
                TryRead(':');
                result[key] = ParseValue();
                SkipWhitespace();
                if (TryRead('}'))
                {
                    return result;
                }

                TryRead(',');
            }

            return result;
        }

        private List<object?> ParseArray()
        {
            var result = new List<object?>();
            _index++;
            SkipWhitespace();
            if (TryRead(']'))
            {
                return result;
            }

            while (_index < _json.Length)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryRead(']'))
                {
                    return result;
                }

                TryRead(',');
            }

            return result;
        }

        private string ParseString()
        {
            var builder = new StringBuilder();
            TryRead('"');
            while (_index < _json.Length)
            {
                var ch = _json[_index++];
                if (ch == '"')
                {
                    return builder.ToString();
                }

                if (ch != '\\' || _index >= _json.Length)
                {
                    builder.Append(ch);
                    continue;
                }

                var escaped = _json[_index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ParseUnicode());
                        break;
                }
            }

            return builder.ToString();
        }

        private char ParseUnicode()
        {
            if (_index + 4 > _json.Length)
            {
                return '\0';
            }

            var hex = _json.Substring(_index, 4);
            _index += 4;
            return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private object ReadLiteral(string literal, object? value)
        {
            _index += literal.Length;
            return value!;
        }

        private double ParseNumber()
        {
            var start = _index;
            while (_index < _json.Length && "-+0123456789.eE".IndexOf(_json[_index]) >= 0)
            {
                _index++;
            }

            var text = _json.Substring(start, _index - start);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private bool TryRead(char expected)
        {
            SkipWhitespace();
            if (_index >= _json.Length || _json[_index] != expected)
            {
                return false;
            }

            _index++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
            {
                _index++;
            }
        }
    }
}
}
