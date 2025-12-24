namespace InvoiceExpenseSystem.Services;

public interface IInvoiceService
{
    Task<InvoiceAnalysisResult?> AnalyzeInvoiceAsync(Stream fileStream, string fileName);
}

public class InvoiceAnalysisResult
{
    public bool IsReceipt { get; set; } // Deprecated - use DocumentType instead
    public string DocumentType { get; set; } = "Receipt"; // Receipt, TaxInvoice, TaxInvoiceReceipt
    public decimal AmountBeforeVat { get; set; }
    public decimal AmountAfterVat { get; set; }
    public DateTime TransactionDate { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? ServiceProvided { get; set; }
    public string? InvoiceNumber { get; set; }
}

