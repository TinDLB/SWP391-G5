using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers;

[Authorize(Roles = "Admin")]
public class VoucherController : Controller
{
    private readonly BakeryManagementDbContext _context;
    private readonly INotificationService _notificationService;

    public VoucherController(
        BakeryManagementDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        // UI gửi search/status qua query string. Controller đọc Vouchers, tạo
        // VoucherListItemViewModel rồi truyền View(items) về Views/Voucher/Index.cshtml.
        // Nhận search/status từ query string, đọc Vouchers, tạo VoucherListItemViewModel
        // và trả danh sách đã lọc cho View quản lý voucher.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = _context.Vouchers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v => v.Code.ToLower().Contains(search.Trim().ToLower()));

        var vouchers = await query.OrderByDescending(v => v.VoucherId).ToListAsync();
        var items = vouchers.Select(v => new VoucherListItemViewModel
        {
            VoucherId = v.VoucherId,
            Code = v.Code,
            DiscountType = v.DiscountType,
            DiscountValue = v.DiscountValue,
            StartDate = v.StartDate,
            EndDate = v.EndDate,
            UsageLimit = v.UsageLimit,
            UsedCount = v.UsedCount,
            IsActive = v.IsActive,
            Status = GetStatus(v, today)
        });

        if (!string.IsNullOrWhiteSpace(status))
            items = items.Where(v => v.Status == status);

        ViewBag.Search = search;
        ViewBag.Status = status;
        return View(items.ToList());
    }

    public IActionResult Create() => View(new VoucherEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VoucherEditViewModel model)
    {
        // UI POST gửi mã, loại giảm, giá trị, thời hạn và giới hạn dùng.
        // Sau validation, các field được map và INSERT vào bảng Vouchers.
        // Nh?n Code, DiscountType, m?c gi?m, th?i h?n v� gi?i h?n d�ng t? form t?o voucher.
        model.Code = model.Code.Trim().ToUpperInvariant();
        await ValidateCodeAsync(model.Code);
        if (!ModelState.IsValid) return View(model);

        _context.Vouchers.Add(new Voucher
        {
            // Chuy?n d? li?u h?p l? t? ViewModel sang entity Voucher ?? l?u v�o database.
            Code = model.Code,
            DiscountType = model.DiscountType,
            DiscountValue = model.DiscountValue,
            MinOrderValue = model.MinOrderValue,
            MaxDiscount = model.MaxDiscount,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            UsageLimit = model.UsageLimit,
            IsActive = model.IsActive,
            UsedCount = 0
        });
        await _context.SaveChangesAsync();
        // Ghi thông báo cho các Admin vào Notifications sau khi voucher đã được lưu thành công.
        await _notificationService.NotifyAdminsAsync(
            "VoucherCreated",
            "Đã tạo voucher mới",
            $"Voucher {model.Code} đã được tạo và sẵn sàng sử dụng.");
        TempData["SuccessMessage"] = "Đã tạo voucher thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var voucher = await _context.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.VoucherId == id);
        if (voucher is null) return NotFound();
        return View(new VoucherEditViewModel
        {
            VoucherId = voucher.VoucherId,
            Code = voucher.Code,
            DiscountType = voucher.DiscountType,
            DiscountValue = voucher.DiscountValue,
            MinOrderValue = voucher.MinOrderValue,
            MaxDiscount = voucher.MaxDiscount,
            StartDate = voucher.StartDate,
            EndDate = voucher.EndDate,
            UsageLimit = voucher.UsageLimit,
            IsActive = voucher.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VoucherEditViewModel model)
    {
        // Nhận VoucherId từ route/model, query Vouchers để lấy bản ghi hiện tại,
        // sau đó UPDATE các cột voucher và trả redirect về danh sách.
        // Nh?n VoucherId t? route v� d? li?u voucher m?i t? form ch?nh s?a.
        if (id != model.VoucherId) return BadRequest();
        model.Code = model.Code.Trim().ToUpperInvariant();
        var existing = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id);
        if (existing is null) return NotFound();

        if (existing.UsedCount > 0 && existing.Code != model.Code)
            ModelState.AddModelError(nameof(model.Code), "Không thể đổi mã voucher đã được sử dụng.");
        await ValidateCodeAsync(model.Code, id);
        if (!ModelState.IsValid) return View(model);

        existing.Code = model.Code;
        // C?p nh?t c�c tr??ng voucher ?� ???c ki?m tra; kh�ng thay ??i UsedCount t?i m�n h�nh qu?n tr?.
        existing.DiscountType = model.DiscountType;
        existing.DiscountValue = model.DiscountValue;
        existing.MinOrderValue = model.MinOrderValue;
        existing.MaxDiscount = model.MaxDiscount;
        existing.StartDate = model.StartDate;
        existing.EndDate = model.EndDate;
        existing.UsageLimit = model.UsageLimit;
        existing.IsActive = model.IsActive;
        await _context.SaveChangesAsync();
        // Chỉ gửi thông báo sau SaveChanges để tránh báo thành công khi UPDATE Vouchers thất bại.
        await _notificationService.NotifyAdminsAsync(
            "VoucherUpdated",
            "Đã cập nhật voucher",
            $"Voucher {existing.Code} đã được cập nhật.");
        TempData["SuccessMessage"] = "Đã cập nhật voucher thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        // UI gửi VoucherId; Controller đọc Vouchers, đổi IsActive, SaveChanges và redirect Index.
        // Nhận VoucherId từ form và lưu trạng thái IsActive vào Vouchers.
        var voucher = await _context.Vouchers.FindAsync(id);
        if (voucher is null) return NotFound();
        voucher.IsActive = !voucher.IsActive;
        await _context.SaveChangesAsync();
        await _notificationService.NotifyAdminsAsync(
            "VoucherStatusChanged",
            "Đã thay đổi trạng thái voucher",
            $"Voucher {voucher.Code} đã {(voucher.IsActive ? "được kích hoạt" : "bị tạm ngưng")}.");
        TempData["SuccessMessage"] = $"Đã {(voucher.IsActive ? "bật" : "tắt")} voucher {voucher.Code}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCodeAsync(string code, int? excludeId = null)
    {
        // ??c m� voucher t? request v� ki?m tra duy nh?t tr??c khi t?o ho?c c?p nh?t.
        var exists = await _context.Vouchers.AnyAsync(v => v.Code == code && (!excludeId.HasValue || v.VoucherId != excludeId.Value));
        if (exists) ModelState.AddModelError(nameof(VoucherEditViewModel.Code), "Mã voucher đã tồn tại.");
    }

    private static string GetStatus(Voucher voucher, DateOnly today)
    {
        if (!voucher.IsActive) return "inactive";
        if (today < voucher.StartDate) return "upcoming";
        if (today > voucher.EndDate) return "expired";
        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value) return "exhausted";
        return "active";
    }
}
