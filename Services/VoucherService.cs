using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Services;

public sealed class VoucherService : IVoucherService
{
        // Nh?n mã voucher và t?ng ti?n ??n hàng t? cart/POS/checkout ?? ki?m tra t?p trung.
    private readonly BakeryManagementDbContext _context;

    public VoucherService(BakeryManagementDbContext context)
    {
        _context = context;
    }

    public async Task<VoucherValidationResult> ValidateAndCalculateAsync(
        string code,
        decimal orderTotal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return VoucherValidationResult.Invalid("Vui lòng nh?p mã voucher.");

        if (orderTotal < 0)
            return VoucherValidationResult.Invalid("Giá tr? ??n hàng không h?p l?.");

        var voucher = await _context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == code.Trim().ToUpper(), cancellationToken);

        if (voucher is null)
            return VoucherValidationResult.Invalid("Mã voucher không t?n t?i.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!voucher.IsActive)
            return VoucherValidationResult.Invalid("Voucher ?ã b? t?t.");
        if (today < voucher.StartDate || today > voucher.EndDate)
            return VoucherValidationResult.Invalid("Voucher không n?m trong th?i gian s? d?ng.");
        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
            return VoucherValidationResult.Invalid("Voucher ?ã h?t l??t s? d?ng.");
        if (orderTotal < voucher.MinOrderValue)
            return VoucherValidationResult.Invalid($"??n hàng t?i thi?u {voucher.MinOrderValue:N0} ?.");

        return VoucherValidationResult.Valid(CalculateDiscount(voucher, orderTotal));
    }

    public decimal CalculateDiscount(Voucher voucher, decimal orderTotal)
    {
        // Nh?n Voucher ?ã h?p l? và t?ng ti?n ??n hàng, sau ?ó tr? v? s? ti?n ???c gi?m.
        var discount = voucher.DiscountType == "Percent"
            ? orderTotal * voucher.DiscountValue / 100m
            : voucher.DiscountValue;

        if (voucher.DiscountType == "Percent" && voucher.MaxDiscount.HasValue)
            discount = Math.Min(discount, voucher.MaxDiscount.Value);

        return Math.Min(Math.Max(discount, 0m), Math.Max(orderTotal, 0m));
    }
}
