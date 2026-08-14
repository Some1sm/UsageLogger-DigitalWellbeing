#nullable enable
using System.Text.Json.Serialization;
using System.Collections.Generic;
using UsageLogger.Core.Models;

namespace UsageLogger.Core.Contexts
{
    [JsonSerializable(typeof(List<AppSession>))]
    [JsonSerializable(typeof(AppSession))]
    [JsonSerializable(typeof(List<CustomTitleRule>))]
    [JsonSerializable(typeof(CustomTitleRule))]
    [JsonSerializable(typeof(List<AppUsage>))]
    [JsonSerializable(typeof(AppUsage))]
    [JsonSerializable(typeof(AppTag))]
    [JsonSerializable(typeof(Dictionary<string, AppTag>))]
    [JsonSerializable(typeof(PowerSnapshot))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
