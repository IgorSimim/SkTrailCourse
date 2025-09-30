using System.Text.Json;

namespace SkTrailCourse.Infra;

public class JsonMemoryStore
{
    private readonly string _dataDirectory;

    public JsonMemoryStore(string dataDirectory = "data")
    {
        _dataDirectory = dataDirectory;
        if (!Directory.Exists(_dataDirectory))
            Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<List<T>> LoadListAsync<T>(string key)
    {
        var filePath = Path.Combine(_dataDirectory, $"{key}.json");
        if (!File.Exists(filePath))
            return new List<T>();

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    public async Task SaveListAsync<T>(string key, List<T> list)
    {
        var filePath = Path.Combine(_dataDirectory, $"{key}.json");
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
}