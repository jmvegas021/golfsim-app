using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds POST bodies for WLED's JSON API. Uses <c>application/json</c> without a charset
/// parameter — several WLED/ESPAsyncWebServer builds do an exact content-type compare and
/// return an empty HTTP 400 when they see <c>application/json; charset=utf-8</c>
/// (what <see cref="System.Net.Http.Json.JsonContent"/> emits by default).
/// </summary>
public static class WledJsonPostContent
{
    private static readonly JsonSerializerOptions SerializerOptions = new();

    public const string MediaType = "application/json";

    public static HttpContent Create(object body, out string json)
    {
        ArgumentNullException.ThrowIfNull(body);
        json = JsonSerializer.Serialize(body, SerializerOptions);
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
        return content;
    }
}
