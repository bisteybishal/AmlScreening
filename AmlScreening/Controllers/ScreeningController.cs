using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers
{
    [Authorize(Policy = AppPermissions.Screening.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ScreeningController : Controller
    {
        private readonly IAmlRecordRepository _repository;
        private readonly IErrorLogService _errorLogService;

        public ScreeningController(IAmlRecordRepository repository, IErrorLogService errorLogService)
        {
            _repository = repository;
            _errorLogService = errorLogService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ScreeningPageViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(string? name, CancellationToken cancellationToken)
        {
            var searchName = name?.Trim() ?? string.Empty;
            var model = new ScreeningSearchResultsViewModel
            {
                SearchName = searchName,

            };
            if (searchName.Length == 0)
                return PartialView("_SearchResults", model);
            if (searchName.Length > 250)
            {
                ModelState.AddModelError(string.Empty, "Enter at most 250 characters.");
                return PartialView("_SearchResults", model);
            }
            try
            {
                var records = await _repository.FindMatchNameAsync(searchName, cancellationToken);
                model.Results = records.Take(50).ToList();
                model.HasMore = records.Count > 50;
                return PartialView("_SearchResults", model);
            }
            catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
            {
                /*
                 * Live search cancels previous
                 * requests while typing.
                 *
                 * This is normal and should
                 * not be recorded as an error.
                 */

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                var errorId =
                    await LogErrorAsync(
                        ex,
                        nameof(Search));


                ModelState.AddModelError(
                    string.Empty,
                    errorId.HasValue
                        ? $"Screening search failed. Error reference: {errorId}"
                        : "Screening search failed.");


                return PartialView(
                    "_SearchResults",
                    model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SearchMultiple(string? name, CancellationToken cancellationToken = default)
        {
            var result = new List<ScreeningSearchResultsViewModel>();
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(string.Empty, "Enter names using no more than 5,000 characters.");
                return PartialView("_MultipleSearchResults", result);
            }
            //split by comma or new line ,never by spaces
            var names = name.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (names.Length == 0)
            {
                ModelState.AddModelError(
           string.Empty,
           "Enter between 1 and 20 names.");

                return PartialView("_MultipleSearchResults", result);
            }
            try
            {
                foreach (var searchname in names)
                {
                    var records = await _repository.FindMatchNameAsync(searchname, cancellationToken);
                    result.Add(new ScreeningSearchResultsViewModel
                    {
                        SearchName = searchname,
                        Results = records.Take(50).ToList(),
                        HasMore = records.Count > 50
                    });

                }
                return PartialView("_MultipleSearchResults", result);
            }
           
            catch (Exception ex)
            {
                var errorId = await LogErrorAsync(
                    ex,
                    nameof(SearchMultiple));

                ModelState.AddModelError(
                    string.Empty,
                    errorId.HasValue
                        ? $"Search failed. Error reference: {errorId}"
                        : "Search failed. Please try again.");

                return PartialView("_MultipleSearchResults", result);
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var record = await _repository.GetPhotoAsync(id, cancellationToken);

                if (record == null || record.Photo == null)
                {
                    return NotFound();
                }
                var contentType =
                   string.IsNullOrWhiteSpace(
                       record.PhotoContentType)

                       ? "image/jpeg"

                       : record.PhotoContentType;
                return File(
                     record.Photo,
                     contentType);
            }
            catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(
                    ex,
                    nameof(GetPhoto));

                return NotFound();
            }
        }
        private Task<long?> LogErrorAsync(
       Exception exception,
       string action)
        {
            return _errorLogService.LogAsync(
                exception,
                nameof(ScreeningController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
        }
    }
}



