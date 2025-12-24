'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { api, Expense, ExpenseCategory, ExpenseFilter, DocumentType, API_BASE_URL } from '@/lib/api';

export default function DashboardPage() {
  const [activeTab, setActiveTab] = useState<'upload' | 'expenses'>('upload');
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [highlightExpenses, setHighlightExpenses] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(false);
  
  // Load dark mode preference
  useEffect(() => {
    const saved = localStorage.getItem('darkMode') === 'true';
    setIsDarkMode(saved);
  }, []);

  // Save dark mode preference
  useEffect(() => {
    localStorage.setItem('darkMode', isDarkMode.toString());
  }, [isDarkMode]);
  
  // Auto-clear success message after 3 seconds (toast behavior)
  useEffect(() => {
    if (success) {
      const timer = setTimeout(() => {
        setSuccess('');
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [success]);
  
  // Filter states
  const [filters, setFilters] = useState<ExpenseFilter>({});
  
  const { user, logout, loading: authLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (activeTab === 'expenses') {
      loadExpenses();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, filters]);

  // Map backend string enum values to frontend numeric enum values
  const mapCategoryFromBackend = (category: string | number | ExpenseCategory | undefined | null): ExpenseCategory => {
    if (category === undefined || category === null) {
      return ExpenseCategory.Other;
    }
    
    // If it's already a number, validate it
    if (typeof category === 'number') {
      if (isNaN(category) || !Object.values(ExpenseCategory).includes(category as ExpenseCategory)) {
        return ExpenseCategory.Other;
      }
      return category as ExpenseCategory;
    }
    
    // If it's a string, map it to the numeric enum
    if (typeof category === 'string') {
      const categoryMap: Record<string, ExpenseCategory> = {
        'Vehicle': ExpenseCategory.Vehicle,
        'Food': ExpenseCategory.Food,
        'Operations': ExpenseCategory.Operations,
        'IT': ExpenseCategory.IT,
        'Training': ExpenseCategory.Training,
        'Other': ExpenseCategory.Other,
        // Also handle numeric strings
        '0': ExpenseCategory.Vehicle,
        '1': ExpenseCategory.Food,
        '2': ExpenseCategory.Operations,
        '3': ExpenseCategory.IT,
        '4': ExpenseCategory.Training,
        '5': ExpenseCategory.Other,
      };
      
      const mapped = categoryMap[category];
      if (mapped !== undefined) {
        return mapped;
      }
      
      // Try parsing as number
      const parsed = parseInt(category, 10);
      if (!isNaN(parsed) && Object.values(ExpenseCategory).includes(parsed as ExpenseCategory)) {
        return parsed as ExpenseCategory;
      }
    }
    
    return ExpenseCategory.Other;
  };

  const clearFilters = () => {
    setFilters({});
  };

  const loadExpenses = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await api.getExpenses(filters);
      // Map categories from backend (string) to frontend (numeric)
      const normalizedData = data.map(expense => ({
        ...expense,
        category: mapCategoryFromBackend(expense.category)
      }));
      setExpenses(normalizedData);
      console.log('expenses loaded:', normalizedData.map(e => ({ id: e.id, category: e.category, businessName: e.businessName })));
    } catch (err) {
      setError((err as Error).message || 'Failed to load expenses');
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    setError('');
    setSuccess('');

    try {
      await api.uploadInvoice(file);
      setSuccess('החשבונית הועלתה בהצלחה!');
      
      // Highlight the expenses tab to guide the user
      setHighlightExpenses(true);
      setTimeout(() => setHighlightExpenses(false), 1000);

      e.target.value = ''; // Clear file input
      if (activeTab === 'expenses') {
        await loadExpenses();
      }
    } catch (err) {
      setError((err as Error).message || 'Upload failed');
    } finally {
      setUploading(false);
      // Reset file input
      e.target.value = '';
    }
  };

  const handleCategoryChange = async (expenseId: number, category: ExpenseCategory) => {
    try {
      setError(''); // Clear any previous errors
      console.log('Changing category for expense', expenseId, 'to', category);
      
      // Optimistic update - update UI immediately
      setExpenses(prevExpenses => 
        prevExpenses.map(exp => 
          exp.id === expenseId ? { ...exp, category } : exp
        )
      );
      
      // Update backend
      const updated = await api.updateExpenseCategory(expenseId, category);
      console.log('Category updated successfully:', updated);
      console.log('Updated expense category (raw):', updated.category, 'type:', typeof updated.category);
      
      // Map the category from backend response (might be string) to numeric enum
      const mappedCategory = mapCategoryFromBackend(updated.category);
      console.log('Mapped category:', mappedCategory);
      
      // Update local state with the mapped category
      setExpenses(prevExpenses => 
        prevExpenses.map(exp => 
          exp.id === expenseId ? { ...exp, category: mappedCategory } : exp
        )
      );
    } catch (err) {
      setError((err as Error).message || 'Failed to update category');
      console.error('Category update error:', err);
      // Revert optimistic update on error
      await loadExpenses();
    }
  };


  const handleDelete = async (expenseId: number) => {
    if (!confirm('האם אתה בטוח שברצונך למחוק הוצאה זו?')) return;

    try {
      // Optimistic update: remove from UI immediately
      setExpenses(prevExpenses => prevExpenses.filter(exp => exp.id !== expenseId));
      
      // Delete from backend
      await api.deleteExpense(expenseId);
      
      // Optionally reload to ensure sync (though optimistic update should be enough)
      // await loadExpenses();
    } catch (err) {
      setError((err as Error).message || 'Failed to delete expense');
      // Revert optimistic update on error by reloading
      await loadExpenses();
    }
  };

  const categoryLabels: Record<ExpenseCategory, string> = {
    [ExpenseCategory.Vehicle]: 'רכב',
    [ExpenseCategory.Food]: 'מזון',
    [ExpenseCategory.Operations]: 'תפעול',
    [ExpenseCategory.IT]: 'IT',
    [ExpenseCategory.Training]: 'הדרכה/הכשרה',
    [ExpenseCategory.Other]: 'אחר',
  };

  // Wait for auth to finish loading
  if (authLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p>טוען...</p>
      </div>
    );
  }

  // Only redirect if not loading AND no user
  if (!user) {
    router.push('/login');
    return null;
  }

  return (
    <div className={`min-h-screen transition-colors duration-300 ${isDarkMode ? 'bg-[#0a0a0a]' : 'bg-gray-50'}`}>
      <div className={`${isDarkMode ? 'bg-[#1a1a1a] border-b border-gray-800 shadow-xl' : 'bg-white shadow'}`}>
        <div className=" mx-auto px-4 sm:px-6 lg:px-8">
          <div className=" max-w-7xl flex justify-between items-center py-4 mx-auto">
            <h1 className={`text-2xl font-bold ${isDarkMode ? 'text-white' : 'text-black'}`}>מערכת ניהול הוצאות</h1>
            <div className="flex items-center gap-6">
              {/* Dark Mode Toggle */}
              <button
                onClick={() => setIsDarkMode(!isDarkMode)}
                className={`p-2 rounded-full transition-colors ${isDarkMode ? 'bg-gray-800 text-yellow-400 hover:bg-gray-700' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}
                title={isDarkMode ? 'מצב יום' : 'מצב לילה'}
              >
                {isDarkMode ? (
                  <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707m12.728 0l-.707-.707M6.343 6.343l-.707-.707" />
                  </svg>
                ) : (
                  <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                  </svg>
                )}
              </button>
              <span className={`text-sm font-bold ${isDarkMode ? 'text-white' : 'text-black'}`}>{user?.email}</span>
              <button
                onClick={logout}
                className={`text-sm font-bold ${isDarkMode ? 'text-blue-400 hover:text-blue-300' : 'text-blue-600 hover:text-blue-500'}`}
              >
                התנתק
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-[1500px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className={`rounded-lg shadow-xl overflow-hidden transition-colors duration-300 ${isDarkMode ? 'bg-[#1a1a1a] border border-gray-800' : 'bg-white'}`}>
          <div className={`border-b ${isDarkMode ? 'border-gray-800' : 'border-gray-200'}`}>
            <nav className="flex">
              <button
                onClick={() => setActiveTab('upload')}
                className={`px-6 py-4 text-sm font-bold transition-all duration-100 transform hover:scale-110 active:scale-95 ${
                  activeTab === 'upload'
                    ? `border-b-2 border-blue-600 ${isDarkMode ? 'text-blue-400 bg-blue-900/20' : 'text-blue-700 bg-blue-50/50'}`
                    : `${isDarkMode ? 'text-gray-400 hover:text-white' : 'text-black hover:text-blue-600'}`
                }`}
              >
                העלאת חשבוניות
              </button>
              <button
                onClick={() => setActiveTab('expenses')}
                className={`px-6 py-4 text-sm font-bold transition-all transform ${
                  highlightExpenses 
                    ? 'text-green-600' 
                    : ''
                } ${
                  activeTab === 'expenses'
                    ? `border-b-2 border-blue-600 shadow-[0_4px_20px_-5px_rgba(37,99,235,0.4)] ${isDarkMode ? 'text-blue-400 bg-blue-900/20' : 'text-blue-700 bg-blue-50/50'}`
                    : `${isDarkMode ? 'text-gray-400 hover:text-white' : 'text-black hover:text-blue-600'}`
                }`}
              >
                <span className={`inline-block`}>
                  <span className={`inline-block ${highlightExpenses ? 'animate-pulse' : ''}`}>
                    ניהול הוצאות
                  </span>
                </span>
              </button>
            </nav>
          </div>

          <div className="p-6">
            {error && (
              <div className={`mb-4 px-4 py-3 rounded border ${isDarkMode ? 'bg-red-900/20 border-red-800 text-red-400' : 'bg-red-50 border-red-200 text-red-700'}`}>
                {error}
              </div>
            )}

            {/* Success Toast */}
            {success && (
              <div className="fixed top-4 left-1/2 -translate-x-1/2 z-50 transition-all transform duration-500 ease-in-out">
                <div className="bg-green-600 text-white px-6 py-3 rounded-lg shadow-2xl flex items-center gap-2 border-2 border-green-400 transform translate-y-0 opacity-100 transition-all animate-bounce">
                  <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                  </svg>
                  <span className="font-bold text-white">{success}</span>
                </div>
              </div>
            )}

            {activeTab === 'upload' && (
              <div className="space-y-4">
                <h2 className={`text-xl font-bold ${isDarkMode ? 'text-white' : 'text-black'}`}>העלאת חשבונית/קבלה</h2>
                <p className={`text-sm font-medium ${isDarkMode ? 'text-gray-400' : 'text-black'}`}>
                  העלה קובץ PDF או תמונה לניתוח אוטומטי. סוג המסמך (חשבונית/קבלה) יזוהה אוטומטית
                </p>
                
                {/* File Upload */}
                <div>
                  <label className={`block text-sm font-bold mb-2 ${isDarkMode ? 'text-white' : 'text-black'}`}>
                    בחר קובץ
                  </label>
                  <input
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png"
                    onChange={handleFileUpload}
                    disabled={uploading}
                    className={`block w-full text-sm file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-bold ${
                      isDarkMode 
                        ? 'text-gray-300 file:bg-gray-800 file:text-blue-400 hover:file:bg-gray-700' 
                        : 'text-black file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100'
                    }`}
                  />
                  {uploading && (
                    <p className={`mt-2 text-sm font-bold ${isDarkMode ? 'text-blue-400' : 'text-black'}`}>מעבד את הקובץ...</p>
                  )}
                </div>
              </div>
            )}

            {activeTab === 'expenses' && (
              <div className="space-y-4">
                <h2 className={`text-xl font-bold ${isDarkMode ? 'text-white' : 'text-black'}`}>ניהול הוצאות</h2>
                
                {/* Filters */}
                <div className={`grid grid-cols-1 md:grid-cols-4 gap-6 p-4 rounded-lg transition-colors duration-300 ${isDarkMode ? 'bg-gray-800/50' : 'bg-gray-50'}`}>
                  {/* Date Filters Stacked */}
                  <div className="space-y-3">
                    <div>
                      <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                        מתאריך
                      </label>
                      <input
                        type="date"
                        value={filters.startDate || ''}
                        onChange={(e) => setFilters({ ...filters, startDate: e.target.value || undefined })}
                        className={`w-full px-3 py-1.5 border rounded-md text-sm transition-colors ${
                          isDarkMode 
                            ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                            : 'bg-white border-gray-300 text-black'
                        }`}
                      />
                    </div>
                    <div>
                      <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                        עד תאריך
                      </label>
                      <input
                        type="date"
                        value={filters.endDate || ''}
                        onChange={(e) => setFilters({ ...filters, endDate: e.target.value || undefined })}
                        className={`w-full px-3 py-1.5 border rounded-md text-sm transition-colors ${
                          isDarkMode 
                            ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                            : 'bg-white border-gray-300 text-black'
                        }`}
                      />
                    </div>
                  </div>

                  {/* Amount Filters Stacked */}
                  <div className="space-y-3">
                    <div>
                      <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                        סכום מינימלי
                      </label>
                      <div className="relative">
                        <span className={`absolute inset-y-0 left-0 pl-3 flex items-center text-sm ${isDarkMode ? 'text-gray-400' : 'text-black'}`}>₪</span>
                        <input
                          type="number"
                          step="0.01"
                          value={filters.minAmount || ''}
                          onChange={(e) => setFilters({ ...filters, minAmount: e.target.value ? parseFloat(e.target.value) : undefined })}
                          className={`w-full pl-7 pr-3 py-1.5 border rounded-md text-sm transition-colors ${
                            isDarkMode 
                              ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                              : 'bg-white border-gray-300 text-black'
                          }`}
                          placeholder="0.00"
                        />
                      </div>
                    </div>
                    <div>
                      <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                        סכום מקסימלי
                      </label>
                      <div className="relative">
                        <span className={`absolute inset-y-0 left-0 pl-3 flex items-center text-sm ${isDarkMode ? 'text-gray-400' : 'text-black'}`}>₪</span>
                        <input
                          type="number"
                          step="0.01"
                          value={filters.maxAmount || ''}
                          onChange={(e) => setFilters({ ...filters, maxAmount: e.target.value ? parseFloat(e.target.value) : undefined })}
                          className={`w-full pl-7 pr-3 py-1.5 border rounded-md text-sm transition-colors ${
                            isDarkMode 
                              ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                              : 'bg-white border-gray-300 text-black'
                          }`}
                          placeholder="0.00"
                        />
                      </div>
                    </div>
                  </div>

                  {/* Business Name */}
                  <div className="flex flex-col justify-start">
                    <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                      שם עסק
                    </label>
                    <input
                      type="text"
                      value={filters.businessName || ''}
                      onChange={(e) => setFilters({ ...filters, businessName: e.target.value || undefined })}
                      className={`w-full px-3 py-1.5 border rounded-md text-sm transition-colors h-[38px] ${
                        isDarkMode 
                          ? 'bg-[#0a0a0a] border-gray-700 text-white placeholder-gray-500' 
                          : 'bg-white border-gray-300 text-black placeholder-gray-400'
                      }`}
                      placeholder="חפש עסק..."
                    />
                  </div>

                  {/* Category */}
                  <div className="flex flex-col justify-start">
                    <label className={`block text-xs font-bold mb-1 ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                      קטגוריה
                    </label>
                    <select
                      value={filters.category !== undefined ? filters.category : ''}
                      onChange={(e) => setFilters({ ...filters, category: e.target.value ? parseInt(e.target.value) as ExpenseCategory : undefined })}
                      className={`w-full px-3 py-1.5 border rounded-md text-sm transition-colors h-[38px] ${
                        isDarkMode 
                          ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                          : 'bg-white border-gray-300 text-black'
                      }`}
                    >
                      <option value="">הכל</option>
                      {Object.entries(categoryLabels).map(([key, label]) => (
                        <option key={key} value={key}>{label}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Clear Filters Button */}
                <div className="flex justify-end">
                  <button
                    onClick={clearFilters}
                    className={`flex items-center gap-2 text-sm font-bold transition-colors ${isDarkMode ? 'text-blue-400 hover:text-blue-300' : 'text-blue-600 hover:text-blue-800'}`}
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                    נקה פילטרים
                  </button>
                </div>

                {/* Expenses Table */}
                {loading ? (
                  <p className={`text-center font-bold py-8 ${isDarkMode ? 'text-white' : 'text-black'}`}>טוען...</p>
                ) : expenses.length === 0 ? (
                  <p className={`text-center font-bold py-8 ${isDarkMode ? 'text-white' : 'text-black'}`}>אין הוצאות להצגה</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className={`min-w-full divide-y ${isDarkMode ? 'divide-gray-800' : 'divide-gray-200'}`}>
                      <thead className={`${isDarkMode ? 'bg-gray-800/50' : 'bg-gray-100'}`}>
                        <tr>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            שם עסק
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            ח״פ / עוסק מורשה
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            תאריך
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            לפני מע״מ
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            אחרי מע״מ
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            מספר חשבונית
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            סוג מסמך
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            קטגוריה
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            מסמך
                          </th>
                          <th className={`px-6 py-3 text-right text-xs font-bold uppercase tracking-wider ${isDarkMode ? 'text-gray-300' : 'text-black'}`}>
                            פעולות
                          </th>
                        </tr>
                      </thead>
                      <tbody className={`divide-y transition-colors duration-300 ${isDarkMode ? 'bg-[#1a1a1a] divide-gray-800' : 'bg-white divide-gray-200'}`}>
                        {expenses.map((expense) => {
                          // ... same logic for docType and formattedDate ...
                          let docType: DocumentType = DocumentType.Receipt;
                          if (expense.documentType !== undefined && expense.documentType !== null) {
                            if (typeof expense.documentType === 'number') {
                              docType = expense.documentType as DocumentType;
                            } else if (typeof expense.documentType === 'string') {
                              docType = DocumentType[expense.documentType as keyof typeof DocumentType] || DocumentType.Receipt;
                            } else {
                              docType = expense.documentType;
                            }
                          } else if (expense.isReceipt !== undefined) {
                            docType = expense.isReceipt ? DocumentType.Receipt : DocumentType.TaxInvoice;
                          }

                          const date = new Date(expense.transactionDate);
                          const formattedDate = `${String(date.getUTCDate()).padStart(2, '0')}/${String(date.getUTCMonth() + 1).padStart(2, '0')}/${date.getUTCFullYear()}`;

                          return (
                            <tr key={expense.id} className={`transition-colors ${isDarkMode ? 'hover:bg-gray-800' : 'hover:bg-gray-50'}`}>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                {expense.businessName}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                {expense.taxId || '-'}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                {formattedDate}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                ₪{expense.amountBeforeVat.toFixed(2)}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm font-semibold ${isDarkMode ? 'text-white' : 'text-black'}`}>
                                ₪{expense.amountAfterVat.toFixed(2)}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                {expense.invoiceNumber || '-'}
                              </td>
                              <td className={`px-6 py-4 whitespace-nowrap text-sm ${isDarkMode ? 'text-gray-200' : 'text-black'}`}>
                                {(() => {
                                  if (docType === DocumentType.TaxInvoiceReceipt) return 'חשבונית מס קבלה';
                                  if (docType === DocumentType.TaxInvoice) return 'חשבונית מס';
                                  return 'קבלה';
                                })()}
                              </td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm">
                              <select
                                key={`category-${expense.id}-${expense.category}`}
                                value={(() => {
                                  let categoryValue: number = ExpenseCategory.Other;
                                  if (expense.category !== undefined && expense.category !== null) {
                                    if (typeof expense.category === 'number' && !isNaN(expense.category)) {
                                      categoryValue = expense.category;
                                    } else if (typeof expense.category === 'string') {
                                      const parsed = parseInt(expense.category as string, 10);
                                      categoryValue = isNaN(parsed) ? ExpenseCategory.Other : parsed;
                                    }
                                  }
                                  if (!Object.values(ExpenseCategory).includes(categoryValue as ExpenseCategory)) {
                                    categoryValue = ExpenseCategory.Other;
                                  }
                                  return categoryValue;
                                })()}
                                onChange={async (e) => {
                                  e.stopPropagation();
                                  const newCategory = parseInt(e.target.value, 10) as ExpenseCategory;
                                  const currentCategory = (() => {
                                    const cat = expense.category as unknown;
                                    if (cat === undefined || cat === null) return ExpenseCategory.Other;
                                    if (typeof cat === 'string') return (parseInt(cat, 10) as ExpenseCategory) || ExpenseCategory.Other;
                                    if (typeof cat === 'number' && isNaN(cat)) return ExpenseCategory.Other;
                                    return Number(cat) as ExpenseCategory;
                                  })();
                                  
                                  if (!isNaN(newCategory) && newCategory !== currentCategory) {
                                    await handleCategoryChange(expense.id, newCategory);
                                  }
                                }}
                                className={`border rounded-md px-2 py-1 text-sm cursor-pointer transition-colors ${
                                  isDarkMode 
                                    ? 'bg-[#0a0a0a] border-gray-700 text-white' 
                                    : 'bg-white border-gray-300 text-black'
                                }`}
                              >
                                {[
                                  ExpenseCategory.Vehicle,
                                  ExpenseCategory.Food,
                                  ExpenseCategory.Operations,
                                  ExpenseCategory.IT,
                                  ExpenseCategory.Training,
                                  ExpenseCategory.Other
                                ].map((categoryValue) => (
                                  <option key={categoryValue} value={Number(categoryValue)}>
                                    {categoryLabels[categoryValue]}
                                  </option>
                                ))}
                              </select>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm">
                              {expense.fileName ? (
                                <a 
                                  href={`${API_BASE_URL}/uploads/${expense.fileName}`} 
                                  target="_blank" 
                                  rel="noopener noreferrer"
                                  className={`font-medium transition-colors ${isDarkMode ? 'text-blue-400 hover:text-blue-300' : 'text-blue-600 hover:text-blue-900'}`}
                                >
                                  צפה
                                </a>
                              ) : (
                                <span className="text-gray-400">-</span>
                              )}
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm">
                              <button
                                onClick={() => handleDelete(expense.id)}
                                className={`transition-colors ${isDarkMode ? 'text-red-400 hover:text-red-300' : 'text-red-600 hover:text-red-900'}`}
                              >
                                מחק
                              </button>
                            </td>
                          </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

