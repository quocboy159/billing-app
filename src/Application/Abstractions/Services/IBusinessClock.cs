namespace Application.Abstractions.Services;

public interface IBusinessClock
{
    Task<DateTime> CurrentDateAsync(int companyId, CancellationToken cancellationToken = default);
}
