using System.Net;
using DentalLab.Application.Abstractions;
using DentalLab.Application.Models.Approvals;
using DentalLab.Application.Security;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class CaseApprovalService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager,
    ICaseService caseService,
    IApplicationEmailService emailService) : ICaseApprovalService
{
    public async Task<List<AdminOption>> GetActiveAdminsAsync(
        CancellationToken ct = default)
    {
        var users = await userManager.GetUsersInRoleAsync(
            ApplicationRoles.Admin);

        return users
            .Where(user =>
                !IsLocked(user) &&
                !string.IsNullOrWhiteSpace(user.Email))
            .OrderBy(user => user.Email)
            .Select(user => new AdminOption
            {
                UserId = user.Id,
                Email = user.Email!
            })
            .ToList();
    }

    public async Task<ApprovalOperationResult> CreateAsync(
        CreateCaseApprovalRequest request,
        string requestedByUserId,
        string requestedByEmail,
        CancellationToken ct = default)
    {
        if (!CaseApprovalRequestTypes.All.Contains(request.RequestType))
        {
            return ApprovalOperationResult.Failure(
                "Invalid request type.");
        }

        if (string.IsNullOrWhiteSpace(requestedByUserId))
        {
            return ApprovalOperationResult.Failure(
                "Current user is unavailable.");
        }

        var admin = await userManager.FindByIdAsync(
            request.AssignedAdminUserId);

        if (admin is null ||
            IsLocked(admin) ||
            !await userManager.IsInRoleAsync(
                admin,
                ApplicationRoles.Admin))
        {
            return ApprovalOperationResult.Failure(
                "Select an active Admin.");
        }

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var caseExists = await db.Records
            .AsNoTracking()
            .AnyAsync(
                record =>
                    record.CaseId == request.CaseId &&
                    !record.IsCancelled,
                ct);

        if (!caseExists)
        {
            return ApprovalOperationResult.Failure(
                "The case was not found or is already cancelled.");
        }

        // Only one active request is permitted for a case, regardless
        // of requester, request type, or assigned Admin.
        var activeRequest = await db.CaseApprovalRequests
            .AsNoTracking()
            .Where(existing =>
                existing.CaseId == request.CaseId &&
                (existing.Status ==
                    CaseApprovalRequestStatuses.Pending ||
                 existing.Status ==
                    CaseApprovalRequestStatuses.Approved))
            .OrderByDescending(existing => existing.RequestedOn)
            .Select(existing => new
            {
                existing.RequestId,
                existing.RequestType,
                existing.Status,
                existing.AssignedAdminEmail
            })
            .FirstOrDefaultAsync(ct);

        if (activeRequest is not null)
        {
            return ApprovalOperationResult.Failure(
                $"Case #{request.CaseId} already has active " +
                $"{activeRequest.RequestType.ToLowerInvariant()} request " +
                $"#{activeRequest.RequestId} ({activeRequest.Status}) " +
                $"assigned to {activeRequest.AssignedAdminEmail}. " +
                "It must be completed or rejected before another " +
                "request can be submitted.");
        }

        var entity = new CaseApprovalRequest
        {
            CaseId = request.CaseId,
            RequestType = request.RequestType,
            Status = CaseApprovalRequestStatuses.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedByEmail = requestedByEmail,
            AssignedAdminUserId = admin.Id,
            AssignedAdminEmail =
                admin.Email ?? admin.UserName ?? string.Empty,
            Reason = request.Reason.Trim(),
            AdditionalDetails = Clean(request.AdditionalDetails),
            RequestedOn = DateTime.UtcNow
        };

        db.CaseApprovalRequests.Add(entity);
        await db.SaveChangesAsync(ct);

        await SendRequestCreatedEmailAsync(entity, ct);

        return ApprovalOperationResult.Success(entity.RequestId);
    }

    public async Task<List<CaseApprovalListItem>> GetForCurrentUserAsync(
        string currentUserId,
        bool isOwner,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var query = db.CaseApprovalRequests.AsNoTracking();

        if (!isOwner)
        {
            query = query.Where(request =>
                request.RequestedByUserId == currentUserId ||
                request.AssignedAdminUserId == currentUserId);
        }

        return await query
            .OrderByDescending(request => request.RequestedOn)
            .Select(request => new CaseApprovalListItem
            {
                RequestId = request.RequestId,
                CaseId = request.CaseId,
                RequestType = request.RequestType,
                Status = request.Status,
                RequestedByEmail = request.RequestedByEmail,
                AssignedAdminEmail = request.AssignedAdminEmail,
                Reason = request.Reason,
                RequestedOn = request.RequestedOn,
                ReviewedOn = request.ReviewedOn
            })
            .ToListAsync(ct);
    }

    public async Task<CaseApprovalDetails?> GetByIdAsync(
        long requestId,
        string currentUserId,
        bool isOwner,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        return await db.CaseApprovalRequests
            .AsNoTracking()
            .Where(request =>
                request.RequestId == requestId &&
                (isOwner ||
                 request.RequestedByUserId == currentUserId ||
                 request.AssignedAdminUserId == currentUserId))
            .Select(request => new CaseApprovalDetails
            {
                RequestId = request.RequestId,
                CaseId = request.CaseId,
                RequestType = request.RequestType,
                Status = request.Status,
                RequestedByUserId = request.RequestedByUserId,
                RequestedByEmail = request.RequestedByEmail,
                AssignedAdminUserId = request.AssignedAdminUserId,
                AssignedAdminEmail = request.AssignedAdminEmail,
                Reason = request.Reason,
                AdditionalDetails = request.AdditionalDetails,
                RequestedOn = request.RequestedOn,
                ReviewedByEmail = request.ReviewedByEmail,
                ReviewedOn = request.ReviewedOn,
                ReviewComment = request.ReviewComment,
                CompletedOn = request.CompletedOn,
                RowVersion = request.RowVersion
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<ApprovalOperationResult> ApproveAsync(
        long requestId,
        string comment,
        byte[] rowVersion,
        string reviewerUserId,
        string reviewerEmail,
        bool isOwner,
        CancellationToken ct = default)
    {
        return ReviewAsync(
            requestId,
            comment,
            rowVersion,
            reviewerUserId,
            reviewerEmail,
            isOwner,
            approve: true,
            ct);
    }

    public Task<ApprovalOperationResult> RejectAsync(
        long requestId,
        string comment,
        byte[] rowVersion,
        string reviewerUserId,
        string reviewerEmail,
        bool isOwner,
        CancellationToken ct = default)
    {
        return ReviewAsync(
            requestId,
            comment,
            rowVersion,
            reviewerUserId,
            reviewerEmail,
            isOwner,
            approve: false,
            ct);
    }

    public async Task<ApprovalOperationResult> ApproveAndCancelAsync(
        long requestId,
        string comment,
        byte[] rowVersion,
        string reviewerUserId,
        string reviewerEmail,
        bool isOwner,
        CancellationToken ct = default)
    {
        var approved = await ApproveAsync(
            requestId,
            comment,
            rowVersion,
            reviewerUserId,
            reviewerEmail,
            isOwner,
            ct);

        if (!approved.Succeeded)
        {
            return approved;
        }

        return await ResumeCancellationAsync(
            requestId,
            reviewerUserId,
            reviewerEmail,
            isOwner,
            ct);
    }

    public async Task<ApprovalOperationResult> ResumeCancellationAsync(
        long requestId,
        string reviewerUserId,
        string reviewerEmail,
        bool isOwner,
        CancellationToken ct = default)
    {
        var details = await GetByIdAsync(
            requestId,
            reviewerUserId,
            isOwner,
            ct);

        if (details is null)
        {
            return ApprovalOperationResult.Failure(
                "The request was not found or access was denied.");
        }

        if (details.RequestType != CaseApprovalRequestTypes.Cancel)
        {
            return ApprovalOperationResult.Failure(
                "This is not a cancellation request.");
        }

        if (details.Status != CaseApprovalRequestStatuses.Approved)
        {
            return ApprovalOperationResult.Failure(
                "Only an approved cancellation request can be resumed.");
        }

        if (!isOwner &&
            details.AssignedAdminUserId != reviewerUserId)
        {
            return ApprovalOperationResult.Failure(
                "This request is assigned to another Admin.");
        }

        var cancelled = await caseService.CancelAsync(
            details.CaseId,
            details.Reason,
            reviewerEmail,
            ct);

        if (!cancelled.Succeeded)
        {
            return ApprovalOperationResult.Failure(
                "The request remains Approved because cancellation " +
                $"was not completed: {cancelled.ErrorMessage}");
        }

        return await CompleteAsync(
            requestId,
            details.CaseId,
            reviewerUserId,
            reviewerEmail,
            isOwner,
            ct);
    }

    public Task<ApprovalOperationResult> CompleteEditAsync(
        long requestId,
        int caseId,
        string completedByUserId,
        string completedByEmail,
        bool isOwner,
        CancellationToken ct = default)
    {
        return CompleteAsync(
            requestId,
            caseId,
            completedByUserId,
            completedByEmail,
            isOwner,
            ct);
    }

    private async Task<ApprovalOperationResult> ReviewAsync(
        long requestId,
        string comment,
        byte[] rowVersion,
        string reviewerUserId,
        string reviewerEmail,
        bool isOwner,
        bool approve,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var entity = await db.CaseApprovalRequests
            .FirstOrDefaultAsync(
                request => request.RequestId == requestId,
                ct);

        if (entity is null)
        {
            return ApprovalOperationResult.Failure(
                "Request not found.");
        }

        if (!isOwner &&
            entity.AssignedAdminUserId != reviewerUserId)
        {
            return ApprovalOperationResult.Failure(
                "This request is assigned to another Admin.");
        }

        if (entity.Status != CaseApprovalRequestStatuses.Pending)
        {
            return ApprovalOperationResult.Failure(
                "Only pending requests can be reviewed.");
        }

        if (rowVersion.Length == 0)
        {
            return ApprovalOperationResult.Failure(
                "Concurrency token missing. Refresh and try again.");
        }

        db.Entry(entity)
            .Property(request => request.RowVersion)
            .OriginalValue = rowVersion;

        entity.Status = approve
            ? CaseApprovalRequestStatuses.Approved
            : CaseApprovalRequestStatuses.Rejected;

        entity.ReviewComment = Clean(comment);
        entity.ReviewedByUserId = reviewerUserId;
        entity.ReviewedByEmail = reviewerEmail;
        entity.ReviewedOn = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApprovalOperationResult.Failure(
                "The request was changed by another user. " +
                "Refresh and try again.");
        }

        await SendRequestReviewedEmailAsync(entity, approve, ct);

        return ApprovalOperationResult.Success(requestId);
    }

    private async Task<ApprovalOperationResult> CompleteAsync(
        long requestId,
        int caseId,
        string userId,
        string email,
        bool isOwner,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var entity = await db.CaseApprovalRequests
            .FirstOrDefaultAsync(
                request =>
                    request.RequestId == requestId &&
                    request.CaseId == caseId,
                ct);

        if (entity is null)
        {
            return ApprovalOperationResult.Failure(
                "Approval request not found.");
        }

        if (!isOwner && entity.AssignedAdminUserId != userId)
        {
            return ApprovalOperationResult.Failure(
                "This request is assigned to another Admin.");
        }

        if (entity.Status != CaseApprovalRequestStatuses.Approved)
        {
            return ApprovalOperationResult.Failure(
                "The request must be approved before completion.");
        }

        entity.Status = CaseApprovalRequestStatuses.Completed;
        entity.CompletedOn = DateTime.UtcNow;
        entity.CompletedByUserId = userId;
        entity.CompletedByEmail = email;

        await db.SaveChangesAsync(ct);

        await SendRequestCompletedEmailAsync(entity, ct);

        return ApprovalOperationResult.Success(requestId);
    }

    private async Task SendRequestCreatedEmailAsync(
        CaseApprovalRequest request,
        CancellationToken ct)
    {
        var typeLabel = request.RequestType ==
            CaseApprovalRequestTypes.Cancel
                ? "Cancellation"
                : "Edit";

        var subject =
            $"[Dental Lab] New {typeLabel} Request - " +
            $"Case #{request.CaseId}";

        var body =
            $"<h2>New Case Approval Request</h2>" +
            $"<p>A new request has been assigned to you.</p>" +
            DetailsTable(request) +
            $"<p><strong>Reason:</strong><br>" +
            $"{Encode(request.Reason)}</p>" +
            AdditionalDetails(request.AdditionalDetails) +
            $"<p>Please sign in to Dental Lab and open " +
            $"Approval Requests to review request " +
            $"#{request.RequestId}.</p>";

        await emailService.SendAsync(
            request.AssignedAdminEmail,
            subject,
            body,
            "CaseApprovalRequested",
            "CaseApprovalRequest",
            request.RequestId.ToString(),
            ct);
    }

    private async Task SendRequestReviewedEmailAsync(
        CaseApprovalRequest request,
        bool approved,
        CancellationToken ct)
    {
        var decision = approved ? "Approved" : "Rejected";
        var subject =
            $"[Dental Lab] {request.RequestType} Request " +
            $"{decision} - Case #{request.CaseId}";

        var statusText = approved
            ? "The request was approved. The approved action is " +
              "awaiting completion by the assigned Admin."
            : "The request was rejected and is now closed.";

        var body =
            $"<h2>Case Request {decision}</h2>" +
            $"<p>{statusText}</p>" +
            DetailsTable(request) +
            $"<p><strong>Reviewed by:</strong> " +
            $"{Encode(request.ReviewedByEmail)}</p>" +
            $"<p><strong>Review comment:</strong><br>" +
            $"{Encode(request.ReviewComment)}</p>" +
            $"<p>Sign in to Dental Lab and open Approval Requests " +
            $"for the latest status.</p>";

        await emailService.SendAsync(
            request.RequestedByEmail,
            subject,
            body,
            approved
                ? "CaseApprovalApproved"
                : "CaseApprovalRejected",
            "CaseApprovalRequest",
            request.RequestId.ToString(),
            ct);
    }

    private async Task SendRequestCompletedEmailAsync(
        CaseApprovalRequest request,
        CancellationToken ct)
    {
        var action = request.RequestType ==
            CaseApprovalRequestTypes.Cancel
                ? "cancellation"
                : "edit";

        var subject =
            $"[Dental Lab] {request.RequestType} Request " +
            $"Completed - Case #{request.CaseId}";

        var body =
            $"<h2>Case Request Completed</h2>" +
            $"<p>The approved {Encode(action)} request for " +
            $"Case #{request.CaseId} has been completed.</p>" +
            DetailsTable(request) +
            $"<p><strong>Completed by:</strong> " +
            $"{Encode(request.CompletedByEmail)}</p>" +
            $"<p><strong>Completed on:</strong> " +
            $"{request.CompletedOn?.ToLocalTime():dd/MM/yyyy HH:mm}</p>";

        await emailService.SendAsync(
            request.RequestedByEmail,
            subject,
            body,
            request.RequestType == CaseApprovalRequestTypes.Cancel
                ? "CaseCancellationCompleted"
                : "CaseEditCompleted",
            "CaseApprovalRequest",
            request.RequestId.ToString(),
            ct);
    }

    private static string DetailsTable(
        CaseApprovalRequest request)
    {
        return
            "<table style=\"border-collapse:collapse\">" +
            Row("Request ID", $"#{request.RequestId}") +
            Row("Case ID", $"#{request.CaseId}") +
            Row("Request type", request.RequestType) +
            Row("Requested by", request.RequestedByEmail) +
            Row("Assigned Admin", request.AssignedAdminEmail) +
            Row(
                "Requested on",
                request.RequestedOn
                    .ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm")) +
            "</table>";
    }

    private static string Row(string name, string? value)
    {
        return
            "<tr>" +
            "<td style=\"padding:5px 12px 5px 0;" +
            "font-weight:bold\">" +
            Encode(name) +
            "</td>" +
            "<td style=\"padding:5px 0\">" +
            Encode(value) +
            "</td>" +
            "</tr>";
    }

    private static string AdditionalDetails(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"<p><strong>Additional details:</strong><br>" +
              $"{Encode(value)}</p>";
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? "-");
    }

    private static bool IsLocked(ApplicationUser user)
    {
        return user.LockoutEnabled &&
               user.LockoutEnd.HasValue &&
               user.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
