using InvoiceExpenseSystem.Models;
using InvoiceExpenseSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvoiceExpenseSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IExpenseService _expenseService;

    public InvoiceController(IInvoiceService invoiceService, IExpenseService expenseService)
    {
        _invoiceService = invoiceService;
        _expenseService = expenseService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadInvoice(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new { message = "Invalid file type. Only PDF and images are allowed." });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            // Generate a permanent filename
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }
            var filePath = Path.Combine(uploadsPath, fileName);

            // Save the file permanently
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Analyze the file
            using var stream = file.OpenReadStream();
            var analysisResult = await _invoiceService.AnalyzeInvoiceAsync(stream, file.FileName);

            if (analysisResult == null)
            {
                return BadRequest(new { message = "Failed to analyze invoice" });
            }

            // Use OCR-detected document type
            DocumentType finalDocumentType = DocumentType.Receipt;
            
            if (Enum.TryParse<DocumentType>(analysisResult.DocumentType, out var detectedType))
            {
                finalDocumentType = detectedType;
            }
            else
            {
                // Fallback mapping
                finalDocumentType = analysisResult.DocumentType switch
                {
                    "TaxInvoiceReceipt" => DocumentType.TaxInvoiceReceipt,
                    "TaxInvoice" => DocumentType.TaxInvoice,
                    _ => DocumentType.Receipt
                };
            }
            Console.WriteLine($"Using OCR-detected document type: {finalDocumentType}");

            // Create expense from analysis result
            var expense = new Expense
            {
                UserId = userId,
                BusinessName = string.IsNullOrWhiteSpace(analysisResult.BusinessName) ? "Unknown Business" : analysisResult.BusinessName,
                // Ensure TransactionDate is UTC (PostgreSQL requirement)
                TransactionDate = analysisResult.TransactionDate.Kind == DateTimeKind.Utc 
                    ? analysisResult.TransactionDate 
                    : analysisResult.TransactionDate.ToUniversalTime(),
                AmountBeforeVat = analysisResult.AmountBeforeVat >= 0 ? analysisResult.AmountBeforeVat : 0,
                AmountAfterVat = analysisResult.AmountAfterVat >= 0 ? analysisResult.AmountAfterVat : 0,
                InvoiceNumber = analysisResult.InvoiceNumber,
                TaxId = analysisResult.TaxId,
                ServiceProvided = analysisResult.ServiceProvided,
                IsReceipt = analysisResult.IsReceipt,
                DocumentType = finalDocumentType,
                Category = ExpenseCategory.Other,
                FileName = fileName, // Store the permanent filename
                CreatedAt = DateTime.UtcNow
            };

            var createdExpense = await _expenseService.CreateExpenseAsync(userId, expense);

            return Ok(new
            {
                expense = createdExpense,
                analysis = analysisResult
            });
        }
        catch (Exception ex)
        {
            // Log full error details for debugging
            Console.WriteLine($"Error processing invoice: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            
            return StatusCode(500, new { 
                message = "Error processing invoice", 
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }
}

