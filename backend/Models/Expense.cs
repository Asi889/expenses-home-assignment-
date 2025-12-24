namespace InvoiceExpenseSystem.Models;

public enum ExpenseCategory
{
    Vehicle,        // רכב
    Food,           // מזון
    Operations,     // תפעול
    IT,             // IT
    Training,       // הדרכה/הכשרה
    Other           // אחר
}

public enum DocumentType
{
    Receipt,                    // קבלה
    TaxInvoice,                 // חשבונית מס
    TaxInvoiceReceipt          // חשבונית מס קבלה
}

public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string BusinessName { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal AmountBeforeVat { get; set; }
    public decimal AmountAfterVat { get; set; }
    public string? InvoiceNumber { get; set; }
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public string? ServiceProvided { get; set; }
    public string? TaxId { get; set; }
    public bool IsReceipt { get; set; } // true for receipt, false for invoice (deprecated - use DocumentType instead)
    public DocumentType DocumentType { get; set; } = DocumentType.Receipt; // קבלה, חשבונית מס, חשבונית מס קבלה
    public string? FileName { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

