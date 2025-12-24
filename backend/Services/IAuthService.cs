using InvoiceExpenseSystem.Models;

namespace InvoiceExpenseSystem.Services;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<User?> GetUserByIdAsync(int userId);
}

