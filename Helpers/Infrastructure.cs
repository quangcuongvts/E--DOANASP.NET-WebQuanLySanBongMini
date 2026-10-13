namespace MiniFootballBooking.Helpers;

public class ImageStorage
{
    private readonly IWebHostEnvironment _env;

    public ImageStorage(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(bool Ok, string Path, string Error)> SaveAsync(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0)
        {
            return (false, string.Empty, "Chưa chọn tệp ảnh.");
        }

        if (file.Length > AppConstants.MaxImageBytes)
        {
            return (false, string.Empty, "Ảnh vượt quá 2MB.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AppConstants.ImageExtensions.Contains(ext))
        {
            return (false, string.Empty, "Chỉ chấp nhận ảnh jpg, jpeg, png, webp, gif.");
        }

        var directory = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var physical = Path.Combine(directory, fileName);
        await using var stream = new FileStream(physical, FileMode.Create);
        await file.CopyToAsync(stream);
        return (true, $"/uploads/{folder}/{fileName}", string.Empty);
    }
}

public class SettingsReader
{
    private readonly Data.ApplicationDbContext _db;

    public SettingsReader(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetAsync(string key, string fallback)
    {
        var value = await _db.SystemSettings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public async Task<int> GetIntAsync(string key, int fallback)
    {
        var value = await GetAsync(key, fallback.ToString());
        return int.TryParse(value, out var number) ? number : fallback;
    }

    public async Task<Dictionary<string, string>> AllAsync()
    {
        return await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}
