# Invoice and Expense Management System
# מערכת לניהול חשבוניות והוצאות

## Home Assignment - Full Stack Position
## משימת בית - מפתח Full-Stack

---

### Hebrew (Hebrew Version)
#### מטרת המשימה
בניית מערכת ווב מלאה (Full Stack) הכוללת מנגנון הרשמה והתחברות, ניתוח חשבוניות/קבלות באופן אוטומטי, וטבלת ניהול הוצאות עם אפשרות סיווג וסינון.
המערכת צריכה להיות מבוססת על תקשורת מאובטחת, עם צד שרת שמבצע את כל הפעולות הרגישות.

#### מה המערכת יודעת לעשות:
*   **הרשמה והתחברות מאובטחת**: שימוש ב-JWT וב-Cookies (HttpOnly) להגנה מקסימלית.
*   **ניתוח מסמכים אוטומטי (OCR)**: העלאת קבלה או חשבונית וזיהוי אוטומטי של שם העסק, ח"פ, תאריך, סכומים וסוג המסמך (חשבונית/קבלה).
*   **ניהול הוצאות**: הצגת כל הנתונים בטבלה נוחה.
*   **פעולות CRUD**: אפשרות למחיקת הוצאות ועדכון קטגוריות ישירות מהטבלה.
*   **סינונים מתקדמים**: סינון לפי טווח תאריכים, טווח סכומים, שם עסק או קטגוריה.
*   **צפייה במסמך המקורי**: ניתן לפתוח את הקובץ שהועלה ישירות מהטבלה.

---

### English Version
#### Project Goal
A full-stack application built as a home assignment. It includes a secure authentication system, automatic invoice/receipt analysis using OCR, and a management dashboard with advanced filtering. 
The system is built with security in mind, ensuring all sensitive operations (OCR analysis, database queries, and data persistence) happen strictly on the server side.

#### Core Features:
*   **Secure Authentication**: User registration and login using JWT stored in secure HttpOnly cookies.
*   **AI-Powered OCR**: Automatic extraction of business name, Tax ID (ח"פ), date, amounts, and document type from uploaded images/PDFs.
*   **Expense Dashboard**: A clean table to view and manage all expenses.
*   **CRUD Operations**: Full support for deleting expenses and updating categories on the fly.
*   **Smart Filtering**: Filter through expenses by date range, amount range, business name, or category.
*   **Document Viewer**: Quick access to the original uploaded document directly from the list.

---

### Tech Stack / טכנולוגיות
*   **Frontend**: Next.js (TypeScript) + Tailwind CSS.
*   **Backend**: ASP.NET Core 8 (C#).
*   **Database**: PostgreSQL.
*   **OCR Engine**: Google Vision API (with Hebrew support).

---

### How to Run / איך להריץ

#### Backend
1. Go to `backend/` folder.
2. Create a `.env` file based on `.env.example` and add your database connection and Google Vision API key.
3. Run `dotnet run`. The server will start on `http://localhost:5000`.

#### Frontend
1. Go to `frontend/` folder.
2. Run `npm install`.
3. Run `npm run dev`. The app will be available at `http://localhost:3000`.

---

### Security / אבטחה
*   **Server-Side Logic**: OCR analysis and data filtering are performed on the backend only.
*   **Protected API**: All endpoints are protected by JWT authentication.
*   **No Exposed Keys**: No API keys or sensitive configurations are shared with the client.
*   **Secure Cookies**: Authentication tokens are handled via HttpOnly cookies to prevent XSS.
