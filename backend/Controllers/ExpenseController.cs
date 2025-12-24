using InvoiceExpenseSystem.Models;
using InvoiceExpenseSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvoiceExpenseSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] ExpenseCategory? category,
        [FromQuery] string? businessName)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var filter = new ExpenseFilter
        {
            StartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : null,
            EndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Category = category,
            BusinessName = businessName
        };

        var expenses = await _expenseService.GetExpensesAsync(userId, filter);
        return Ok(expenses);
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        Console.WriteLine($"Updating category for expense {id}, user {userId}, category: {request.Category}");

        var expense = await _expenseService.UpdateExpenseCategoryAsync(userId, id, request.Category);
        if (expense == null)
        {
            Console.WriteLine($"Expense {id} not found for user {userId}");
            return NotFound();
        }

        Console.WriteLine($"Category updated successfully. New category: {expense.Category}");
        return Ok(expense);
    }

    [HttpPut("{id}/document-type")]
    public async Task<IActionResult> UpdateDocumentType(int id, [FromBody] UpdateDocumentTypeRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var expense = await _expenseService.UpdateExpenseDocumentTypeAsync(userId, id, request.DocumentType);
        if (expense == null)
        {
            return NotFound();
        }

        return Ok(expense);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var deleted = await _expenseService.DeleteExpenseAsync(userId, id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}

public class UpdateCategoryRequest
{
    public ExpenseCategory Category { get; set; }
}

public class UpdateDocumentTypeRequest
{
    public DocumentType DocumentType { get; set; }
}

