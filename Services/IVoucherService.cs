using SWP391_G5.Models.Entities;

namespace SWP391_G5.Services;

public sealed record VoucherValidationResult(bool IsValid, string? ErrorMessage, decimal DiscountAmount)
{
    public static VoucherValidationResult Invalid(string message) => new(false, message, 0m);
    public static VoucherValidationResult Valid(decimal amount) => new(true, null, amount);
}

public interface IVoucherService
{
    Task<VoucherValidationResult> ValidateAndCalculateAsync(string code, decimal orderTotal, CancellationToken cancellationToken = default);
    decimal CalculateDiscount(Voucher voucher, decimal orderTotal);
}
