using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AnalyticsPlatform.Editor
{

public sealed class AnalyticsCodegenWindow : EditorWindow
{
    private string endpoint = "http://localhost:3000/api";
    private string token = "";
    private string projectId = "";
    private string outputPath = "Assets/Generated/AnalyticsEvents.cs";

    [MenuItem("Tools/Analytics/Generate Event Builders")]
    public static void Open()
    {
        GetWindow<AnalyticsCodegenWindow>("Analytics Codegen");
    }

    private void OnGUI()
    {
        endpoint = EditorGUILayout.TextField("Endpoint", endpoint);
        projectId = EditorGUILayout.TextField("Project Id", projectId);
        token = EditorGUILayout.PasswordField("Bearer Token", token);
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(token)))
        {
            if (GUILayout.Button("Generate"))
            {
                _ = GenerateAsync();
            }
        }
    }

    private async Task GenerateAsync()
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint.TrimEnd('/')}/v1/projects/{projectId}/schemas");
        request.Headers.TryAddWithoutValidation("authorization", $"Bearer {token}");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var schemas = ParseSchemas(await response.Content.ReadAsStringAsync());
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Assets");
        await File.WriteAllTextAsync(outputPath, Render(schemas), Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    private static string Render(IReadOnlyCollection<EventSchemaDto> schemas)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using AnalyticsPlatform.Unity;");
        builder.AppendLine();
        builder.AppendLine("namespace Generated");
        builder.AppendLine("{");
        builder.AppendLine("public static class AnalyticsEvents");
        builder.AppendLine("{");
        foreach (var schema in schemas)
        {
            var method = ToPascal(schema.EventName);
            var typeName = $"{method}Properties";
            builder.AppendLine($"    public readonly struct {typeName}");
            builder.AppendLine("    {");
            foreach (var property in schema.Properties)
            {
                builder.AppendLine($"        public {ToCsharpType(property.Value)} {ToPascal(property.Key)} {{ get; init; }}");
            }

            builder.AppendLine();
            builder.AppendLine("        public Dictionary<string, object?> ToDictionary()");
            builder.AppendLine("        {");
            builder.AppendLine("            return new Dictionary<string, object?>");
            builder.AppendLine("            {");
            foreach (var property in schema.Properties)
            {
                builder.AppendLine($"                [\"{property.Key}\"] = {ToPascal(property.Key)},");
            }

            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine($"    public static bool {method}({typeName} properties)");
            builder.AppendLine("    {");
            builder.AppendLine($"        return Analytics.Track(\"{schema.EventName}\", properties.ToDictionary());");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static IReadOnlyCollection<EventSchemaDto> ParseSchemas(string json)
    {
        if (StableJson.Parse(json) is not List<object?> items)
        {
            return Array.Empty<EventSchemaDto>();
        }

        var schemas = new List<EventSchemaDto>();
        foreach (var item in items)
        {
            if (item is not Dictionary<string, object?> map ||
                !map.TryGetValue("eventName", out var eventName) ||
                eventName is not string value)
            {
                continue;
            }

            schemas.Add(new EventSchemaDto
            {
                EventName = value,
                Properties = ParseProperties(map),
            });
        }

        return schemas;
    }

    private static Dictionary<string, Dictionary<string, object?>> ParseProperties(Dictionary<string, object?> schema)
    {
        if (!schema.TryGetValue("jsonSchema", out var jsonSchema) ||
            jsonSchema is not Dictionary<string, object?> jsonMap ||
            !jsonMap.TryGetValue("properties", out var properties) ||
            properties is not Dictionary<string, object?> propertyMap)
        {
            return new Dictionary<string, Dictionary<string, object?>>();
        }

        var result = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var property in propertyMap)
        {
            if (property.Value is Dictionary<string, object?> definition)
            {
                result[property.Key] = definition;
            }
        }

        return result;
    }

    private static string ToCsharpType(Dictionary<string, object?> property)
    {
        var type = property.TryGetValue("type", out var value) ? value as string : null;
        return type switch
        {
            "integer" => "int",
            "number" => "double",
            "boolean" => "bool",
            _ => "string",
        };
    }

    private static string ToPascal(string value)
    {
        var builder = new StringBuilder();
        var upper = true;
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(ch) : ch);
            upper = false;
        }

        return builder.Length == 0 ? "Track" : builder.ToString();
    }

    private sealed class EventSchemaDto
    {
        public string EventName { get; set; } = "";
        public Dictionary<string, Dictionary<string, object?>> Properties { get; set; } = new Dictionary<string, Dictionary<string, object?>>();
    }
}
}
