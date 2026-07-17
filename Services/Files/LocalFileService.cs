using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.Storage;

public class LocalFileService
{
    #region 🔧 Dependencies

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileService> _logger;
    private readonly string _rootPath;

    public LocalFileService(IWebHostEnvironment env, ILogger<LocalFileService> logger)
    {
        _env = env;
        _logger = logger;

        _rootPath = Path.Combine(_env.WebRootPath, "Temp-Files");

        EnsureDirectory();
    }

    #endregion


    #region 📁 Directory Setup

    private void EnsureDirectory()
    {
        if (!Directory.Exists(_rootPath))
            Directory.CreateDirectory(_rootPath);
    }

    #endregion


    #region 🔐 Safe Path Handling

    private string GetSafePath(string fileName)
    {
        var safeName = Path.GetFileName(fileName); // prevents path traversal
        return Path.Combine(_rootPath, safeName);
    }

    #endregion


    #region 📥 Read Operations

    public async Task<string> ReadTextAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            var path = GetSafePath(fileName);

            if (!File.Exists(path))
                return "";

            return await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadTextAsync failed");
            return "";
        }
    }

    public async Task<byte[]> ReadBytesAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            var path = GetSafePath(fileName);

            if (!File.Exists(path))
                return Array.Empty<byte>();

            return await File.ReadAllBytesAsync(path, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadBytesAsync failed");
            return Array.Empty<byte>();
        }
    }

    #endregion


    #region 📤 Write Operations

    public async Task WriteTextAsync(string fileName, string content, CancellationToken ct = default)
    {
        try
        {
            var path = GetSafePath(fileName);
            await File.WriteAllTextAsync(path, content ?? "", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteTextAsync failed");
        }
    }

    public async Task WriteBytesAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        try
        {
            var path = GetSafePath(fileName);
            await File.WriteAllBytesAsync(path, content ?? Array.Empty<byte>(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteBytesAsync failed");
        }
    }

    #endregion


    #region 📤 Upload Handling

    public async Task<string> SaveUploadAsync(IFormFile file, CancellationToken ct = default)
    {
        try
        {
            if (file == null || file.Length == 0)
                return "";

            var fileName = Path.GetFileName(file.FileName);
            var path = GetSafePath(fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream, ct);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveUploadAsync failed");
            return "";
        }
    }

    #endregion


    #region ❌ Delete & Exists

    public bool Delete(string fileName)
    {
        try
        {
            var path = GetSafePath(fileName);

            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed");
            return false;
        }
    }

    public bool Exists(string fileName)
    {
        var path = GetSafePath(fileName);
        return File.Exists(path);
    }

    #endregion


    #region 📂 File Listing

    public string[] ListFiles()
    {
        try
        {
            EnsureDirectory();

            return Directory.GetFiles(_rootPath)
                            .Select(Path.GetFileName)
                            .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListFiles failed");
            return Array.Empty<string>();
        }
    }

    #endregion


    #region 🧹 Maintenance

    public void ClearAll()
    {
        try
        {
            var files = Directory.GetFiles(_rootPath);

            foreach (var file in files)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearAll failed");
        }
    }

    #endregion


    #region 📊 File Info

    public (string Name, long Size, DateTime Created)? GetInfo(string fileName)
    {
        try
        {
            var path = GetSafePath(fileName);

            if (!File.Exists(path))
                return null;

            var file = new FileInfo(path);

            return (file.Name, file.Length, file.CreationTimeUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetInfo failed");
            return null;
        }
    }

    #endregion


    #region 🤖 AI ACTION ADAPTER

    // 🔥 Used by AIService action engine
    public async Task SaveAsync(JsonElement action, string json, CancellationToken ct = default)
    {
        try
        {
            string fileName = "ai-result.json";

            if (action.TryGetProperty("config", out var config) &&
                config.TryGetProperty("path", out var pathElement))
            {
                var rawPath = pathElement.GetString();

                if (!string.IsNullOrWhiteSpace(rawPath))
                    fileName = Path.GetFileName(rawPath);
            }

            await WriteTextAsync(fileName, json, ct);

            _logger.LogInformation("AI result saved: {File}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAsync adapter failed");
            throw;
        }
    }

    #endregion
}