#nullable enable
using System.Text.Json.Serialization;
using System.Collections.Generic;
using UsageLogger.Core.Models;

namespace UsageLogger.Core.Contexts
{
    [JsonSerializable(typeof(List<AppSession>))]
    [JsonSerializable(typeof(AppSession))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
