using System.Text.Json.Serialization;
using System.Collections.Generic;
using DigitalWellbeing.Core.Models;

namespace DigitalWellbeing.Core.Contexts
{
    [JsonSerializable(typeof(List<AppSession>))]
    [JsonSerializable(typeof(AppSession))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
