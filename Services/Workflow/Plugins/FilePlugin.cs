using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;
using MITANZ360Pro.Web.Services;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

public enum FileOperation
{
    Read,
    Write,
    Append,
    Delete,
    Copy,
    Move,
    List,

    SaveHtml,
    SaveMarkdown,
    SaveJson,
    SaveCsv,

    SaveDocx,
    SavePdf,
    SaveExcel,

    UploadToSharePoint
}
public sealed class FilePlugin : WorkflowPluginBase
{

    public FilePlugin(
        ILogger<FilePlugin> logger)
        : base(logger)
    {
    }

    public override string Type => "file";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var operationText =
            GetSetting<string>(step, "operation");

        if (!Enum.TryParse<FileOperation>(
            operationText,
            true,
            out var operation))
        {
            operation = FileOperation.Read;
        }

        switch (operation)
        {
            case FileOperation.Read:
                await ReadAsync(context, step, cancellationToken);
                break;

            case FileOperation.Write:
                await WriteAsync(context, step, cancellationToken);
                break;

            case FileOperation.Append:
                await AppendAsync(context, step, cancellationToken);
                break;

            case FileOperation.Delete:
                Delete(context, step);
                break;

            case FileOperation.Copy:
                Copy(context, step);
                break;

            case FileOperation.Move:
                Move(context, step);
                break;

            case FileOperation.List:
                List(context, step);
                break;

            case FileOperation.SaveHtml:
                SetExtension(step, ".html");
                await WriteAsync(context, step, cancellationToken);
                break;

            case FileOperation.SaveMarkdown:
                SetExtension(step, ".md");
                await WriteAsync(context, step, cancellationToken);
                break;

            case FileOperation.SaveJson:
                SetExtension(step, ".json");
                await WriteAsync(context, step, cancellationToken);
                break;

            case FileOperation.SaveCsv:
                SetExtension(step, ".csv");
                await WriteAsync(context, step, cancellationToken);
                break;

            case FileOperation.SaveDocx:
            case FileOperation.SavePdf:
            case FileOperation.SaveExcel:
                throw new NotImplementedException(
                    $"{operation} is not implemented yet.");

            case FileOperation.UploadToSharePoint:
                await UploadToSharePointAsync(
                    context,
                    step,
                    cancellationToken);
                break;

            default:
                throw new NotSupportedException(
                    $"File operation '{operation}' is not supported.");
        }
    }

    private async Task ReadAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var path = GetRequiredPath(step);

        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        var extension = Path.GetExtension(path).ToLowerInvariant();

        object result = extension switch
        {
            ".txt" or ".json" or ".xml" or ".csv" or ".md" or ".html"
                => await File.ReadAllTextAsync(path, cancellationToken),

            _ => await File.ReadAllBytesAsync(path, cancellationToken)
        };

        context.Set(step.Output, result);
    }

    private async Task WriteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var path = GetRequiredPath(step);

        var inputKey =
            GetSetting<string>(step, "input")
            ?? throw new InvalidOperationException(
                "Input setting is required.");

        var value = context.Get<object>(inputKey);

        // ✅ DEBUG LOG (CRITICAL)
        Logger.LogInformation(
            "FilePlugin.WriteAsync -> Path: {Path}, InputKey: {InputKey}, ValueType: {Type}, IsNull: {IsNull}",
            path,
            inputKey,
            value?.GetType().FullName ?? "null",
            value == null);

        EnsureDirectory(path);

        string content;

        switch (value)
        {
            case null:
                content = string.Empty;
                break;

            case byte[] bytes:
                await File.WriteAllBytesAsync(
                    path,
                    bytes,
                    cancellationToken);

                context.Set(step.Output, new
                {
                    Path = path,
                    BytesWritten = bytes.Length
                });

                return;

            case string str:
                content = str;
                break;

            default:
                // ✅ FIX: serialize object instead of ToString()
                content = System.Text.Json.JsonSerializer.Serialize(
                    value,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                break;
        }

        await File.WriteAllTextAsync(
            path,
            content,
            cancellationToken);

        var fileInfo = new FileInfo(path);

        context.Set(
            step.Output,
            new
            {
                Path = fileInfo.FullName,
                Name = fileInfo.Name,
                Extension = fileInfo.Extension,
                Size = fileInfo.Length,
                Created = fileInfo.CreationTimeUtc
            });
    }
    private async Task AppendAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var path = GetRequiredPath(step);

        var inputKey =
            GetSetting<string>(step, "input")
            ?? throw new InvalidOperationException(
                "Input setting is required.");

        var value = context.Get<object>(inputKey);

        EnsureDirectory(path);

        await File.AppendAllTextAsync(
            path,
            value?.ToString() ?? string.Empty,
            cancellationToken);

        context.Set(
            step.Output,
            new
            {
                Path = path,
                Appended = true
            });
    }

    private void Delete(
        WorkflowContext context,
        WorkflowStep step)
    {
        var path = GetRequiredPath(step);

        var deleted = false;

        if (File.Exists(path))
        {
            File.Delete(path);
            deleted = true;
        }

        context.Set(
            step.Output,
            new
            {
                Path = path,
                Deleted = deleted
            });
    }

    private void Copy(
        WorkflowContext context,
        WorkflowStep step)
    {
        var source = GetRequiredPath(step);

        var destination =
            GetSetting<string>(step, "destination")
            ?? throw new InvalidOperationException(
                "Destination is required.");

        if (!File.Exists(source))
            throw new FileNotFoundException(source);

        EnsureDirectory(destination);

        File.Copy(source, destination, true);

        context.Set(
            step.Output,
            new
            {
                Source = source,
                Destination = destination,
                Copied = true
            });
    }

    private void Move(
        WorkflowContext context,
        WorkflowStep step)
    {
        var source = GetRequiredPath(step);

        var destination =
            GetSetting<string>(step, "destination")
            ?? throw new InvalidOperationException(
                "Destination is required.");

        if (!File.Exists(source))
            throw new FileNotFoundException(source);

        EnsureDirectory(destination);

        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(source, destination);

        context.Set(
            step.Output,
            new
            {
                Source = source,
                Destination = destination,
                Moved = true
            });
    }

    private void List(
        WorkflowContext context,
        WorkflowStep step)
    {
        var path = GetRequiredPath(step);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(path);

        var files = Directory
            .GetFiles(path)
            .Select(file => new
            {
                Name = Path.GetFileName(file),
                Path = file,
                Size = new FileInfo(file).Length,
                Modified = File.GetLastWriteTimeUtc(file)
            })
            .ToList();

        context.Set(step.Output, files);
    }

    private static string GetRequiredPath(
        WorkflowStep step)
    {
        var path = step.Settings.TryGetValue("path", out var value)
            ? value?.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "File path is required.");

        return path;
    }

    private static void EnsureDirectory(
        string filePath)
    {
        var directory =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void SetExtension(
        WorkflowStep step,
        string extension)
    {
        if (!step.Settings.TryGetValue("path", out var value))
            return;

        var path = value?.ToString();

        if (string.IsNullOrWhiteSpace(path))
            return;

        step.Settings["path"] =
            Path.ChangeExtension(path, extension);
    }


    private readonly SharePointService _sharePointService;

    public FilePlugin(
        ILogger<FilePlugin> logger,
        SharePointService sharePointService)
        : base(logger)
    {
        _sharePointService = sharePointService;
    }

    private async Task UploadToSharePointAsync(
    WorkflowContext context,
    WorkflowStep step,
    CancellationToken cancellationToken)
    {
        var inputKey =
            GetSetting<string>(step, "input")
            ?? throw new InvalidOperationException(
                "Input setting is required.");

        var value =
            context.Get<object>(inputKey);

        var content =
            value?.ToString() ?? string.Empty;

        var fileName =
            GetSetting<string>(step, "fileName")
            ?? $"Report-{DateTime.Now:yyyyMMddHHmmss}.html";

        //var url = await _sharePointService.UploadWorkflowTextAsync(fileName,content);

        //context.Set(step.Output,url);
    }

}