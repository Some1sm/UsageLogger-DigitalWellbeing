using System.Text.Json.Serialization;
using System.Collections.Generic;
using DigitalWellbeing.Core.Models;

namespace DigitalWellbeing.Core.Contexts
{
    [JsonSerializable(typeof(List<AppSession>))]
    [JsonSerializable(typeof(AppSession))]
    [JsonSerializable(typeof(System.Text.Json.JsonElement))]
    [JsonSerializable(typeof(System.TimeSpan))]
    [JsonSerializable(typeof(System.Collections.Generic.List<string>))]
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, int>))]
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, string>))]
    [JsonSerializable(typeof(System.Collections.Generic.List<DigitalWellbeing.Core.Models.CustomAppTag>))]
    [JsonSerializable(typeof(System.Collections.Generic.List<DigitalWellbeing.Core.Models.FocusSession>))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
