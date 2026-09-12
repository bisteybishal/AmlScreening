using AmlScreening.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Portal.ViewModels;

namespace AmlScreening.Portal.Controllers
{
    [Authorize(Policy = AppPermissions.Screening)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ScreeningController(IAmlRecordRepository repository, ILogger<ScreeningController> logger) : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View(new ScreeningPageViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(string name, CancellationToken cancellationToken)
        {
            var searchName = name.Trim();
            var model = new ScreeningSearchResultsViewModel
            {
                SearchName = searchName,

            };
            if (searchName.Length == 0)
                return PartialView("_SearchResults", model);
            try
            {
                var records = await repository.FindMatchNameAsync(searchName, cancellationToken);
                model.Results = records.Take(50).ToList();
                model.HasResults = records.Count > 50;
                return PartialView("_SearchResults", model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while searching for name: {SearchName}", searchName);
                ModelState.AddModelError(string.Empty, "An error occurred while processing your request.");
                return PartialView("_SearchResults", model);
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken = default)
        {
            var record = await repository.GetPhotoAsync(id, cancellationToken);

            if (record == null || record.Photo == null)
            {
                return NotFound();
            }

            return File(
                record.Photo,
                record.PhotoContentType ?? "image/jpeg");
        }
    }
}



