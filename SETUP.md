# הוראות התקנה מהירה

## שלב 1: Backend Setup

### אם יש לך .NET SDK מותקן:

```bash
cd backend
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

### אם אין לך .NET SDK:

1. הורד והתקן .NET 8.0 SDK מ: https://dotnet.microsoft.com/download
2. לאחר ההתקנה, הפעל את הפקודות למעלה

### הגדרת מסד נתונים:

1. התקן PostgreSQL (אם עדיין לא מותקן)
2. צור מסד נתונים:
   ```sql
   CREATE DATABASE invoiceexpensesystem;
   ```
3. עדכן את `appsettings.json` עם פרטי החיבור שלך

## שלב 2: Frontend Setup

```bash
cd frontend
npm install
```

צור קובץ `.env.local`:
```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

הפעל:
```bash
npm run dev
```

## בדיקה

1. פתח דפדפן ב-`http://localhost:3000`
2. תועבר אוטומטית לדף התחברות
3. הירשם עם אימייל וסיסמה
4. תועבר לדאשבורד עם שתי כרטיסיות

## הערות חשובות

- ה-backend רץ על פורט 5000 (ברירת מחדל)
- ה-frontend רץ על פורט 3000
- ודא ש-CORS מוגדר נכון ב-backend (כבר מוגדר ל-localhost:3000)
- הטוקן JWT נשמר ב-localStorage

## בעיות נפוצות

### Backend לא מתחבר למסד נתונים:
- ודא ש-PostgreSQL רץ
- בדוק את connection string ב-`appsettings.json`
- ודא שהמסד נתונים נוצר

### Frontend לא מתחבר ל-Backend:
- ודא שה-backend רץ
- בדוק את `NEXT_PUBLIC_API_URL` ב-`.env.local`
- ודא שאין שגיאות CORS (פתח את Console בדפדפן)

### OCR לא עובד:
- זה נורמלי בשלב זה - התשתית קיימת אבל דורשת התקנת Tesseract או מפתח Google Vision API
- המערכת תמשיך לעבוד אבל הניתוח יהיה בסיסי

