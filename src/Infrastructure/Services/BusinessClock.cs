using Application.Abstractions.Services;
using SharedKernel;

namespace Infrastructure.Services;

internal sealed class BusinessClock(IDateTimeProvider clock) : IBusinessClock
{
    public Task<DateTime> CurrentDateAsync(int companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(clock.UtcNow.Date);
}
