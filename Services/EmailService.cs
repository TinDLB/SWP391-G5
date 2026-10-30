using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SWP391_G5.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var port = int.TryParse(_configuration["EmailSettings:Port"], out var p) ? p : 587;
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "bmsbakery.swp@gmail.com";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "BMS Bakery Shop";
            var appPassword = _configuration["EmailSettings:AppPassword"] ?? "";
            var enableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var ssl) ? ssl : true;

            // Nếu mật khẩu ứng dụng chưa được cấu hình hoặc là chuỗi mẫu, log lại để hỗ trợ dev/test
            if (string.IsNullOrWhiteSpace(appPassword) || appPassword.Contains("xxxx"))
            {
                _logger.LogWarning("EmailSettings:AppPassword chưa được cấu hình thực tế. Email tới {ToEmail} với tiêu đề '{Subject}' sẽ chỉ được ghi log.", toEmail, subject);
                return;
            }

            // Thử gửi tối đa 3 lần nếu bị timeout hoặc lỗi tạm thời
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var message = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = subject,
                        Body = htmlContent,
                        IsBodyHtml = true
                    };
                    message.SubjectEncoding = System.Text.Encoding.UTF8;
                    message.BodyEncoding = System.Text.Encoding.UTF8;
                    message.To.Add(toEmail);

                    using var client = new SmtpClient(smtpServer, port)
                    {
                        Credentials = new NetworkCredential(senderEmail, appPassword.Replace(" ", "")),
                        EnableSsl = enableSsl,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Timeout = 30000 // 30s timeout (tăng từ 15s)
                    };

                    await client.SendMailAsync(message);
                    _logger.LogInformation("Đã gửi email thành công tới {ToEmail}. Tiêu đề: {Subject} (lần thử {Attempt})", toEmail, subject, attempt);
                    return; // Gửi thành công, thoát vòng lặp
                }
                catch (SmtpException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Lỗi SMTP khi gửi email tới {ToEmail} (lần thử {Attempt}/{MaxRetries}). Sẽ thử lại sau 2 giây...", toEmail, attempt, maxRetries);
                    await Task.Delay(2000 * attempt); // Chờ 2s, 4s trước khi thử lại
                }
                catch (Exception ex) when (attempt < maxRetries && (ex.InnerException is System.IO.IOException || ex.InnerException is TimeoutException))
                {
                    _logger.LogWarning(ex, "Lỗi kết nối khi gửi email tới {ToEmail} (lần thử {Attempt}/{MaxRetries}). Sẽ thử lại...", toEmail, attempt, maxRetries);
                    await Task.Delay(2000 * attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi email qua SMTP tới {ToEmail} (lần thử {Attempt}/{MaxRetries})", toEmail, attempt, maxRetries);
                    throw; // Lần cuối cùng hoặc lỗi không thể retry -> ném ra ngoài
                }
            }
        }

        public async Task SendOtpEmailAsync(string toEmail, string otpCode, string purpose)
        {
            var title = purpose == "Register" 
                ? "Mã xác thực đăng ký tài khoản BMS Bakery" 
                : "Mã xác thực đặt lại mật khẩu BMS Bakery";

            var introText = purpose == "Register"
                ? "Cảm ơn bạn đã đăng ký tài khoản tại <strong>Tiệm Bánh BMS Bakery</strong>. Vui lòng nhập mã OTP bên dưới để kích hoạt tài khoản của bạn:"
                : "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản tại <strong>BMS Bakery</strong>. Vui lòng sử dụng mã OTP bên dưới để tiến hành tạo mật khẩu mới:";

            var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8f5f0; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 550px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #ebd9c8; }}
        .email-header {{ background: linear-gradient(135deg, #4A2E18, #6f4423); color: #ffffff; padding: 25px; text-align: center; }}
        .email-header h2 {{ margin: 0; font-size: 24px; font-weight: 700; letter-spacing: 1px; color: #DDA15E; }}
        .email-body {{ padding: 30px 25px; color: #333333; line-height: 1.6; font-size: 15px; }}
        .otp-box {{ background-color: #fff9f2; border: 2px dashed #DDA15E; border-radius: 10px; padding: 18px; text-align: center; margin: 25px 0; }}
        .otp-code {{ font-size: 34px; font-weight: 800; color: #4A2E18; letter-spacing: 8px; font-family: monospace; }}
        .otp-expiry {{ color: #dc3545; font-size: 13px; margin-top: 8px; font-weight: 600; }}
        .email-footer {{ background-color: #faf7f2; border-top: 1px solid #eee; padding: 15px; text-align: center; font-size: 12px; color: #888888; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='email-header'>
            <h2>BMS BAKERY SHOP</h2>
        </div>
        <div class='email-body'>
            <p>Xin chao quy khach,</p>
            <p>{introText}</p>
            <div class='otp-box'>
                <div class='otp-code'>{otpCode}</div>
                <div class='otp-expiry'>Ma xac thuc co hieu luc trong vong 5 phut</div>
            </div>
            <p style='color: #666; font-size: 13px;'>* Neu ban khong thuc hien yeu cau nay, vui long bo qua email nay hoac lien he ngay voi chung toi de bao ve tai khoan.</p>
        </div>
        <div class='email-footer'>
            &copy; {DateTime.UtcNow.Year} BMS Bakery System. Chuc ban co trai nghiem tuyet voi cung tiem banh!
        </div>
    </div>
</body>
</html>";

            try
            {
                await SendEmailAsync(toEmail, title, htmlContent);
                _logger.LogInformation("Đã gửi OTP thành công tới {ToEmail}. Mục đích: {Purpose}", toEmail, purpose);
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết nhưng cho phép controller tiếp tục (mã OTP vẫn có trong cache và log)
                _logger.LogError(ex, "Không thể gửi email OTP tới {ToEmail}. Mã OTP là: {OtpCode}. Mục đích: {Purpose}", toEmail, otpCode, purpose);
            }
        }
    }
}
