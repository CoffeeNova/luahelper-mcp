using System.Text.Json;
using System.Text.Json.Serialization;
using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Serialization;

[JsonSerializable(typeof(LuaHelperConfig))]
[JsonSerializable(typeof(List<LuaDiagnostic>))]
[JsonSerializable(typeof(SupportedCheck[]))]
[JsonSerializable(typeof(LuahelperJsonTemplate))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class LspJsonContext : JsonSerializerContext;

internal static class LspJson
{
    public static readonly LspJsonContext Default = LspJsonContext.Default;

    public static readonly LspJsonContext Indented = new(
        new JsonSerializerOptions { WriteIndented = true }
    );

    public static readonly LspJsonContext IndentedCamelCase = new(
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }
    );
}
