using Application.Abstractions.Persistence;
using Domain.Bills;
using Infrastructure.Database;

namespace Infrastructure.Persistence;

internal sealed class BillRepository(ApplicationDbContext context) : IBillRepository
{
    public void Add(Bill bill) => context.Bills.Add(bill);
}
