using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SportsCenterManagement.API.Localization;

/// <summary>
/// When the client sends <c>Accept-Language: en</c>, rewrites user-facing message fields
/// (message, title, detail, error, errors, proposedHandling, warning) of JSON API responses
/// into English. Business data is never touched: only those keys are translated, and only
/// when the text is a known API message.
/// </summary>
public sealed class ResponseMessageLocalizationMiddleware(RequestDelegate next)
{
    private const long MaxBodyBytes = 1024 * 1024;

    private static readonly HashSet<string> MessageKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "message", "title", "detail", "error", "errors", "proposedHandling", "warning"
    };

    // Writer-based output: no reflection serializer needed (works with trimming/AOT too).
    private static readonly JsonWriterOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || !ApiMessageLocalizer.PrefersEnglish(context.Request))
        {
            await next(context);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        var contentType = context.Response.ContentType ?? string.Empty;
        if (buffer.Length > 0 && buffer.Length <= MaxBodyBytes
            && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            JsonNode? root = null;
            try
            {
                root = JsonNode.Parse(buffer);
            }
            catch (JsonException)
            {
                // Not valid JSON; pass the body through unchanged.
            }

            if (root is not null && Translate(root, insideMessage: false))
            {
                using var output = new MemoryStream();
                await using (var writer = new Utf8JsonWriter(output, WriteOptions))
                {
                    root.WriteTo(writer);
                }
                context.Response.ContentLength = output.Length;
                output.Position = 0;
                await output.CopyToAsync(original, context.RequestAborted);
                return;
            }
            buffer.Position = 0;
        }

        await buffer.CopyToAsync(original, context.RequestAborted);
    }

    /// <summary>Returns true when at least one string was replaced.</summary>
    private static bool Translate(JsonNode node, bool insideMessage)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is null) continue;
                    var isMessage = insideMessage || MessageKeys.Contains(key);
                    if (value is JsonValue leaf)
                    {
                        if (isMessage && TryReplace(leaf, out var english))
                        {
                            obj[key] = english;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= Translate(value, isMessage);
                    }
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var item = array[i];
                    if (item is null) continue;
                    if (item is JsonValue leaf)
                    {
                        if (insideMessage && TryReplace(leaf, out var english))
                        {
                            array[i] = english;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= Translate(item, insideMessage);
                    }
                }
                break;
        }
        return changed;
    }

    private static bool TryReplace(JsonValue leaf, out string english)
    {
        english = string.Empty;
        return leaf.TryGetValue<string>(out var text) && ApiMessageLocalizer.TryTranslate(text, out english);
    }
}
