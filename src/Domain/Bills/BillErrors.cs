using SharedKernel;

namespace Domain.Bills;

public static class BillErrors
{
    public static Error NotFound(Guid guid) =>
        Error.NotFound("Bills.NotFound", $"Bill with guid '{guid}' was not found.");

    public static Error InsertFailed() =>
        Error.Problem("Bills.InsertFailed", "BILL TABLE ZERO RECORD");

    public static Error PlanNotAllowed(string message) =>
        Error.Problem("Bills.PlanNotAllowed", message);

    public static Error VendorInvalid() =>
        Error.Problem("Bills.VendorInvalid", "The selected vendor is invalid!");

    public static Error VendorAlreadyExists() =>
        Error.Conflict("Bills.VendorAlreadyExists", "Vendor already exists!");
}
