using System.Diagnostics;
using System.Text.Json;

namespace Bidibip.Plugins.FreeForTheMonth;

internal sealed class FabListing
{
    public required string Uid { get; init; }
    public required string Title { get; init; }
    public required string Seller { get; init; }
    public required string ListingType { get; init; }
    public required decimal OriginalPrice { get; init; }
    public required DateTimeOffset? DiscountEnd { get; init; }
    public required string? ThumbnailUrl { get; init; }
    public required double AverageRating { get; init; }
    public required int TotalRatings { get; init; }
    public required List<string> Formats { get; init; }

    public string Url => $"https://www.fab.com/listings/{Uid}";
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

            var seller = "";
            if (listing.TryGetProperty("user", out var user))
                seller = user.TryGetProperty("sellerName", out var sn) ? sn.GetString() ?? "" : "";

            decimal originalPrice = 0;
            DateTimeOffset? discountEnd = null;
            if (listing.TryGetProperty("licenses", out var licenses))
            {
                foreach (var license in licenses.EnumerateArray())
                {
                    if (!license.TryGetProperty("priceTier", out var pt)) continue;
                    if (pt.TryGetProperty("price", out var p)) originalPrice = p.GetDecimal();
                    if (pt.TryGetProperty("discountEndDate", out var de))
                        discountEnd = DateTimeOffset.Parse(de.GetString()!);
                    break;
                }
            }

            string? thumbnailUrl = null;
            if (listing.TryGetProperty("thumbnails", out var thumbs))
            {
                foreach (var thumb in thumbs.EnumerateArray())
                {
                    if (!thumb.TryGetProperty("images", out var images)) continue;
                    foreach (var img in images.EnumerateArray())
                    {
                        var url = img.TryGetProperty("url", out var u) ? u.GetString() : null;
                        var width = img.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                        if (url is not null && (thumbnailUrl is null || width is >= 320 and <= 800))
                            thumbnailUrl = url;
                    }
                }
            }

            double avgRating = 0;
            int totalRatings = 0;
            if (listing.TryGetProperty("ratings", out var ratings))
            {
                if (ratings.TryGetProperty("averageRating", out var ar)) avgRating = ar.GetDouble();
                if (ratings.TryGetProperty("total", out var tr)) totalRatings = tr.GetInt32();
            }

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
                ListingType = listingType,
                OriginalPrice = originalPrice,
                DiscountEnd = discountEnd,
                ThumbnailUrl = thumbnailUrl,
                AverageRating = avgRating,
                TotalRatings = totalRatings,
                Formats = formats
            });
        }

        return results;
    }
}
