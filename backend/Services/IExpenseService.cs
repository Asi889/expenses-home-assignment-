using InvoiceExpenseSystem.Models;

namespace InvoiceExpenseSystem.Services;

public interface IExpenseService
{
    Task<Expense> CreateExpenseAsync(int userId, Expense expense);
    Task<List<Expense>> GetExpensesAsync(int userId, ExpenseFilter? filter = null);
    Task<Expense?> UpdateExpenseCategoryAsync(int userId, int expenseId, ExpenseCategory category);
    Task<Expense?> UpdateExpenseDocumentTypeAsync(int userId, int expenseId, DocumentType documentType);
    Task<bool> DeleteExpenseAsync(int userId, int expenseId);
}

public class ExpenseFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public ExpenseCategory? Category { get; set; }
    public string? BusinessName { get; set; }
}

