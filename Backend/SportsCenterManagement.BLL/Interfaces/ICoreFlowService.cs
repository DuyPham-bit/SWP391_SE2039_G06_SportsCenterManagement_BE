using CoreFlowsResponses = SportsCenterManagement.BLL.DTOs.CoreFlows.Responses;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Interfaces;

public interface ICoreFlowService
{
    Task<IReadOnlyList<MembershipPackage>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<CoreFlowsResponses.PendingMembershipResult> CreatePendingMembershipAsync(
        long memberId,
        long packageId,
        long? createdBy,
        CancellationToken cancellationToken = default);

    Task<ClassEnrollment> EnrollMemberAsync(
        long classId,
        long memberId,
        long subscriptionId,
        long? registeredBy,
        CancellationToken cancellationToken = default);

    Task<Payment> RecordCashPaymentAsync(
        long invoiceId,
        long processedBy,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<CoreFlowsResponses.RevenueSummary> GetRevenueAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
