using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.Screening)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ScreeningController(
    IAmlRecordRepository repository,
    ILogger<ScreeningController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var record = await repository.GetPhotoAsync(id, cancellationToken);
        if (record?.Photo is not { Length: > 0 }) return NotFound();
        // Serve only recognized raster formats. Never execute uploaded HTML/SVG.
        var bytes = record.Photo;
        string? type = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
            ? "image/png" : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255
            ? "image/jpeg" : null;
        if (type is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, type);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("screening")]
    public async Task<IActionResult> Search(ScreeningBatchRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var names = request.Names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var model = new ScreeningBatchResultsViewModel();
        try
        {
            // Use sequential queries: a scoped EF DbContext is not thread safe.
            foreach (var name in names)
            {
                var records = await repository.FindMatchNameAsync(name, cancellationToken);
                model.Groups.Add(new ScreeningSearchResultsViewModel
                {
                    SearchName = name,
                    Results = records.Take(50).ToList(),
                    HasResults = records.Count > 50
                });
            }
            return PartialView("_SearchBatchResults", model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not put customers' names in ordinary application logs.
            logger.LogError(exception, "Name lookup failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            return Problem(statusCode: 503, title: "Search is temporarily unavailable. No screening conclusion was made.");
        }
    }
}
