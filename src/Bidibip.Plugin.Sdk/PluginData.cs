using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Shared helper for loading and saving plugin JSON data files.
/// On load, missing properties (new fields added to the C# model) are
/// automatically written back to the file with their default values.
/// </summary>
public static class PluginData
{
    /// <summary>
    /// Loads data of type <typeparamref name="T"/> from <paramref name="filePath"/>.
    /// If the file does not exist, creates it with a default <typeparamref name="T"/>.
    /// If the file exists but has missing properties compared to the model, those
    /// properties are added with their default values and the file is rewritten.
    /// </summary>
    public static async Task<T> LoadAsync<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
        {
            var defaultInstance = new T();
            await SaveAsync(filePath, defaultInstance);
            return defaultInstance;
        }

        var json = await File.ReadAllTextAsync(filePath);
        var loaded = JsonSerializer.Deserialize<T>(json, PluginJsonOptions.Default) ?? new T();

        // Merge missing properties from a default instance into the file
        var fileNode = JsonNode.Parse(json);
        var defaultNode = JsonSerializer.SerializeToNode(new T(), PluginJsonOptions.Default);

        if (fileNode is JsonObject fileObj && defaultNode is JsonObject defaultObj)
        {
            if (MergeDefaults(fileObj, defaultObj))
            {
                var merged = fileObj.ToJsonString(PluginJsonOptions.Default);
                await File.WriteAllTextAsync(filePath, merged);
            }
        }

        return loaded;
    }

    /// <summary>
    /// Serializes <paramref name="data"/> and writes it to <paramref name="filePath"/>.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    public static async Task SaveAsync<T>(string filePath, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(data, PluginJsonOptions.Default);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Recursively adds properties from <paramref name="defaults"/> that are
    /// missing in <paramref name="target"/>. Returns true if anything was added.
    /// </summary>
    private static bool MergeDefaults(JsonObject target, JsonObject defaults)
    {
        var modified = false;

        foreach (var (key, defaultValue) in defaults)
        {
            if (!target.ContainsKey(key))
            {
                target[key] = defaultValue?.DeepClone();
                modified = true;
            }
            else if (target[key] is JsonObject targetChild && defaultValue is JsonObject defaultChild)
            {
                modified |= MergeDefaults(targetChild, defaultChild);
            }
        }

        return modified;
    }
}
