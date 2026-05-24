namespace Application.Abstractions.Persistence;

public interface ICompanyRepository
{
    Task<string?> GetBusinessCurrencyAsync(int companyId, CancellationToken cancellationToken = default);
}
