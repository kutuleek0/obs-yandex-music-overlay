using System.Text.Json.Serialization;

namespace SmtcHelper
{
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(NowPlayingState))]
    internal partial class SmtcJsonContext : JsonSerializerContext
    {
    }
}


