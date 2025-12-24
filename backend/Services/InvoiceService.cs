using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace InvoiceExpenseSystem.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IConfiguration _configuration;

    public InvoiceService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public async Task<InvoiceAnalysisResult?> AnalyzeInvoiceAsync(Stream fileStream, string fileName)
    {
        // OCR implementation using OCR.space API (free tier: 25,000 requests/month)
        // No system dependencies required - works in any deployment environment
        
        Console.WriteLine($"\n=== Starting Invoice Analysis ===");
        Console.WriteLine($"File name: {fileName}");
        
        // Save file temporarily
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(fileName));
        try
        {
            using (var fileStream2 = new FileStream(tempPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(fileStream2);
            }
            
            var fileInfo = new FileInfo(tempPath);
            Console.WriteLine($"File saved temporarily: {tempPath}");
            Console.WriteLine($"File size: {fileInfo.Length} bytes");

            // Extract text using OCR (Google Vision or OCR.space)
            var extractedText = await ExtractTextWithOCR(tempPath);

            // Parse the extracted text
            var result = ParseInvoiceText(extractedText);
            
            Console.WriteLine($"=== Invoice Analysis Complete ===\n");
            return result;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                Console.WriteLine($"Temporary file deleted: {tempPath}");
            }
        }
    }

    private async Task<string> ExtractTextWithOCR(string imagePath)
    {
        Console.WriteLine($"\n--- Starting OCR Extraction ---");
        Console.WriteLine($"Processing file: {Path.GetFileName(imagePath)}");
        
        // Check if Google Vision is configured and preferred
        var useGoogleVision = Environment.GetEnvironmentVariable("GOOGLE_VISION_USE")?.ToLower() == "true"
            || _configuration.GetValue<bool>("GoogleVision:UseGoogleVision", false);
        var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_VISION_API_KEY") 
            ?? _configuration["GoogleVision:ApiKey"];
        
        if (useGoogleVision && !string.IsNullOrWhiteSpace(googleApiKey))
        {
            Console.WriteLine("Using Google Vision API for OCR (Hebrew + English)...");
            var googleResult = await ExtractTextWithGoogleVision(imagePath, googleApiKey);
            
            // If Google Vision fails, show error but don't fallback
            if (string.IsNullOrWhiteSpace(googleResult))
            {
                Console.WriteLine("❌ Google Vision failed - no text extracted");
                Console.WriteLine("   Make sure billing is enabled: https://console.cloud.google.com/billing/enable");
                return string.Empty;
            }
            
            return googleResult;
        }
        
        // If Google Vision is not configured, show error
        Console.WriteLine("❌ Google Vision is not enabled!");
        Console.WriteLine("   Set GOOGLE_VISION_USE=true and GOOGLE_VISION_API_KEY in .env file");
        Console.WriteLine("   Falling back to OCR.space (poor Hebrew support)...");
        
        // Use OCR.space only as last resort
        Console.WriteLine("Using OCR.space API for OCR...");
        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                // Read image file
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                Console.WriteLine($"File size: {imageBytes.Length} bytes");
                
                // Prepare request content with file upload
                var content = new MultipartFormDataContent();
                
                // Add image file
                var fileContent = new ByteArrayContent(imageBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", Path.GetFileName(imagePath));
                
                // OCR.space API parameters
                // Get API key from environment variable or configuration
                var apiKey = Environment.GetEnvironmentVariable("OCR_SPACE_API_KEY") 
                    ?? _configuration["OcrSpace:ApiKey"] 
                    ?? "helloworld";
                var apiUrl = _configuration["OcrSpace:ApiUrl"] ?? "https://api.ocr.space/parse/image";
                
                // Note: OCR.space free tier with "helloworld" key has language restrictions
                // Try without language parameter first (auto-detect), or get a real API key
                var language = _configuration["OcrSpace:Language"];
                if (!string.IsNullOrWhiteSpace(language) && language != "auto")
                {
                    content.Add(new StringContent(language), "language");
                    Console.WriteLine($"Language parameter: {language}");
                }
                else
                {
                    Console.WriteLine("Language: auto-detect (no language parameter)");
                }
                
                content.Add(new StringContent("false"), "isOverlayRequired"); // Set to false to avoid overlay data issues
                content.Add(new StringContent(apiKey), "apikey"); // Free API key for free tier
                
                Console.WriteLine("Calling OCR.space API...");
                Console.WriteLine($"API URL: {apiUrl}");
                Console.WriteLine($"API Key: {apiKey.Substring(0, Math.Min(5, apiKey.Length))}... (hidden for security)");
                Console.WriteLine("Note: For better Hebrew support, get a free API key from https://ocr.space/ocrapi/freekey");
                
                // Call OCR.space API
                var response = await httpClient.PostAsync(apiUrl, content);
                
                Console.WriteLine($"OCR API Response Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"OCR API Raw Response (full JSON, {jsonResponse.Length} chars):");
                    Console.WriteLine(jsonResponse);
                    Console.WriteLine($"--- END OF JSON RESPONSE ---");
                    
                    OcrSpaceResponse? ocrResult = null;
                    try
                    {
                        ocrResult = JsonSerializer.Deserialize<OcrSpaceResponse>(jsonResponse, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true
                        });
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"❌ JSON Deserialization Error: {jsonEx.Message}");
                        Console.WriteLine($"JSON Error Path: {jsonEx.Path}");
                        Console.WriteLine($"JSON Error Line: {jsonEx.LineNumber}");
                        
                        // Try to manually extract text from JSON using regex as fallback
                        Console.WriteLine("Attempting fallback text extraction using regex...");
                        var textMatches = System.Text.RegularExpressions.Regex.Matches(jsonResponse, @"""ParsedText""\s*:\s*""((?:[^""\\]|\\.)*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (textMatches.Count > 0)
                        {
                            var extractedTexts = new List<string>();
                            foreach (System.Text.RegularExpressions.Match match in textMatches)
                            {
                                if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                                {
                                    // Unescape JSON string
                                    var text = match.Groups[1].Value
                                        .Replace("\\n", "\n")
                                        .Replace("\\r", "\r")
                                        .Replace("\\t", "\t")
                                        .Replace("\\\"", "\"")
                                        .Replace("\\\\", "\\");
                                    extractedTexts.Add(text);
                                }
                            }
                            
                            if (extractedTexts.Count > 0)
                            {
                                var allText = string.Join(" ", extractedTexts);
                                Console.WriteLine($"✓ Fallback extraction successful: {allText.Length} characters from {extractedTexts.Count} matches");
                                return allText;
                            }
                        }
                        
                        // Try using JsonDocument for more flexible parsing
                        Console.WriteLine("Attempting fallback using JsonDocument...");
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("ParsedResults", out var parsedResults) && parsedResults.ValueKind == JsonValueKind.Array)
                                {
                                    var texts = new List<string>();
                                    foreach (var result in parsedResults.EnumerateArray())
                                    {
                                        if (result.TryGetProperty("ParsedText", out var parsedText) && parsedText.ValueKind == JsonValueKind.String)
                                        {
                                            var text = parsedText.GetString();
                                            if (!string.IsNullOrWhiteSpace(text))
                                            {
                                                texts.Add(text);
                                            }
                                        }
                                    }
                                    if (texts.Count > 0)
                                    {
                                        var allText = string.Join(" ", texts);
                                        Console.WriteLine($"✓ JsonDocument fallback successful: {allText.Length} characters");
                                        return allText;
                                    }
                                }
                            }
                        }
                        catch (Exception docEx)
                        {
                            Console.WriteLine($"JsonDocument fallback also failed: {docEx.Message}");
                        }
                        
                        return string.Empty;
                    }
                    
                    if (ocrResult == null)
                    {
                        Console.WriteLine($"❌ Failed to deserialize OCR response");
                        return string.Empty;
                    }
                    
                    // Check for errors
                    if (ocrResult.IsErroredOnProcessing)
                    {
                        Console.WriteLine($"❌ OCR API Processing Error: {ocrResult.ErrorMessage}");
                        Console.WriteLine($"OCR Exit Code: {ocrResult.OCRExitCode}");
                        return string.Empty;
                    }
                    
                    // Extract text from all parsed results
                    if (ocrResult.ParsedResults != null && ocrResult.ParsedResults.Length > 0)
                    {
                        Console.WriteLine($"✓ OCR returned {ocrResult.ParsedResults.Length} parsed result(s)");
                        
                        var allText = string.Join(" ", ocrResult.ParsedResults
                            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ParsedText))
                            .Select(r => r.ParsedText!));
                        
                        Console.WriteLine($"Extracted text length: {allText.Length} characters");
                        
                        if (!string.IsNullOrWhiteSpace(allText))
                        {
                            var previewText = allText.Substring(0, Math.Min(500, allText.Length));
                            Console.WriteLine($"Extracted text preview (first 500 chars):");
                            Console.WriteLine($"--- START TEXT ---");
                            Console.WriteLine(previewText);
                            Console.WriteLine($"--- END TEXT ---");
                            if (allText.Length > 500)
                            {
                                Console.WriteLine($"... (truncated, showing first 500 of {allText.Length} chars)");
                            }
                            
                            Console.WriteLine($"✓ OCR extraction successful!");
                            return allText;
                        }
                        else
                        {
                            Console.WriteLine($"⚠ WARNING: OCR returned empty text!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠ WARNING: OCR API returned no parsed results!");
                        Console.WriteLine($"ParsedResults is null: {ocrResult.ParsedResults == null}");
                        if (ocrResult.ParsedResults != null)
                        {
                            Console.WriteLine($"ParsedResults length: {ocrResult.ParsedResults.Length}");
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ OCR API HTTP Error: {response.StatusCode}");
                    Console.WriteLine($"Error Response: {errorContent}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OCR Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
        
        Console.WriteLine($"❌ OCR extraction failed - returning empty string");
        Console.WriteLine($"--- OCR Extraction Complete ---\n");
        return string.Empty;
    }

    private async Task<string> ExtractTextWithGoogleVision(string imagePath, string apiKey)
    {
        try
        {
            Console.WriteLine("Calling Google Vision API...");
            
            // Read image file and convert to base64
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64Image = Convert.ToBase64String(imageBytes);
            
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                // Google Vision API request
                var requestBody = new
                {
                    requests = new[]
                    {
                        new
                        {
                            image = new { content = base64Image },
                            features = new[]
                            {
                                new { type = "TEXT_DETECTION", maxResults = 10 }
                            },
                            imageContext = new
                            {
                                languageHints = new[] { "he", "en" } // Hebrew first, then English for better Hebrew recognition
                            }
                        }
                    }
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync(
                    $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}",
                    content
                );
                
                Console.WriteLine($"Google Vision API Response Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Google Vision API Response (first 1000 chars):");
                    Console.WriteLine(jsonResponse.Substring(0, Math.Min(1000, jsonResponse.Length)));
                    
                    var visionResult = JsonSerializer.Deserialize<GoogleVisionResponse>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (visionResult?.Responses != null && visionResult.Responses.Length > 0)
                    {
                        var allText = new StringBuilder();
                        foreach (var resp in visionResult.Responses)
                        {
                            if (resp.TextAnnotations != null && resp.TextAnnotations.Length > 0)
                            {
                                // First annotation is usually the full text
                                var fullText = resp.TextAnnotations[0].Description;
                                if (!string.IsNullOrWhiteSpace(fullText))
                                {
                                    allText.AppendLine(fullText);
                                }
                            }
                        }
                        
                        var extractedText = allText.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(extractedText))
                        {
                            Console.WriteLine($"✓ Google Vision extracted {extractedText.Length} characters");
                            Console.WriteLine($"Extracted text preview (first 500 chars):");
                            Console.WriteLine(extractedText.Substring(0, Math.Min(500, extractedText.Length)));
                            return extractedText;
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Google Vision API Error: {response.StatusCode}");
                    
                    // Check if it's a billing/permission error
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine("⚠ Google Vision requires billing to be enabled.");
                        Console.WriteLine("   To enable: https://console.cloud.google.com/billing/enable");
                        Console.WriteLine("   Or use OCR.space instead (set GOOGLE_VISION_USE=false in .env)");
                    }
                    else
                    {
                        Console.WriteLine($"Error Response: {errorContent}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Google Vision Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        
        return string.Empty;
    }

    private InvoiceAnalysisResult ParseInvoiceText(string text)
    {
        Console.WriteLine($"\n--- Starting Text Parsing ---");
        Console.WriteLine($"Input text length: {text.Length} characters");
        
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine($"⚠ WARNING: Input text is empty - will return default values");
        }
        else
        {
            var previewLength = Math.Min(1000, text.Length);
            Console.WriteLine($"Input text preview (first {previewLength} chars):");
            Console.WriteLine($"--- START TEXT ---");
            Console.WriteLine(text.Substring(0, previewLength));
            Console.WriteLine($"--- END TEXT ---");
            if (text.Length > previewLength)
            {
                Console.WriteLine($"... (truncated, showing first {previewLength} of {text.Length} chars)");
            }
        }
        
        var result = new InvoiceAnalysisResult
        {
            TransactionDate = DateTime.UtcNow, // PostgreSQL requires UTC
            BusinessName = "Unknown Business",
            AmountBeforeVat = 0,
            AmountAfterVat = 0,
            IsReceipt = false
        };

        // ============================================
        // [1/8] Extract Amount After VAT (סכום אחרי מע״מ)
        // ============================================
        Console.WriteLine($"\n[1/8] Searching for amount AFTER VAT (סכום אחרי מע״מ)...");
        var amountAfterVatPatterns = new[]
        {
            // Specific patterns for "Total to pay"
            @"סה""כ[:\s]*לתשלום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",  // סה"כ לתשלום: ₪ 1,638.00
            @"לתשלום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",              // לתשלום: ₪ 1,638.00
            @"סכום[:\s]*לתשלום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",    // סכום לתשלום: ₪ 1,638.00
            @"סכום[:\s]*אחרי[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)", // סכום אחרי מע"מ: ₪ 1,638.00
            
            // Patterns with ₪ symbol and generic "Total"
            @"סה""כ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",              // סה"כ: ₪ 1,638.00
            @"סכום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",                // סכום: ₪ 1,638.00
            @"סכום[:\s]*סה""כ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",     // סכום סה"כ: ₪ 1,638.00
            @"סכום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)\s*(?=מע""מ|כולל)", // סכום ₪ 1,638.00 מע"מ
            
            // Standard patterns
            @"כולל[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",      // כולל מע"מ: ₪ 1,638.00
            @"סה""כ[:\s]*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)\s*שקל",              // סה"כ 1,638.00 שקל
            @"(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)\s*שקל[:\s]*חדש",                // 1,638.00 שקל חדש
            
            // Patterns without thousands separator
            @"סה""כ[:\s]*לתשלום[:\s]*₪?\s*(\d+[,.]?\d*)",                            // סה"כ לתשלום: ₪ 1638.00
            @"לתשלום[:\s]*₪?\s*(\d+[,.]?\d*)",                                        // לתשלום: ₪ 1638.00
            @"סה""כ[:\s]*(\d+[,.]?\d*)",                                              // סה"כ 1638.00 (simple)
        };

        var vatPatterns = new[]
        {
            @"מע""מ[:\s]*17%?[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",     // מע"מ 17%: ₪ 238.00
            @"מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",              // מע"מ: ₪ 238.00
            @"סכום[:\s]*המע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",    // סכום המע"מ: ₪ 238.00
        };
        
        decimal? amountAfterVat = null;
        decimal? amountVat = null;

        // Extract VAT amount specifically
        foreach (var pattern in vatPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success && match.Groups.Count > 1)
            {
                var amountStr = match.Groups[1].Value.Trim().Replace(",", "");
                if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                {
                    amountVat = amount;
                    Console.WriteLine($"  ✓ VAT amount found: {amount}");
                    break;
                }
            }
        }

        foreach (var pattern in amountAfterVatPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var amountStr = match.Groups[1].Value.Trim();
                    // Handle formats like "1,638.00" or "1638.00" or "1638,00" or "1,638"
                    // Remove thousands separators (commas) but keep decimal point/comma
                    if (amountStr.Contains(",") && amountStr.Contains("."))
                    {
                        // If both comma and dot exist, comma is likely thousands separator
                        amountStr = amountStr.Replace(",", "");
                    }
                    else if (amountStr.Contains(",") && amountStr.Split(',')[1].Length <= 2)
                    {
                        // Comma is decimal separator, replace with dot
                        amountStr = amountStr.Replace(",", ".");
                        // Remove any remaining commas (thousands separators)
                        amountStr = amountStr.Replace(",", "");
                    }
                    else
                    {
                        // Remove commas as thousands separators
                        amountStr = amountStr.Replace(",", "");
                    }
                    
                    if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount > 0)
                    {
                        // If we have a VAT amount, the total must be greater than VAT
                        if (amountVat.HasValue && amount <= amountVat.Value) continue;

                        if (!amountAfterVat.HasValue || amount > amountAfterVat.Value)
                        {
                            amountAfterVat = amount;
                            Console.WriteLine($"  ✓ Amount AFTER VAT found: {amount} (from: '{match.Groups[0].Value}')");
                        }
                    }
                }
            }
        }

        // ============================================
        // [2/8] Extract Amount Before VAT (סכום לפני מע״מ)
        // ============================================
        Console.WriteLine($"\n[2/8] Searching for amount BEFORE VAT (סכום לפני מע״מ)...");
        var amountBeforeVatPatterns = new[]
        {
            // Patterns with ₪ symbol
            @"סכום[:\s]*לפני[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)", // סכום לפני מע"מ: ₪ 1,400.00
            @"סכום[:\s]*לפני[:\s]*מע""מ[:\s]*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",       // סכום לפני מע"מ: 1,400.00
            @"סה""כ[:\s]*ללא[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",  // סה"כ ללא מע"מ: ₪ 1,400.00
            @"ללא[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",              // ללא מע"מ: ₪ 1,400.00
            @"סה""כ[:\s]*לפני[:\s]*מע""מ[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)", // סה"כ לפני מע"מ: ₪ 1,400.00
            @"סה""כ[:\s]*ללא[:\s]*מע""מ[:\s]*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",        // סה"כ ללא מע"מ: 1,400.00
            @"ללא[:\s]*מע""מ[:\s]*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)",                  // ללא מע"מ: 1,400.00
            @"סכום[:\s]*₪?\s*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)\s*(?=לפני[:\s]*מע""מ|ללא[:\s]*מע""מ)", // סכום ₪ 1,400.00 לפני מע"מ
            @"סה""כ[:\s]*(\d{1,3}(?:[,.]?\d{3})*(?:[,.]\d{2})?)\s*(?=מע""מ|ללא)",           // סה"כ 1,400.00 לפני מע"מ
            // Patterns without thousands separator
            @"סכום[:\s]*לפני[:\s]*מע""מ[:\s]*₪?\s*(\d+[,.]?\d*)",                            // סכום לפני מע"מ: ₪ 1400.00
            @"סה""כ[:\s]*ללא[:\s]*מע""מ[:\s]*₪?\s*(\d+[,.]?\d*)",                            // סה"כ ללא מע"מ: ₪ 1400.00
        };
        
        decimal? amountBeforeVat = null;
        foreach (var pattern in amountBeforeVatPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var amountStr = match.Groups[1].Value.Trim();
                    // Handle formats like "1,400.00" or "1400.00" or "1400,00" or "1,400"
                    // Remove thousands separators (commas) but keep decimal point/comma
                    if (amountStr.Contains(",") && amountStr.Contains("."))
                    {
                        // If both comma and dot exist, comma is likely thousands separator
                        amountStr = amountStr.Replace(",", "");
                    }
                    else if (amountStr.Contains(",") && amountStr.Split(',')[1].Length <= 2)
                    {
                        // Comma is decimal separator, replace with dot
                        amountStr = amountStr.Replace(",", ".");
                        // Remove any remaining commas (thousands separators)
                        amountStr = amountStr.Replace(",", "");
                    }
                    else
                    {
                        // Remove commas as thousands separators
                        amountStr = amountStr.Replace(",", "");
                    }
                    
                    if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount > 0)
                    {
                        amountBeforeVat = amount;
                        Console.WriteLine($"  ✓ Amount BEFORE VAT found: {amount} (from: '{match.Groups[0].Value}')");
                        break;
                    }
                }
            }
        }
        
        // Set amounts - use extracted values or calculate
        if (amountAfterVat.HasValue)
        {
            result.AmountAfterVat = amountAfterVat.Value;
            
            if (amountBeforeVat.HasValue)
            {
                result.AmountBeforeVat = amountBeforeVat.Value;
            }
            else if (amountVat.HasValue)
            {
                // Calculate before VAT from after VAT and specific VAT amount
                result.AmountBeforeVat = amountAfterVat.Value - amountVat.Value;
                Console.WriteLine($"  ✓ Calculated before VAT: {result.AmountBeforeVat} ({amountAfterVat.Value} - {amountVat.Value})");
            }
            else
            {
                // Fallback to 17% calculation
                result.AmountBeforeVat = amountAfterVat.Value / 1.17m;
                Console.WriteLine($"  ✓ Estimated before VAT (17%): {result.AmountBeforeVat}");
            }
            
            Console.WriteLine($"  ✓ Final: After VAT = {result.AmountAfterVat}, Before VAT = {result.AmountBeforeVat}");
        }
        else if (amountBeforeVat.HasValue)
        {
            result.AmountBeforeVat = amountBeforeVat.Value;
            result.AmountAfterVat = amountBeforeVat.Value * 1.17m;
            Console.WriteLine($"  ✓ Final: Before VAT = {result.AmountBeforeVat}, After VAT = {result.AmountAfterVat}");
        }
        else
        {
            Console.WriteLine($"  ✗ No amount patterns matched");
        }

        // ============================================
        // [3/8] Extract Transaction Date (תאריך העסקה)
        // ============================================
        Console.WriteLine($"\n[3/8] Searching for transaction date (תאריך העסקה)...");
        var datePatterns = new[]
        {
            @"תאריך[:\s]*עסקה[:\s]*(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})", // תאריך עסקה: DD/MM/YYYY
            @"תאריך[:\s]*עסקה[:\s]*(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})[:\s]*עד[:\s]*(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})", // תאריך עסקה: DD/MM/YYYY עד DD/MM/YYYY (take first date)
            @"תאריך[:\s]*(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})", // תאריך: DD/MM/YYYY
            @"(\d{1,2})[/-](\d{1,2})[/-](\d{4})",           // DD/MM/YYYY or DD-MM-YYYY
            @"(\d{1,2})[/-](\d{1,2})[/-](\d{2})",          // DD/MM/YY or DD-MM-YY
        };
        
        DateTime? foundDate = null;
        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count >= 4)
            {
                var day = match.Groups[1].Value;
                var month = match.Groups[2].Value;
                var year = match.Groups[3].Value;
                
                // Handle 2-digit year
                if (year.Length == 2)
                {
                    var yearInt = int.Parse(year);
                    year = yearInt > 50 ? $"19{year}" : $"20{year}";
                }
                
                // Try DD/MM/YYYY format (common in Israel)
                if (DateTime.TryParse($"{day}/{month}/{year}", out var date))
                {
                    foundDate = date;
                    Console.WriteLine($"  ✓ Date pattern matched: '{pattern}' -> {day}/{month}/{year}");
                    break;
                }
            }
        }
        
        if (foundDate.HasValue)
        {
            // Ensure date is UTC (PostgreSQL requirement)
            result.TransactionDate = foundDate.Value.Kind == DateTimeKind.Utc 
                ? foundDate.Value 
                : DateTime.SpecifyKind(foundDate.Value, DateTimeKind.Utc);
            Console.WriteLine($"  ✓ Parsed date: {result.TransactionDate:yyyy-MM-dd}");
        }
        else
        {
            Console.WriteLine($"  ✗ No date pattern matched");
        }

        // ============================================
        // [4/8] Extract Business Name (שם העסק) - NOT recipient name (לכבוד)
        // ============================================
        Console.WriteLine($"\n[4/8] Searching for business name (שם העסק)...");
        
        // 1. Detect recipient name to ignore it
        var recipientMatch = Regex.Match(text, @"לכבוד[:\s]*([^\n\r]{2,100})", RegexOptions.IgnoreCase);
        string? recipientName = recipientMatch.Success ? recipientMatch.Groups[1].Value.Trim() : null;
        if (recipientName != null)
        {
            Console.WriteLine($"  ℹ Recipient name detected: '{recipientName}' (will be ignored)");
        }

        string? foundBusinessName = null;
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var topLines = lines.Take(12).ToList();

        // 2. HELPER: Exclusion list for names
        bool IsExcluded(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 3) return true;
            
            var lower = name.ToLower();
            var excludedWords = new[] 
            { 
                "חשבון", "חשבונית", "קבלה", "לכבוד", "עבור", "מספר", 
                "עוסק", "מורשה", "ורשה", "ח.פ", "ח\"פ", "חפ", 
                "טלפון", "פקס", "כתובת", "רחוב", "ת.ד", "תאריך", 
                "שעה", "דף", "העתק", "מקור", "תודה רבה" 
            };
            
            if (excludedWords.Any(word => lower.Contains(word))) return true;
            if (Regex.IsMatch(name, @"^\d+$")) return true; // Only numbers
            if (Regex.IsMatch(name, @"\d{8,9}")) return true; // Contains Tax ID
            if (recipientName != null && name.Contains(recipientName)) return true;
            
            return false;
        }

        // 3. PRIORITY: Header Analysis (Look at top lines first)
        foreach (var line in topLines)
        {
            var trimmed = line.Trim();
            if (!IsExcluded(trimmed) && Regex.IsMatch(trimmed, @"[\u0590-\u05FF]{3,}"))
            {
                foundBusinessName = trimmed;
                Console.WriteLine($"  ✓ Business name found from top-header: '{foundBusinessName}'");
                break;
            }
        }

        // 4. SECONDARY: Explicit Patterns (if header failed)
        if (string.IsNullOrWhiteSpace(foundBusinessName))
        {
            var businessPatterns = new[]
            {
                @"שם[:\s]+העסק[:\s]*[:]?\s*([^\n\r]{2,100})",
                @"שם[:\s]*החברה[:\s]*[:]?\s*([^\n\r]{2,100})",
                @"עסק[:\s]*[:]?\s*([^\n\r]{2,100})",
                @"חברה[:\s]*[:]?\s*([^\n\r]{2,100})",
                @"([^\n\r]{2,50})\s*חשבון[:\s]*עסקה",
                @"([^\n\r]{2,50})\s*חשבונית",
                @"שם[:\s]*המוכר[:\s]*[:]?\s*([^\n\r]{2,100})"
            };

            foreach (var pattern in businessPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success && match.Groups.Count > 1)
                {
                    var candidate = match.Groups[1].Value.Trim();
                    if (!IsExcluded(candidate))
                    {
                        foundBusinessName = candidate;
                        Console.WriteLine($"  ✓ Business name found from pattern: '{foundBusinessName}'");
                        break;
                    }
                }
            }
        }
        
        // 5. Final cleanup
        if (!string.IsNullOrWhiteSpace(foundBusinessName))
        {
            var nameParts = foundBusinessName.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foundBusinessName = nameParts[0].Trim();
            
            foundBusinessName = Regex.Replace(foundBusinessName, @"[:\s\-\|]+$", "");
            foundBusinessName = Regex.Replace(foundBusinessName, @"^[:\s\-\|]+", "");
            foundBusinessName = Regex.Replace(foundBusinessName, @"\d{8,9}", "").Trim();
            
            var stopStrings = new[] { "חשבון", "חשבונית", "מספר", "בע\"מ", "בעמ" };
            foreach (var stop in stopStrings)
            {
                var index = foundBusinessName.IndexOf(stop);
                if (index > 0)
                {
                    if (stop == "בע\"מ" || stop == "בעמ")
                        foundBusinessName = foundBusinessName.Substring(0, index + stop.Length).Trim();
                    else
                        foundBusinessName = foundBusinessName.Substring(0, index).Trim();
                }
            }

            result.BusinessName = foundBusinessName;
            Console.WriteLine($"  ✓ Final Business Name: '{result.BusinessName}'");
        }
        else
        {
            Console.WriteLine($"  ✗ No business name detected");
        }

        // ============================================
        // [5/8] Extract Tax ID / Authorized Dealer Number (ח״פ / מספר עוסק מורשה)
        // ============================================
        Console.WriteLine($"\n[5/8] Searching for tax ID / authorized dealer number (ח״פ / מספר עוסק מורשה)...");
        var taxIdPatterns = new[]
        {
            @"מספר[:\s]*עוסק[:\s]*מורשה[:\s]*(\d{8,9})",    // מספר עוסק מורשה: 305288508
            @"עוסק[:\s]*מורשה[:\s]*(\d{8,9})",              // עוסק מורשה: 305288508
            @"ורשה[:\s]*(\d{8,9})",                          // ורשה (OCR misread of עוסק מורשה): 305288508
            @"ח""פ[:\s]*(\d{8,9})",                          // ח"פ: 305288508
            @"ח""פ[:\s]*[:]?\s*(\d{8,9})",                   // ח"פ: 305288508
            @"מס[:\s]*עוסק[:\s]*מורשה[:\s]*(\d{8,9})",      // מס עוסק מורשה: 305288508
            @"מס[:\s]*עוסק[:\s]*(\d{8,9})",                  // מס עוסק: 305288508
            @"(\d{8,9})\s*ורשה",                             // 305288508 ורשה (OCR misread)
            @"(\d{8,9})\s*עוסק",                             // 305288508 עוסק
            @"(\d{8,9})\s*ח""פ",                             // 305288508 ח"פ
        };
        
        string? foundTaxId = null;
        foreach (var pattern in taxIdPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                foundTaxId = match.Groups[1].Value.Trim();
                Console.WriteLine($"  ✓ Tax ID found: '{foundTaxId}'");
                break;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(foundTaxId))
        {
            result.TaxId = foundTaxId;
        }
        else
        {
            Console.WriteLine($"  ✗ No tax ID pattern matched");
        }

        // ============================================
        // [6/8] Extract Invoice Number (מספר חשבונית)
        // ============================================
        Console.WriteLine($"\n[6/8] Searching for invoice number (מספר חשבונית)...");
        var invoicePatterns = new[]
        {
            @"מספר[:\s]*חשבונית[:\s]*מס[:\s]*קבלה[:\s]*(\d+[/-]\d+)",  // מספר חשבונית מס קבלה: 02/000001
            @"חשבונית[:\s]*מס[:\s]*קבלה[:\s]*מספר[:\s]*(\d+[/-]\d+)",  // חשבונית מס קבלה מספר: 02/000001
            @"מספר[:\s]*חשבונית[:\s]*(\d+[/-]\d+)",                    // מספר חשבונית: 02/000001
            @"חשבונית[:\s]*מספר[:\s]*(\d+[/-]\d+)",                    // חשבונית מספר: 02/000001
            @"חשבונית[:\s]*מס[:\s]*מספר[:\s]*(\d+[/-]\d+)",            // חשבונית מס מספר: 02/000001
            @"חשבונית[:\s]*#?(\d+[/-]\d+)",                            // חשבונית #02/000001
            @"מספר[:\s]*(\d+[/-]\d+)",                                  // מספר: 02/000001
            @"חשבון[:\s]*עסקה[:\s]*(\d+)",                            // חשבון עסקה 40005
        };
        
        string? foundInvoiceNumber = null;
        foreach (var pattern in invoicePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                foundInvoiceNumber = match.Groups[1].Value.Trim();
                Console.WriteLine($"  ✓ Invoice number found: '{foundInvoiceNumber}'");
                break;
            }
        }
        
        // ============================================
        // [7/8] Determine Document Type (קבלה, חשבונית מס, חשבונית מס קבלה)
        // ============================================
        Console.WriteLine($"\n[7/8] Determining document type (קבלה, חשבונית מס, חשבונית מס קבלה)...");
        
        // Priority 1: Check for "חשבונית מס קבלה" (Tax Invoice Receipt) - all three words together
        // This is the most specific pattern and should be checked first
        bool isTaxInvoiceReceipt = Regex.IsMatch(text, @"חשבונית[:\s]*מס[:\s]*קבלה", RegexOptions.IgnoreCase);
        
        // Priority 2: Check for "חשבונית מס" (Tax Invoice) - two words together
        bool isTaxInvoiceWithMas = Regex.IsMatch(text, @"חשבונית[:\s]*מס(?!\s*קבלה)", RegexOptions.IgnoreCase);
        
        // Priority 3: Check for "חשבונית עסקה" (Business Invoice)
        bool isTaxInvoiceWithAsaka = Regex.IsMatch(text, @"חשבונית[:\s]*עסקה", RegexOptions.IgnoreCase);
        
        // Priority 4: Check for standalone "חשבונית" (Invoice) - just the word חשבונית
        bool isInvoice = Regex.IsMatch(text, @"(?:^|\s)חשבונית(?:\s|$|[:\s]*מספר|[:\s]*\d+)", RegexOptions.IgnoreCase);
        
        // Priority 5: Check for "קבלה" (Receipt) - standalone, not part of invoice
        bool isReceipt = Regex.IsMatch(text, @"(?:^|\s)קבלה(?:\s|$|[:\s]*מספר)", RegexOptions.IgnoreCase);
        
        string documentType = "Receipt"; // Default
        
        if (isTaxInvoiceReceipt)
        {
            // All three words together: חשבונית מס קבלה
            documentType = "TaxInvoiceReceipt";
            result.IsReceipt = false;
            Console.WriteLine($"  ✓ Document type: חשבונית מס קבלה (Tax Invoice Receipt)");
        }
        else if (isTaxInvoiceWithMas)
        {
            // חשבונית מס (two words together)
            documentType = "TaxInvoice";
            result.IsReceipt = false;
            Console.WriteLine($"  ✓ Document type: חשבונית מס (Tax Invoice) - detected 'חשבונית מס'");
        }
        else if (isTaxInvoiceWithAsaka)
        {
            // חשבונית עסקה
            documentType = "TaxInvoice";
            result.IsReceipt = false;
            Console.WriteLine($"  ✓ Document type: חשבונית מס (Tax Invoice) - detected 'חשבונית עסקה'");
        }
        else if (isInvoice)
        {
            // Just חשבונית (standalone)
            documentType = "TaxInvoice";
            result.IsReceipt = false;
            Console.WriteLine($"  ✓ Document type: חשבונית מס (Tax Invoice) - detected 'חשבונית'");
        }
        else if (isReceipt)
        {
            // קבלה (Receipt)
            documentType = "Receipt";
            result.IsReceipt = true;
            Console.WriteLine($"  ✓ Document type: קבלה (Receipt)");
        }
        else
        {
            // Default based on invoice number presence
            if (foundInvoiceNumber != null)
            {
                documentType = "TaxInvoice";
                result.IsReceipt = false;
                Console.WriteLine($"  → Defaulting to: חשבונית מס (Tax Invoice) - has invoice number");
            }
            else
            {
                documentType = "Receipt";
                result.IsReceipt = true;
                Console.WriteLine($"  → Defaulting to: קבלה (Receipt) - no invoice number");
            }
        }
        
        result.DocumentType = documentType;
        
        if (foundInvoiceNumber != null)
        {
            result.InvoiceNumber = foundInvoiceNumber;
        }

        // ============================================
        // [8/8] Extract Service Provided (השירות שסופק)
        // ============================================
        Console.WriteLine($"\n[8/8] Searching for service provided (השירות שסופק)...");
        
        var services = new List<string>();
        
        // First, try to find explicit "השירות שסופק" label
        var serviceLabelPatterns = new[]
        {
            @"השירות[:\s]*שסופק[:\s]*[:]?\s*([^\n\r]{5,100})",  // השירות שסופק: חיוב לדוגמא
            @"שירות[:\s]*שסופק[:\s]*[:]?\s*([^\n\r]{5,100})",    // שירות שסופק: חיוב לדוגמא
            @"תיאור[:\s]*השירות[:\s]*[:]?\s*([^\n\r]{5,100})",   // תיאור השירות: חיוב לדוגמא
        };
        
        string? foundServiceFromLabel = null;
        foreach (var pattern in serviceLabelPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                foundServiceFromLabel = match.Groups[1].Value.Trim();
                // Clean up - take first line and remove trailing punctuation
                var serviceLines = foundServiceFromLabel.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (serviceLines.Length > 0)
                {
                    foundServiceFromLabel = serviceLines[0].Trim();
                    foundServiceFromLabel = Regex.Replace(foundServiceFromLabel, @"[:\s]*$", "");
                    if (foundServiceFromLabel.Length >= 5 && foundServiceFromLabel.Length <= 100)
                    {
                        Console.WriteLine($"  ✓ Service found from label: '{foundServiceFromLabel}'");
                        services.Add(foundServiceFromLabel);
                        break;
                    }
                }
            }
        }
        
        // If not found from label, extract item descriptions from the invoice text
        if (services.Count == 0)
        {
            var serviceSearchLines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in serviceSearchLines)
            {
                var trimmedLine = line.Trim();
                
                // Skip lines that are clearly not product descriptions
                if (trimmedLine.Length < 10 || 
                    trimmedLine.Length > 100 ||
                    trimmedLine.Contains("סה\"כ") ||
                    trimmedLine.Contains("מע\"מ") ||
                    trimmedLine.Contains("תאריך") ||
                    trimmedLine.Contains("מספר") ||
                    trimmedLine.Contains("לכבוד") ||
                    trimmedLine.Contains("טלפון") ||
                    trimmedLine.Contains("פקס") ||
                    trimmedLine.Contains("השירות שסופק") ||
                    Regex.IsMatch(trimmedLine, @"^\d+[/-]\d+") || // Invoice numbers
                    Regex.IsMatch(trimmedLine, @"^\d+[,.]?\d*$") || // Just numbers
                    Regex.IsMatch(trimmedLine, @"^[^\u0590-\u05FF]+$")) // No Hebrew
                {
                    continue;
                }
                
                // Check if line contains Hebrew and looks like a product description
                if (Regex.IsMatch(trimmedLine, @"[\u0590-\u05FF]{5,}")) // At least 5 Hebrew characters
                {
                    // Extract just the Hebrew text part (remove prices/numbers at the end)
                    var hebrewPart = Regex.Match(trimmedLine, @"([\u0590-\u05FF\s]+)");
                    if (hebrewPart.Success)
                    {
                        var serviceName = hebrewPart.Groups[1].Value.Trim();
                        // Filter out common non-product words
                        if (serviceName.Length >= 5 && 
                            !serviceName.Contains("פריט") &&
                            !serviceName.Contains("תאור") &&
                            !serviceName.Contains("כמות") &&
                            !serviceName.Contains("יחידה") &&
                            !services.Contains(serviceName))
                        {
                            services.Add(serviceName);
                            Console.WriteLine($"  ✓ Found service item: '{serviceName}'");
                        }
                    }
                }
            }
        }
        
        if (services.Count > 0)
        {
            result.ServiceProvided = string.Join("; ", services.Take(4)); // Take first 4 items
            Console.WriteLine($"  ✓ Service provided: '{result.ServiceProvided}'");
        }
        else
        {
            Console.WriteLine($"  ✗ No service items found");
        }

        Console.WriteLine($"\n--- Parsing Complete ---");
        Console.WriteLine($"Final result (כל השדות):");
        var documentTypeDisplay = result.DocumentType switch
        {
            "TaxInvoiceReceipt" => "חשבונית מס קבלה (Tax Invoice Receipt)",
            "TaxInvoice" => "חשבונית מס (Tax Invoice)",
            _ => "קבלה (Receipt)"
        };
        Console.WriteLine($"  1. Document Type: {documentTypeDisplay}");
        Console.WriteLine($"  2. Amount Before VAT (סכום לפני מע״מ): {result.AmountBeforeVat}");
        Console.WriteLine($"  3. Amount After VAT (סכום אחרי מע״מ): {result.AmountAfterVat}");
        Console.WriteLine($"  4. Transaction Date (תאריך העסקה): {result.TransactionDate:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"  5. Business Name (שם העסק): {result.BusinessName}");
        Console.WriteLine($"  6. Tax ID (ח״פ / מספר עוסק מורשה): {result.TaxId ?? "(none)"}");
        Console.WriteLine($"  7. Service Provided (השירות שסופק): {result.ServiceProvided ?? "(none)"}");
        Console.WriteLine($"  8. Invoice Number (מספר חשבונית): {result.InvoiceNumber ?? "(none)"}");
        Console.WriteLine($"--- Text Parsing Complete ---\n");
        
        return result;
    }
}

// OCR.space API Response Models
public class OcrSpaceResponse
{
    [JsonPropertyName("ParsedResults")]
    public OcrSpaceParsedResult[]? ParsedResults { get; set; }
    
    [JsonPropertyName("OCRExitCode")]
    public int? OCRExitCode { get; set; }
    
    [JsonPropertyName("IsErroredOnProcessing")]
    public bool IsErroredOnProcessing { get; set; }
    
    [JsonPropertyName("ErrorMessage")]
    [JsonConverter(typeof(ErrorMessageConverter))]
    public string? ErrorMessage { get; set; }
    
    [JsonPropertyName("ErrorDetails")]
    public string? ErrorDetails { get; set; }
}

public class OcrSpaceParsedResult
{
    [JsonPropertyName("ParsedText")]
    public string? ParsedText { get; set; }
    
    [JsonPropertyName("TextOverlay")]
    public object? TextOverlay { get; set; }
    
    [JsonPropertyName("TextOrientation")]
    public string? TextOrientation { get; set; }
    
    [JsonPropertyName("FileParseExitCode")]
    public int? FileParseExitCode { get; set; }
}

// Custom converter to handle ErrorMessage as either string or array
public class ErrorMessageConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            var messages = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    messages.Add(reader.GetString() ?? string.Empty);
                }
            }
            return string.Join("; ", messages);
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value != null)
        {
            writer.WriteStringValue(value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

// Google Vision API Response Models
public class GoogleVisionResponse
{
    [JsonPropertyName("responses")]
    public GoogleVisionResponseItem[]? Responses { get; set; }
}

public class GoogleVisionResponseItem
{
    [JsonPropertyName("textAnnotations")]
    public GoogleVisionTextAnnotation[]? TextAnnotations { get; set; }
}

public class GoogleVisionTextAnnotation
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("boundingPoly")]
    public object? BoundingPoly { get; set; }
}

