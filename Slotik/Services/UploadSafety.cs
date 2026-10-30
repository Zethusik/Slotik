using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Slotik.Services;

public sealed class UploadSettings
{
    public long MaxFileBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxRequestBytes { get; set; } = 55 * 1024 * 1024;
    public int MaxWidth { get; set; } = 4096;
    public int MaxHeight { get; set; } = 4096;
    public long MaxPixels { get; set; } = 16_000_000;
    public string[] AllowedFormats { get; set; } = ["JPEG", "PNG", "WEBP"];
}

// Resource stage limits the body before model binding; action stage checks actual bytes.
public sealed class ImageUploadFilter(IOptions<UploadSettings> settings) : IAsyncResourceFilter, IAsyncActionFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > settings.Value.MaxRequestBytes)
        {
            context.Result = new ObjectResult(new { error = "upload_too_large" }) { StatusCode = 413 };
            return;
        }
        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = settings.Value.MaxRequestBytes;
        await next();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
            foreach (var file in form.Files)
            {
                var error = await ValidateAsync(file, context.HttpContext.RequestAborted);
                if (error != null)
                {
                    context.Result = new BadRequestObjectResult(new { error = "invalid_image", message = error });
                    return;
                }
            }
        }
        await next();
    }

    public async Task<string?> ValidateAsync(IFormFile file, CancellationToken ct = default)
    {
        var limits = settings.Value;
        if (file.Length <= 0) return "Image is empty.";
        if (file.Length > limits.MaxFileBytes) return "Image exceeds the upload size limit.";
        try
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            using var codec = SKCodec.Create(buffer);
            if (codec == null) return "Image content is invalid.";
            if (!limits.AllowedFormats.Contains(codec.EncodedFormat.ToString(), StringComparer.OrdinalIgnoreCase))
                return "Image format is not allowed.";
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > limits.MaxWidth || info.Height > limits.MaxHeight
                || (long)info.Width * info.Height > limits.MaxPixels)
                return "Image dimensions exceed the upload limit.";
            if (codec.FrameCount > 1) return "Only still images are supported.";
            var pixels = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(pixels);
            if (codec.GetPixels(pixels, bitmap.GetPixels()) != SKCodecResult.Success)
                return "Image content is invalid or incomplete.";
            return null;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException)
        {
            return "Image content is invalid.";
        }
    }
}

// Compensates external uploads on every early return / exception before the DB commit.
public sealed class PhotoUploadBatch(IPhotoService provider, ILogger logger) : IAsyncDisposable
{
    private readonly List<string> _newAssets = [];
    private bool _committed;

    public async Task<ImageUploadResult> AddAsync(IFormFile file)
    {
        var result = await provider.AddPhotoAsync(file);
        if (!string.IsNullOrEmpty(result.PublicId)) _newAssets.Add(result.PublicId);
        return result;
    }

    public static bool Failed(ImageUploadResult result) => result.Error != null
        || result.SecureUrl == null || string.IsNullOrWhiteSpace(result.PublicId);

    public void Commit() => _committed = true;

    public static async Task DeleteBestEffortAsync(IPhotoService provider, string? asset, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(asset)) return;
        try
        {
            var result = await provider.DeletePhotoAsync(asset);
            if (result.Error != null) logger.LogWarning("Photo cleanup failed; manual reconciliation may be needed.");
        }
        catch (Exception)
        {
            // Do not log provider exceptions: they can contain request URLs/credentials.
            logger.LogWarning("Photo cleanup failed; manual reconciliation may be needed.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
            foreach (var asset in _newAssets.Distinct()) await DeleteBestEffortAsync(provider, asset, logger);
    }
}
