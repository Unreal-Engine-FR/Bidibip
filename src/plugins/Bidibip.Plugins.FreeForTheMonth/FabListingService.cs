using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bidibip.Plugins.FreeForTheMonth;

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

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagsRegex();

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

internal static class FabListingService
{
    private const string ApiUrl = "https://www.fab.com/i/blades/free_content_blade";

    public static async Task<List<FabListing>> FetchListingsAsync()
    {
        // Fab.com uses Cloudflare with TLS fingerprinting that blocks .NET's HttpClient.
        // Shell out to curl which has a trusted TLS fingerprint.
        var psi = new ProcessStartInfo("curl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add("-L");
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("Accept: application/json");
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("Accept-Language: en-US,en;q=0.9");
        psi.ArgumentList.Add(ApiUrl);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start curl");

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

        if (!doc.RootElement.TryGetProperty("tiles", out var tiles))
            return results;

        foreach (var tile in tiles.EnumerateArray())
        {
            if (!tile.TryGetProperty("listing", out var listing))
                continue;

            var uid = listing.GetProperty("uid").GetString() ?? "";
            var title = listing.GetProperty("title").GetString() ?? "";
            var listingType = listing.TryGetProperty("listingType", out var lt) ? lt.GetString() ?? "" : "";

            // Seller
            var seller = "";
            string? sellerAvatarUrl = null;
            if (listing.TryGetProperty("user", out var user))
            {
                seller = user.TryGetProperty("sellerName", out var sn) ? sn.GetString() ?? "" : "";
                sellerAvatarUrl = user.TryGetProperty("profileImageUrl", out var av) ? av.GetString() : null;
            }

            // Price (USD) & discount end
            decimal startingPriceUsd = 0;
            DateTimeOffset? discountEnd = null;
            if (listing.TryGetProperty("startingPrice", out var sp) && sp.TryGetProperty("price", out var spv))
                startingPriceUsd = spv.GetDecimal();
            if (listing.TryGetProperty("licenses", out var licenses))
            {
                foreach (var license in licenses.EnumerateArray())
                {
                    if (license.TryGetProperty("priceTier", out var pt) &&
                        pt.TryGetProperty("discountEndDate", out var de))
                        discountEnd = DateTimeOffset.Parse(de.GetString()!);
                    break;
                }
            }

            // Description snippet
            var descHtml = listing.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var snippet = FabListing.ExtractSnippet(descHtml);

            // Review count
            var reviewCount = listing.TryGetProperty("reviewCount", out var rc) ? rc.GetInt32() : 0;

            // Image (pick large ~960px for embed image)
            string? imageUrl = null;
            if (listing.TryGetProperty("thumbnails", out var thumbs))
            {
                foreach (var thumb in thumbs.EnumerateArray())
                {
                    if (!thumb.TryGetProperty("images", out var images)) continue;
                    string? best = null;
                    int bestWidth = 0;
                    foreach (var img in images.EnumerateArray())
                    {
                        var url = img.TryGetProperty("url", out var u) ? u.GetString() : null;
                        var width = img.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
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
            if (listing.TryGetProperty("ratings", out var ratings))
            {
                if (ratings.TryGetProperty("averageRating", out var ar)) avgRating = ar.GetDouble();
                if (ratings.TryGetProperty("total", out var tr)) totalRatings = tr.GetInt32();
            }

            // Formats
            var formats = new List<string>();
            if (listing.TryGetProperty("assetFormats", out var af))
            {
                foreach (var fmt in af.EnumerateArray())
                {
                    if (fmt.TryGetProperty("assetFormatType", out var aft) &&
                        aft.TryGetProperty("name", out var name))
                        formats.Add(name.GetString() ?? "");
                }
            }

            results.Add(new FabListing
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
            });
        }

        return results;
    }
}
