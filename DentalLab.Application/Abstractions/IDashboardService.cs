using DentalLab.Application.Models.Dashboard;

namespace DentalLab.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardData> GetDashboardAsync(
        DashboardFilter filter,
        string currentUserId,
        string currentUserEmail,
        string role,
        CancellationToken cancellationToken = default);
}
