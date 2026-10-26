using System.Globalization;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>
/// Shared Excel import wizard (ADM-23, ITM-11): download template → upload and preview → import.
/// The uploaded file waits in memory for 15 minutes under a random token bound to the user, so the import
/// saves exactly the file that was previewed.
/// </summary>
public abstract class ImportPageModel(ImportService imports, IMemoryCache cache, ICurrentUser currentUser) : AppPageModel
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public abstract ImportKind Kind { get; }
    public ImportPreviewDto? Preview { get; private set; }
    public string? Token { get; private set; }
    public string? FileName { get; private set; }
    public ImportResultDto? Result { get; private set; }

    public string Culture => CultureInfo.CurrentUICulture.Name;

    public string Header(string key) => ImportService.HeaderOf(Kind, key, Culture);

    public async Task<IActionResult> OnGetTemplateAsync()
    {
        var bytes = await imports.TemplateAsync(Kind, Culture);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{Kind.ToString().ToLowerInvariant()}-template.xlsx");
    }

    public async Task<IActionResult> OnPostPreviewAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0 || file.Length > ImportService.MaxFileBytes
            || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, L["Import.FileSize"]);
            return Page();
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (await TryExecuteAsync(async () => Preview = await imports.PreviewAsync(Kind, new MemoryStream(bytes)), string.Empty))
        {
            Token = SecureToken.Create(16);
            FileName = Path.GetFileName(file.FileName);
            cache.Set(CacheKey(Token), (bytes, FileName), Lifetime);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(string token)
    {
        if (!cache.TryGetValue(CacheKey(token), out (byte[] Bytes, string Name) entry))
        {
            ModelState.AddModelError(string.Empty, L["Import.Expired"]);
            return Page();
        }

        var ok = await TryExecuteAsync(async () => Result = await imports.ImportAsync(Kind, new MemoryStream(entry.Bytes)), string.Empty);
        if (ok)
        {
            cache.Remove(CacheKey(token));
        }
        else
        {
            // Data changed since the preview (e.g. a code was taken meanwhile): show the fresh validation.
            Preview = await imports.PreviewAsync(Kind, new MemoryStream(entry.Bytes));
            Token = token;
            FileName = entry.Name;
        }

        return Page();
    }

    private string CacheKey(string token) => $"import:{currentUser.UserId}:{Kind}:{token}";
}
