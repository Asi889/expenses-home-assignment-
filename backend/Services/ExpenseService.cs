using InvoiceExpenseSystem.Data;
using InvoiceExpenseSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceExpenseSystem.Services;

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _context;

    public ExpenseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Expense> CreateExpenseAsync(int userId, Expense expense)
    {
        // Verify user exists
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            throw new InvalidOperationException($"User with ID {userId} does not exist");
        }
        
        expense.UserId = userId;
        expense.CreatedAt = DateTime.UtcNow;
        
        // Ensure BusinessName is not empty (database constraint)
        if (string.IsNullOrWhiteSpace(expense.BusinessName))
        {
            expense.BusinessName = "Unknown Business";
        }
        
        // Ensure category defaults to Other if not set
        if (!Enum.IsDefined(typeof(ExpenseCategory), expense.Category))
        {
            expense.Category = ExpenseCategory.Other;
        }
        
        // Ensure amounts are not negative
        if (expense.AmountAfterVat < 0)
        {
            expense.AmountAfterVat = 0;
        }
        if (expense.AmountBeforeVat < 0)
        {
            expense.AmountBeforeVat = 0;
        }
        
        // Clear navigation property to avoid tracking issues
        expense.User = null!;
        
        try
        {
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();
            
            // Return the saved expense (EF will populate the Id)
            return expense;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving expense: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Expense data: BusinessName={expense.BusinessName}, AmountAfterVat={expense.AmountAfterVat}, UserId={expense.UserId}, TransactionDate={expense.TransactionDate}, DocumentType={expense.DocumentType}");
            throw;
        }
    }

    public async Task<List<Expense>> GetExpensesAsync(int userId, ExpenseFilter? filter = null)
    {
        var query = _context.Expenses.Where(e => e.UserId == userId);

        if (filter != null)
        {
            if (filter.StartDate.HasValue)
                query = query.Where(e => e.TransactionDate >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(e => e.TransactionDate <= filter.EndDate.Value);

            if (filter.MinAmount.HasValue)
                query = query.Where(e => e.AmountAfterVat >= filter.MinAmount.Value);

            if (filter.MaxAmount.HasValue)
                query = query.Where(e => e.AmountAfterVat <= filter.MaxAmount.Value);

            if (filter.Category.HasValue)
                query = query.Where(e => e.Category == filter.Category.Value);

            if (!string.IsNullOrEmpty(filter.BusinessName))
                query = query.Where(e => e.BusinessName.Contains(filter.BusinessName));
        }

        return await query.OrderByDescending(e => e.TransactionDate).ToListAsync();
    }

    public async Task<Expense?> UpdateExpenseCategoryAsync(int userId, int expenseId, ExpenseCategory category)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense == null)
        {
            Console.WriteLine($"Expense {expenseId} not found for user {userId}");
            return null;
        }

        Console.WriteLine($"Updating expense {expenseId} category from {expense.Category} to {category}");
        expense.Category = category;
        
        // Mark the entity as modified to ensure EF tracks the change
        _context.Entry(expense).Property(e => e.Category).IsModified = true;
        
        var rowsAffected = await _context.SaveChangesAsync();
        Console.WriteLine($"SaveChangesAsync completed. Rows affected: {rowsAffected}");
        Console.WriteLine($"Expense {expenseId} category after save: {expense.Category}");

        // Detach and reload to ensure we have the latest from database
        _context.Entry(expense).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var reloadedExpense = await _context.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);
        
        if (reloadedExpense != null)
        {
            Console.WriteLine($"Reloaded expense {expenseId} category from DB: {reloadedExpense.Category}");
            return reloadedExpense;
        }

        return expense;
    }

    public async Task<Expense?> UpdateExpenseDocumentTypeAsync(int userId, int expenseId, DocumentType documentType)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense == null)
            return null;

        expense.DocumentType = documentType;
        // Also update IsReceipt for backwards compatibility
        expense.IsReceipt = documentType == DocumentType.Receipt;
        await _context.SaveChangesAsync();

        return expense;
    }

    public async Task<bool> DeleteExpenseAsync(int userId, int expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense == null)
            return false;

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        return true;
    }
}

