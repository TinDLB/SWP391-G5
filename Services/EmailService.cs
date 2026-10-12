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

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(senderEmail, appPassword.Replace(" ", "")),
                    EnableSsl = enableSsl,
                    Timeout = 15000 // 15s timeout
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Đã gửi email thành công tới {ToEmail}. Tiêu đề: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email qua SMTP tới {ToEmail}", toEmail);
                // Ném exception để caller biết và xử lý (hoặc controller có thể hiển thị cảnh báo)
                throw;
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
            <h2>🥖 BMS BAKERY SHOP 🥐</h2>
        </div>
        <div class='email-body'>
            <p>Xin chào quý khách,</p>
            <p>{introText}</p>
            <div class='otp-box'>
                <div class='otp-code'>{otpCode}</div>
                <div class='otp-expiry'>⏰ Mã xác thực có hiệu lực trong vòng 5 phút</div>
            </div>
            <p style='color: #666; font-size: 13px;'>* Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này hoặc liên hệ ngay với chúng tôi để bảo vệ tài khoản.</p>
        </div>
        <div class='email-footer'>
            &copy; {DateTime.UtcNow.Year} BMS Bakery System. Chúc bạn có trải nghiệm tuyệt vời cùng tiệm bánh!
        </div>
    </div>
</body>
</html>";

            try
            {
                await SendEmailAsync(toEmail, title, htmlContent);
            }
            catch (Exception)
            {
                // Cho phép controller tiếp tục (với mã OTP vẫn có trong cache và log)
                _logger.LogWarning("Không thể gửi email OTP thực tế tới {ToEmail}. Mã OTP là: {OtpCode}", toEmail, otpCode);
            }
        }
    }
}
