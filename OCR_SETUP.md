# OCR Setup - Hebrew Support

## Current Configuration

### OCR Service: OCR.space API
- **Service**: OCR.space (Free Tier)
- **Language Support**: Hebrew + English (`eng+heb`)
- **Free Tier**: 25,000 requests/month
- **Cost**: FREE
- **Location**: Backend only (secure)

## Configuration

### appsettings.json
```json
{
  "OcrSpace": {
    "ApiKey": "helloworld",
    "Language": "eng+heb",
    "ApiUrl": "https://api.ocr.space/parse/image"
  }
}
```

### Current Settings:
- **Language**: `eng+heb` - Supports both Hebrew and English text recognition
- **API Key**: `helloworld` - Free tier placeholder key
- **Backend Only**: All OCR processing happens on the server (secure)

## Hebrew Support Features

### ✅ What's Supported:
1. **Hebrew Text Recognition**: OCR can read Hebrew characters from invoices/receipts
2. **Hebrew Pattern Matching**: Regex patterns recognize common Hebrew invoice terms:
   - Amounts: `סה"כ`, `סך הכל`, `כולל מע"מ`, `₪`, `שקל`
   - Business names: `שם העסק`, `עסק`, `חברה`
   - Dates: `תאריך`, `תאריך עסקה`
   - Tax ID: `ח.פ.`, `מס עוסק`
   - Invoice numbers: `חשבונית`, `מספר חשבונית`

### 📝 Hebrew Patterns Recognized:

#### Amount Patterns:
- `סה"כ: 150.00`
- `סך הכל: 150.00`
- `כולל מע"מ: 150.00`
- `₪ 150.00`
- `150.00 שקל`
- `תשלום: 150.00`

#### Business Name Patterns:
- `שם העסק: [name]`
- `עסק: [name]`
- `חברה: [name]`

#### Date Patterns:
- `תאריך: DD/MM/YYYY`
- `תאריך עסקה: DD/MM/YYYY`
- Standard formats: `DD/MM/YYYY`, `DD-MM-YYYY`

## Getting a Free API Key (Optional but Recommended)

While `helloworld` works for testing, you can get a dedicated free API key:

1. Visit: https://ocr.space/ocrapi/freekey
2. Register (free)
3. Get your API key
4. Update `appsettings.json`:
   ```json
   "OcrSpace": {
     "ApiKey": "YOUR_FREE_API_KEY_HERE",
     "Language": "eng+heb",
     "ApiUrl": "https://api.ocr.space/parse/image"
   }
   ```

## Security

✅ **All OCR processing happens on the backend**:
- API keys are never exposed to the frontend
- Files are processed server-side only
- No sensitive data sent to client
- Complies with assignment security requirements

## Testing Hebrew OCR

1. Upload an invoice/receipt with Hebrew text
2. Check backend console logs:
   - Look for "Language: eng+heb (Hebrew + English support)"
   - Check extracted text preview (should show Hebrew characters)
   - Verify pattern matching results

## Troubleshooting

### If OCR returns empty text:
1. Check console logs for full JSON response
2. Verify API key is valid
3. Check if image quality is good
4. Ensure Hebrew text is clear and readable

### If patterns don't match:
1. Check extracted text preview in logs
2. Verify Hebrew characters are being recognized
3. Patterns may need adjustment for specific invoice formats

## For Your Assignment

You can mention:
- ✅ OCR supports Hebrew text recognition
- ✅ Free tier (25,000 requests/month)
- ✅ All processing on backend (secure)
- ✅ No system dependencies required
- ✅ Works in deployed environments (Render.com, etc.)

