// ──────────────────────────────────────────────────────────────────────────────
// FabListingService.cs — Fetches free assets from the Fab.com marketplace
//
// Fab.com (formerly Unreal Engine Marketplace) offers free assets each month.
// This service fetches the current list from Fab's internal API and parses the
// JSON response into FabListing objects.
//
// Why curl-impersonate instead of HttpClient?
//   Fab.com sits behind Cloudflare which uses TLS fingerprinting to block
//   automated requests. .NET's HttpClient and standard curl on Linux produce
//   TLS fingerprints that Cloudflare detects as non-browser traffic. The
//   curl-impersonate tool mimics Chrome's exact TLS handshake (cipher suites,
//   extensions, ALPN) to bypass this check.
//
// Why shell out to a process?
//   There's no .NET library that can replicate Chrome's TLS fingerprint.
//   curl-impersonate is a C binary with its own BoringSSL build, so we must
//   invoke it as an external process.
// ──────────────────────────────────────────────────────────────────────────────

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.FreeForTheMonth;

/// <summary>
/// Represents a single free asset listing from Fab.com, with all the metadata
/// needed to build a Discord embed (title, image, rating, seller info, etc.).
/// </summary>
internal sealed partial class FabListing
{
    public required string Uid { get; init; }
    public required string Title { get; init; }
    public required string Seller { get; init; }
    public required string? SellerAvatarUrl { get; init; }
    public required string ListingType { get; init; }
    public required decimal StartingPriceUsd { get; init; }
    public required DateTimeOffset? DiscountEnd { get; init; }
    public required string? ImageUrl { get; init; }
    public required double AverageRating { get; init; }
    public required int TotalRatings { get; init; }
    public required int ReviewCount { get; init; }
    public required string? DescriptionSnippet { get; init; }
    public required List<string> Formats { get; init; }

    public string Url => $"https://www.fab.com/listings/{Uid}";

    public string DisplayType => ListingType switch
    {
        "3d-model" => "3D Model",
        "game-system" => "Game System",
        "environment" => "Environment",
        "material" => "Material",
        "texture" => "Texture",
        "audio" => "Audio",
        "hdri" => "HDRI",
        "decal" => "Decal",
        _ => ListingType.Replace('-', ' ')
    };

    /// <summary>
    /// Renders a star rating string for Discord embeds.
    /// Example: "★★★★☆ **4.2**/5 (128 votes, 45 reviews)"
    /// </summary>
    public string StarsDisplay
    {
        get
        {
            if (TotalRatings == 0) return "Pas encore note";
            var full = (int)Math.Round(AverageRating);
            var stars = new string('\u2605', full) + new string('\u2606', 5 - full);
            var info = ReviewCount > 0
                ? $"{TotalRatings} votes, {ReviewCount} reviews"
                : $"{TotalRatings} votes";
            return $"{stars} **{AverageRating:F1}**/5 ({info})";
        }
    }

    /// <summary>Compiled regex for stripping HTML tags from descriptions.</summary>
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagsRegex();

    /// <summary>
    /// Strips HTML tags from a description and truncates to a readable snippet.
    /// Fab's API returns descriptions as HTML, but Discord embeds need plain text.
    /// </summary>
    public static string? ExtractSnippet(string? html, int maxLength = 150)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = HtmlTagsRegex().Replace(html, " ");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= maxLength) return text;
        return text[..maxLength].TrimEnd() + "...";
    }
}

/// <summary>
/// Fetches the current month's free asset listings from Fab.com's internal API.
/// Uses curl-impersonate to bypass Cloudflare's TLS fingerprinting.
/// </summary>
internal static class FabListingService
{
    /// <summary>
    /// Fab's internal "blade" endpoint that returns the free content listings.
    /// This is the same endpoint the Fab.com website calls when rendering the
    /// "Free for the month" section on the homepage.
    /// </summary>
    private const string ApiUrl = "https://www.fab.com/i/blades/free_content_blade";

    /// <summary>
    /// Fetches and parses the current free listings. The response JSON has this structure:
    /// <code>
    /// {
    ///   "tiles": [
    ///     { "listing": { "uid": "...", "title": "...", "user": {...}, "ratings": {...}, ... } },
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static async Task<List<FabListing>> FetchListingsAsync()
    {
        // Fab.com uses Cloudflare with TLS fingerprinting that blocks .NET's HttpClient
        // and standard curl on Linux. Use curl-impersonate (Chrome profile) to bypass.
        // The executable is overridable via the FFM_CURL_IMPERSONATE env var (loaded from
        // .env): the Docker image installs `curl_chrome116` on PATH, while a local dev box
        // can point it at a curl-impersonate build (e.g. C:\tools\curl_chrome116.exe).
        var curlExe = Environment.GetEnvironmentVariable("FFM_CURL_IMPERSONATE");
        if (string.IsNullOrWhiteSpace(curlExe))
            curlExe = "curl_chrome116";

        var psi = new ProcessStartInfo(curlExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-s");         // Silent mode (no progress bar)
        psi.ArgumentList.Add("-L");         // Follow redirects
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("Accept: application/json"); // Request JSON (not HTML)
        psi.ArgumentList.Add(ApiUrl);

        using var process = StartCurlImpersonate(psi, curlExe);

        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"curl failed (exit {process.ExitCode}): {error}");
        }

        if (!json.TrimStart().StartsWith('{'))
            throw new InvalidOperationException($"Fab returned non-JSON response (first 200 chars): {json[..Math.Min(json.Length, 200)]}");

        using var doc = JsonDocument.Parse(json);

        var results = new List<FabListing>();

        if (!doc.RootElement.TryGetProp("tiles", out var tiles))
            return results;

        foreach (var tile in tiles.EnumerateArrayOrEmpty())
        {
            if (!tile.TryGetProp("listing", out var listing))
                continue;

            try
            {
                if (ParseListing(listing) is { } parsed)
                    results.Add(parsed);
            }
            catch (Exception ex)
            {
                // Fab's API is external and undocumented; a single listing with an
                // unexpected shape must not abort (and thus drop) the whole batch.
                FreeForTheMonthPlugin.Logger.LogWarning(ex, "FreeForTheMonth: skipped a malformed listing");
            }
        }

        return results;
    }

    /// <summary>
    /// Starts the curl-impersonate process, turning the "executable not found" failure
    /// (Win32Exception) into a clear, actionable message instead of the raw OS error.
    /// </summary>
    private static Process StartCurlImpersonate(ProcessStartInfo psi, string curlExe)
    {
        try
        {
            return Process.Start(psi)
                ?? throw new InvalidOperationException("curl-impersonate process could not be started.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"curl-impersonate executable '{curlExe}' was not found. It is required to bypass " +
                "Cloudflare's TLS fingerprinting when reaching Fab. The Docker image installs it on " +
                "PATH automatically; for local runs install a curl-impersonate build and add it to " +
                "PATH, or set FFM_CURL_IMPERSONATE (in .env) to the executable's full path.", ex);
        }
    }

    /// <summary>
    /// Parses one <c>listing</c> JSON object into a <see cref="FabListing"/>, or returns null
    /// when it has no usable uid. Every access goes through the null-safe helpers in
    /// <see cref="JsonElementExtensions"/>: Fab regularly returns explicit nulls for optional
    /// fields (e.g. <c>"user": null</c>, <c>"ratings": null</c>, <c>"startingPrice": null</c>),
    /// and the raw JsonElement accessors throw on those instead of reporting "absent".
    /// </summary>
    private static FabListing? ParseListing(JsonElement listing)
    {
        var uid = listing.TryGetProp("uid", out var uidEl) ? uidEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(uid))
            return null;

        var title = listing.TryGetProp("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
        var listingType = listing.TryGetProp("listingType", out var lt) ? lt.GetString() ?? "" : "";

        // Seller
        var seller = "";
        string? sellerAvatarUrl = null;
        if (listing.TryGetProp("user", out var user))
        {
            seller = user.TryGetProp("sellerName", out var sn) ? sn.GetString() ?? "" : "";
            sellerAvatarUrl = user.TryGetProp("profileImageUrl", out var av) ? av.GetString() : null;
        }

        // Price (USD) & discount end
        decimal startingPriceUsd = 0;
        DateTimeOffset? discountEnd = null;
        if (listing.TryGetProp("startingPrice", out var sp) && sp.TryGetProp("price", out var spv))
            startingPriceUsd = spv.GetDecimal();
        if (listing.TryGetProp("licenses", out var licenses))
        {
            foreach (var license in licenses.EnumerateArrayOrEmpty())
            {
                if (license.TryGetProp("priceTier", out var pt) &&
                    pt.TryGetProp("discountEndDate", out var de) &&
                    DateTimeOffset.TryParse(de.GetString(), out var parsedEnd))
                    discountEnd = parsedEnd;
                break;
            }
        }

        // Description snippet
        var descHtml = listing.TryGetProp("description", out var desc) ? desc.GetString() : null;
        var snippet = FabListing.ExtractSnippet(descHtml);

        // Review count
        var reviewCount = listing.TryGetProp("reviewCount", out var rc) ? rc.GetInt32() : 0;

        // Image (pick large ~960px for embed image)
        string? imageUrl = null;
        if (listing.TryGetProp("thumbnails", out var thumbs))
        {
            foreach (var thumb in thumbs.EnumerateArrayOrEmpty())
            {
                if (!thumb.TryGetProp("images", out var images)) continue;
                string? best = null;
                int bestWidth = 0;
                foreach (var img in images.EnumerateArrayOrEmpty())
                {
                    var url = img.TryGetProp("url", out var u) ? u.GetString() : null;
                    var width = img.TryGetProp("width", out var w) ? w.GetInt32() : 0;
                    if (url is null) continue;
                    // Prefer ~960px for a good large embed image
                    if (best is null || (width <= 960 && width > bestWidth) || (bestWidth > 960 && width < bestWidth))
                    {
                        best = url;
                        bestWidth = width;
                    }
                }
                imageUrl = best;
            }
        }

        // Ratings
        double avgRating = 0;
        int totalRatings = 0;
        if (listing.TryGetProp("ratings", out var ratings))
        {
            if (ratings.TryGetProp("averageRating", out var ar)) avgRating = ar.GetDouble();
            if (ratings.TryGetProp("total", out var tr)) totalRatings = tr.GetInt32();
        }

        // Formats
        var formats = new List<string>();
        if (listing.TryGetProp("assetFormats", out var af))
        {
            foreach (var fmt in af.EnumerateArrayOrEmpty())
            {
                if (fmt.TryGetProp("assetFormatType", out var aft) &&
                    aft.TryGetProp("name", out var name))
                    formats.Add(name.GetString() ?? "");
            }
        }

        return new FabListing
        {
            Uid = uid,
            Title = title,
            Seller = seller,
            SellerAvatarUrl = sellerAvatarUrl,
            ListingType = listingType,
            StartingPriceUsd = startingPriceUsd,
            DiscountEnd = discountEnd,
            ImageUrl = imageUrl,
            AverageRating = avgRating,
            TotalRatings = totalRatings,
            ReviewCount = reviewCount,
            DescriptionSnippet = snippet,
            Formats = formats
        };
    }
}

/// <summary>
/// Null-safe navigation helpers for <see cref="JsonElement"/>.
///
/// The built-in <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> and
/// <see cref="JsonElement.EnumerateArray"/> THROW an <see cref="InvalidOperationException"/>
/// ("The requested operation requires an element of type 'Object'/'Array', but the target
/// element has type 'Null'.") when the element is not an object/array — which includes the
/// common case of an explicit JSON null. Fab returns nulls for optional fields, so the raw
/// accessors crashed the whole fetch. These helpers report "nothing here" instead.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Returns true (with the value) only when <paramref name="element"/> is a JSON object
    /// containing <paramref name="name"/> with a non-null value; otherwise false. Unlike the
    /// built-in TryGetProperty it never throws on non-object elements, and it reports an
    /// explicit JSON null as absent so callers never invoke Get*/Parse on a null leaf.
    /// </summary>
    public static bool TryGetProp(this JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out value) &&
            value.ValueKind != JsonValueKind.Null)
            return true;

        value = default;
        return false;
    }

    /// <summary>
    /// Enumerates <paramref name="element"/> as a JSON array, or yields nothing when it is not
    /// an array (null, object, scalar, or absent) — instead of throwing.
    /// </summary>
    public static IEnumerable<JsonElement> EnumerateArrayOrEmpty(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];
        return element.EnumerateArray();
    }
}
