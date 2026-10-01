using System.Security.Cryptography;

namespace SWP391_G5.Services
{
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 16; // 128-bit
        private const int KeySize = 32;  // 256-bit
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            // Hỗ trợ kiểm tra mật khẩu dạng BCrypt (dữ liệu trong BakeryManagementDB)
            if (passwordHash.StartsWith("$2a$") || passwordHash.StartsWith("$2b$") || passwordHash.StartsWith("$2y$"))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, passwordHash);
                }
                catch
                {
                    return false;
                }
            }

            // Hỗ trợ kiểm tra mật khẩu mẫu chưa băm (trong trường hợp seed data thủ công)
            if (!passwordHash.Contains(':') && password == passwordHash)
            {
                return true;
            }

            string[] parts = passwordHash.Split(':', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, KeySize);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
