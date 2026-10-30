using AmlScreening.Application.Interface;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Portal.Services;

public class CustomerKycReviewService
{
    private readonly AmlScreeningDbContext _dbContext;
    private readonly ICurrentUserContext _userContext;


    public CustomerKycReviewService(
        AmlScreeningDbContext dbContext,
        ICurrentUserContext userContext)
    {
        _dbContext =
            dbContext;

        _userContext =
            userContext;
    }


    // =========================================================
    // PENDING LIST
    // =========================================================

    public async Task<List<CustomerKycScreeningLog>>
        GetPendingAsync(
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .CustomerKycScreeningLogs
            .AsNoTracking()

            .Where(x =>
                x.PartnerId ==
                    _userContext.PartnerId
                &&
                x.Status ==
                    "PENDING_REVIEW"
                &&
                x.ApprovedBy == null
                &&
                x.RejectedBy == null)


            .OrderByDescending(x =>
                x.CreatedDate)

            .ToListAsync(
                cancellationToken);
    }


    // =========================================================
    // DETAILS
    // =========================================================

    public async Task<CustomerKycScreeningLog?>
        GetAsync(
            long id,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .CustomerKycScreeningLogs
            .AsNoTracking()

            .SingleOrDefaultAsync(
                x =>
                    x.Id == id
                    &&
                    x.PartnerId ==
                        _userContext.PartnerId,
                cancellationToken);
    }


    // =========================================================
    // APPROVE
    // =========================================================

    public async Task ApproveAsync(
        long id,
        byte[] rowVersion,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var log =
            await _dbContext
                .CustomerKycScreeningLogs

                .SingleOrDefaultAsync(
                    x =>
                        x.Id == id
                        &&
                        x.PartnerId ==
                            _userContext.PartnerId,
                    cancellationToken);


        if (log == null)
        {
            throw new InvalidOperationException(
                "KYC screening was not found.");
        }


        if (log.Status !=
            "PENDING_REVIEW")
        {
            throw new InvalidOperationException(
                "This KYC screening is no longer pending review.");
        }


        // Maker cannot approve own screening.

        if (log.UserId ==
            _userContext.UserId)
        {
            throw new InvalidOperationException(
                "You cannot approve your own KYC screening.");
        }


        if (log.ApprovedBy != null ||
            log.ApprovedDate != null)
        {
            throw new InvalidOperationException(
                "This screening has already been approved.");
        }


        if (log.RejectedBy != null ||
            log.RejectedDate != null)
        {
            throw new InvalidOperationException(
                "This screening has already been rejected.");
        }


        _dbContext.Entry(log)
            .Property(x => x.RowVersion)
            .OriginalValue =
                rowVersion;


        log.Status =
            "APPROVED";


        log.ApprovedBy =
            _userContext.Username;

        log.ApprovedDate =
            DateTime.UtcNow;

        log.ApprovalRemarks =
            remarks?.Trim();


        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Another checker already updated this KYC screening. Reload the page.");
        }
    }


    // =========================================================
    // REJECT
    // =========================================================

    public async Task RejectAsync(
        long id,
        byte[] rowVersion,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                remarks))
        {
            throw new ArgumentException(
                "Rejection reason is required.");
        }


        var log =
            await _dbContext
                .CustomerKycScreeningLogs

                .SingleOrDefaultAsync(
                    x =>
                        x.Id == id
                        &&
                        x.PartnerId ==
                            _userContext.PartnerId,
                    cancellationToken);


        if (log == null)
        {
            throw new InvalidOperationException(
                "KYC screening was not found.");
        }


        if (log.Status !=
            "PENDING_REVIEW")
        {
            throw new InvalidOperationException(
                "This KYC screening is no longer pending review.");
        }


        if (log.UserId ==
            _userContext.UserId)
        {
            throw new InvalidOperationException(
                "You cannot reject your own KYC screening.");
        }


        _dbContext.Entry(log)
            .Property(x => x.RowVersion)
            .OriginalValue =
                rowVersion;


        log.Status =
            "REJECTED";

        //log.RejectedByUserId =
        //    _userContext.UserId;

        log.RejectedBy =
            _userContext.Username;

        log.RejectedDate =
            DateTime.UtcNow;

        log.RejectedRemarks =
            remarks.Trim();


        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Another checker already updated this KYC screening. Reload the page.");
        }
    }
}