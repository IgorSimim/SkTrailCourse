using System.Text.Json;

namespace SkTrailCourse.Infra;

public class JsonMemoryStore
{
    private readonly string _folder;
    private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public JsonMemoryStore(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    private string PathFor(string key) => System.IO.Path.Combine(_folder, $"{key}.json");

    public async Task<List<T>> LoadListAsync<T>(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return new List<T>();
        using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(fs) ?? new List<T>();
    }

    public async Task SaveListAsync<T>(string key, List<T> items)
    {
        var path = PathFor(key);
        using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, items, _opts);
    }
}
