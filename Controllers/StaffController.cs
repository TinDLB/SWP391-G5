using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers;

[Authorize(Roles = "Admin")]
public class StaffController : Controller
{
    private readonly BakeryManagementDbContext _context; //dbcontext của database
    private readonly IPasswordService _passwordService;  //service để hash password

    public StaffController(BakeryManagementDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<IActionResult> Index(string? search, string? role, string? status) //Nhận các tham số search, role, status từ query string để lọc danh sách nhân viên
    {
        // Nhận các tham số search, role, status từ query string để lọc danh sách nhân viên
        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.Status = status;

        // Truy vấn dữ liệu nhân viên từ Users;
        var query = _context.Users
            .Where(u => u.Role == "Cashier" || u.Role == "Baker") //Chỉ lấy những người dùng có vai trò là "Cashier" hoặc "Baker"
            .AsNoTracking(); //AsNoTracking() để tăng hiệu suất khi chỉ đọc dữ liệu mà không cần theo dõi thay đổi.

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower(); // Chuẩn hóa từ khóa tìm kiếm: loại bỏ khoảng trắng ở đầu và cuối, chuyển về chữ thường
            query = query.Where(u => u.FullName.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword)); // Lọc theo tên đầy đủ hoặc email nếu có từ khóa tìm kiếm
        }

        if (role is "Cashier" or "Baker") query = query.Where(u => u.Role == role); // Lọc theo vai trò nếu role là "Cashier" hoặc "Baker"
        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "inactive") query = query.Where(u => !u.IsActive);

        // Chuyển đổi kết quả truy vấn thành danh sách StaffListItemViewModel để hiển thị trên giao diện.
        var staff = await query.OrderBy(u => u.Role).ThenBy(u => u.FullName) // Sắp xếp theo vai trò và tên đầy đủ
            .Select(u => new StaffListItemViewModel
            {
                UserId = u.UserId,
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                Gender = u.Gender,
                Address = u.Addresses
                    .Where(a => a.IsDefault)
                    .Select(a => a.AddressLine)
                    .FirstOrDefault(),
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(); // Chuyển đổi kết quả truy vấn thành danh sách StaffListItemViewModel để hiển thị trên giao diện.

        return View(staff); // Trả về view với danh sách nhân viên đã lọc và sắp xếp
    }

    public IActionResult Create() => View(new StaffEditViewModel()); // Trả về view tạo nhân viên mới với một StaffEditViewModel rỗng 

    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffEditViewModel model) // Nhận dữ liệu từ form POST để tạo nhân viên mới
    {
        // Chuẩn hóa trước khi kiểm tra dữ liệu.
        Normalize(model); // Chuẩn hóa dữ liệu trước khi kiểm tra: loại bỏ khoảng trắng, chuyển email về chữ thường
        await ValidateEmailAsync(model.Email); // Kiểm tra email đã tồn tại trong cơ sở dữ liệu hay chưa
        if (!ModelState.IsValid) return View(model); // Nếu dữ liệu không hợp lệ, trả về view với model để hiển thị lỗi

        // Tạo một đối tượng User mới từ dữ liệu trong model và hash mật khẩu trước khi lưu vào cơ sở dữ liệu.
        var user = new User
        {
            Email = model.Email,
            FullName = model.FullName,
            Phone = model.Phone,
            Gender = model.Gender,
            Role = model.Role,
            PasswordHash = _passwordService.HashPassword(model.Password!),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user); //Thêm dữ liệu user vào database
        await _context.SaveChangesAsync();
        await SaveStaffAddressAsync(user, model);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã tạo tài khoản nhân viên thành công.";
        return RedirectToAction(nameof(Index)); // Sau khi tạo thành công, chuyển hướng về trang danh sách nhân viên
    }

    public async Task<IActionResult> Edit(int id) 
    {
        // Nhận UserId từ route và truy vấn dữ liệu nhân viên từ cơ sở dữ liệu để hiển thị trên form chỉnh sửa.
        var user = await _context.Users
            .Include(u => u.Addresses)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == id && (u.Role == "Cashier" || u.Role == "Baker"));
        if (user is null) return NotFound();

        return View(new StaffEditViewModel
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Gender = user.Gender,
            Address = user.Addresses.FirstOrDefault(a => a.IsDefault)?.AddressLine,
            Role = user.Role,
            IsActive = user.IsActive
        });
    }

    // Hai cái edit này là khác nhau, một cái là GET để hiển thị form, một cái là POST để nhận dữ liệu từ form và cập nhật vào cơ sở dữ liệu.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StaffEditViewModel model) // Nhận dữ liệu từ form POST để cập nhật thông tin nhân viên
    {
        // Nhận UserId từ route và kiểm tra xem có trùng với UserId trong model hay không để tránh lỗi bảo mật.
        if (id != model.UserId) return BadRequest();
        Normalize(model);
        await ValidateEmailAsync(model.Email, id);
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.UserId == id && (u.Role == "Cashier" || u.Role == "Baker"));
        if (user is null) return NotFound(); // Kiểm tra xem nhân viên có tồn tại trong cơ sở dữ liệu hay không, nếu không thì trả về NotFound.

        // Cập nhật thông tin nhân viên từ model vào đối tượng User và hash mật khẩu nếu có thay đổi.
        user.Email = model.Email;
        user.FullName = model.FullName;
        user.Phone = model.Phone?.Trim();
        user.Gender = model.Gender?.Trim();
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.Password))
            user.PasswordHash = _passwordService.HashPassword(model.Password);

        await SaveStaffAddressAsync(user, model);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật thông tin nhân viên.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        // Nhận UserId từ route và truy vấn dữ liệu nhân viên từ cơ sở dữ liệu để thay đổi trạng thái hoạt động (active/inactive).
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id && (u.Role == "Cashier" || u.Role == "Baker"));
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã {(user.IsActive ? "kích hoạt" : "tạm khóa")} tài khoản {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateEmailAsync(string email, int? excludeId = null)
    {
        // Kiểm tra xem email đã tồn tại trong cơ sở dữ liệu hay chưa, loại trừ UserId nếu có để tránh lỗi khi chỉnh sửa thông tin nhân viên.
        var exists = await _context.Users.AnyAsync(u => u.Email == email && (!excludeId.HasValue || u.UserId != excludeId.Value));
        if (exists) ModelState.AddModelError(nameof(StaffEditViewModel.Email), "Email đã được sử dụng.");
    }

    private static void Normalize(StaffEditViewModel model)
    {
        // Chuyển đổi email về chữ thường và loại bỏ khoảng trắng ở đầu và cuối, cũng như loại bỏ khoảng trắng ở đầu và cuối của tên đầy đủ và vai trò.
        model.Email = model.Email.Trim().ToLowerInvariant(); // Chuyển email về chữ thường và loại bỏ khoảng trắng ở đầu và cuối
        model.FullName = model.FullName.Trim();
        model.Role = model.Role.Trim();
    }

    private Task SaveStaffAddressAsync(User user, StaffEditViewModel model)
    {
        var addressText = model.Address?.Trim();
        var address = user.Addresses.FirstOrDefault(a => a.IsDefault);

        if (string.IsNullOrWhiteSpace(addressText))
        {
            if (address is not null)
                _context.Addresses.Remove(address);
            return Task.CompletedTask;
        }

        if (address is null)
        {
            user.Addresses.Add(new Address
            {
                ReceiverName = user.FullName,
                Phone = user.Phone ?? string.Empty,
                AddressLine = addressText,
                IsDefault = true
            });
        }
        else
        {
            address.ReceiverName = user.FullName;
            address.Phone = user.Phone ?? string.Empty;
            address.AddressLine = addressText;
        }

        return Task.CompletedTask;
    }
}
