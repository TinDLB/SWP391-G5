using System.Threading.Tasks;

namespace SWP391_G5.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent);
        Task SendOtpEmailAsync(string toEmail, string otpCode, string purpose);
    }
}
