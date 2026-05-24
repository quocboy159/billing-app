using Domain.Bills;

namespace Application.Abstractions.Persistence;

public interface IBillRepository
{
    /// <summary>
    /// Stages a new bill (and any line items / taxes already attached to it via
    /// <see cref="Bill.AddLine"/> and <see cref="BillTransaction.AddTax"/>).
    /// EF's navigation-property fixup persists the children when the unit-of-work commits.
    /// </summary>
    void Add(Bill bill);
}
