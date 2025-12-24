using InvoiceExpenseSystem.Models;
using InvoiceExpenseSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace InvoiceExpenseSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Email and password are required" });
        }

        var result = await _authService.RegisterAsync(request);
        if (result == null)
        {
            return BadRequest(new { message = "Email already exists" });
        }

        // Set JWT token in httpOnly cookie
        SetTokenCookie(result.Token);

        return Ok(new { email = result.Email, message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Email and password are required" });
        }

        var result = await _authService.LoginAsync(request);
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Set JWT token in httpOnly cookie
        SetTokenCookie(result.Token);

        return Ok(new { email = result.Email, message = "Login successful" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear the cookie
        Response.Cookies.Delete("authToken");
        return Ok(new { message = "Logged out successfully" });
    }

    private void SetTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Prevents JavaScript access (XSS protection)
            Secure = false, // Set to true in production with HTTPS
            SameSite = SameSiteMode.Lax, // CSRF protection
            Expires = DateTimeOffset.UtcNow.AddDays(7) // Same as JWT expiration
        };

        Response.Cookies.Append("authToken", token, cookieOptions);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new { id = user.Id, email = user.Email });
    }
}

