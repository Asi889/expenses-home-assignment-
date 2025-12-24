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
    <div className="min-h-screen bg-gray-50">
      <div className="bg-white shadow">
        <div className=" mx-auto px-4 sm:px-6 lg:px-8">
          <div className=" max-w-7xl flex justify-between items-center py-4 mx-auto">
            <h1 className="text-2xl font-bold text-black">מערכת ניהול הוצאות</h1>
            <div className="flex items-center gap-4">
              <span className="text-sm font-bold text-black">{user?.email}</span>
              <button
                onClick={logout}
                className="text-sm text-blue-600 hover:text-blue-500"
              >
                התנתק
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-[1500px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-white rounded-lg shadow">
          <div className="border-b border-gray-200">
            <nav className="flex">
              <button
                onClick={() => setActiveTab('upload')}
                className={`px-6 py-4 text-sm font-bold transition-all duration-100 transform hover:scale-110 active:scale-95 ${
                  activeTab === 'upload'
                    ? 'border-b-2 border-blue-600 text-blue-700 bg-blue-50/50 rounded-t-lg'
                    : 'text-black hover:text-blue-600 hover:rotate-1'
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
                    ? 'border-b-2 border-blue-600 text-blue-700 shadow-[0_4px_20px_-5px_rgba(37,99,235,0.4)] bg-blue-50/50 rounded-t-lg'
                    : 'text-black hover:text-blue-600 hover:-rotate-1'
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
              <div className="mb-4 bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
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
                <h2 className="text-xl font-bold text-black">העלאת חשבונית/קבלה</h2>
                <p className="text-sm font-medium text-black">
                  העלה קובץ PDF או תמונה לניתוח אוטומטי. סוג המסמך (חשבונית/קבלה) יזוהה אוטומטית
                </p>
                
                {/* File Upload */}
                <div>
                  <label className="block text-sm font-bold text-black mb-2">
                    בחר קובץ
                  </label>
                  <input
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png"
                    onChange={handleFileUpload}
                    disabled={uploading}
                    className="block w-full text-sm text-black file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-bold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                  />
                  {uploading && (
                    <p className="mt-2 text-sm font-bold text-black">מעבד את הקובץ...</p>
                  )}
                </div>
              </div>
            )}

            {activeTab === 'expenses' && (
              <div className="space-y-4">
                <h2 className="text-xl font-bold text-black">ניהול הוצאות</h2>
                
                {/* Filters */}
                <div className="grid grid-cols-1 md:grid-cols-4 gap-6 p-4 bg-gray-50 rounded-lg">
                  {/* Date Filters Stacked */}
                  <div className="space-y-3">
                    <div>
                      <label className="block text-xs font-bold text-black mb-1">
                        מתאריך
                      </label>
                      <input
                        type="date"
                        value={filters.startDate || ''}
                        onChange={(e) => setFilters({ ...filters, startDate: e.target.value || undefined })}
                        className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-sm text-black"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-black mb-1">
                        עד תאריך
                      </label>
                      <input
                        type="date"
                        value={filters.endDate || ''}
                        onChange={(e) => setFilters({ ...filters, endDate: e.target.value || undefined })}
                        className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-sm text-black"
                      />
                    </div>
                  </div>

                  {/* Amount Filters Stacked */}
                  <div className="space-y-3">
                    <div>
                      <label className="block text-xs font-bold text-black mb-1">
                        סכום מינימלי
                      </label>
                      <div className="relative">
                        <span className="absolute inset-y-0 left-0 pl-3 flex items-center text-black text-sm">₪</span>
                        <input
                          type="number"
                          step="0.01"
                          value={filters.minAmount || ''}
                          onChange={(e) => setFilters({ ...filters, minAmount: e.target.value ? parseFloat(e.target.value) : undefined })}
                          className="w-full pl-7 pr-3 py-1.5 border border-gray-300 rounded-md text-sm text-black"
                          placeholder="0.00"
                        />
                      </div>
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-black mb-1">
                        סכום מקסימלי
                      </label>
                      <div className="relative">
                        <span className="absolute inset-y-0 left-0 pl-3 flex items-center text-black text-sm">₪</span>
                        <input
                          type="number"
                          step="0.01"
                          value={filters.maxAmount || ''}
                          onChange={(e) => setFilters({ ...filters, maxAmount: e.target.value ? parseFloat(e.target.value) : undefined })}
                          className="w-full pl-7 pr-3 py-1.5 border border-gray-300 rounded-md text-sm text-black"
                          placeholder="0.00"
                        />
                      </div>
                    </div>
                  </div>

                  {/* Business Name */}
                  <div className="flex flex-col justify-start">
                    <label className="block text-xs font-bold text-black mb-1">
                      שם עסק
                    </label>
                    <input
                      type="text"
                      value={filters.businessName || ''}
                      onChange={(e) => setFilters({ ...filters, businessName: e.target.value || undefined })}
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-sm text-black h-[38px]"
                      placeholder="חפש עסק..."
                    />
                  </div>

                  {/* Category */}
                  <div className="flex flex-col justify-start">
                    <label className="block text-xs font-bold text-black mb-1">
                      קטגוריה
                    </label>
                    <select
                      value={filters.category !== undefined ? filters.category : ''}
                      onChange={(e) => setFilters({ ...filters, category: e.target.value ? parseInt(e.target.value) as ExpenseCategory : undefined })}
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-sm text-black bg-white h-[38px]"
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
                    className="flex items-center gap-2 text-sm font-bold text-blue-600 hover:text-blue-800 transition-colors"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                    נקה פילטרים
                  </button>
                </div>

                {/* Expenses Table */}
                {loading ? (
                  <p className="text-center text-black font-bold py-8">טוען...</p>
                ) : expenses.length === 0 ? (
                  <p className="text-center text-black font-bold py-8">אין הוצאות להצגה</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="min-w-full divide-y divide-gray-200">
                      <thead className="bg-gray-100">
                        <tr>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            שם עסק
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            ח״פ / עוסק מורשה
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            תאריך
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            לפני מע״מ
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            אחרי מע״מ
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            מספר חשבונית
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            סוג מסמך
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            קטגוריה
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            מסמך
                          </th>
                          <th className="px-6 py-3 text-right text-xs font-bold text-black uppercase tracking-wider">
                            פעולות
                          </th>
                        </tr>
                      </thead>
                      <tbody className="bg-white divide-y divide-gray-200">
                        {expenses.map((expense) => {
                          // Calculate document type once per expense
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

                          // Format date once per expense
                          const date = new Date(expense.transactionDate);
                          const formattedDate = `${String(date.getUTCDate()).padStart(2, '0')}/${String(date.getUTCMonth() + 1).padStart(2, '0')}/${date.getUTCFullYear()}`;

                          return (
                            <tr key={expense.id} className="hover:bg-gray-50">
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                {expense.businessName}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                {expense.taxId || '-'}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                {formattedDate}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                ₪{expense.amountBeforeVat.toFixed(2)}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black font-semibold">
                                ₪{expense.amountAfterVat.toFixed(2)}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                {expense.invoiceNumber || '-'}
                              </td>
                              <td className="px-6 py-4 whitespace-nowrap text-sm text-black">
                                {(() => {
                                  // Display all 3 types in Hebrew
                                  if (docType === DocumentType.TaxInvoiceReceipt) {
                                    return 'חשבונית מס קבלה';
                                  } else if (docType === DocumentType.TaxInvoice) {
                                    return 'חשבונית מס';
                                  } else {
                                    return 'קבלה';
                                  }
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
                                      const parsed = parseInt(expense.category, 10);
                                      categoryValue = isNaN(parsed) ? ExpenseCategory.Other : parsed;
                                    }
                                  }
                                  // Ensure it's a valid enum value
                                  if (!Object.values(ExpenseCategory).includes(categoryValue as ExpenseCategory)) {
                                    categoryValue = ExpenseCategory.Other;
                                  }
                                  return categoryValue;
                                })()}
                                onChange={async (e) => {
                                  e.stopPropagation();
                                  const newCategory = parseInt(e.target.value, 10) as ExpenseCategory;
                                  const currentCategory = (() => {
                                    let cat = expense.category;
                                    if (cat === undefined || cat === null) return ExpenseCategory.Other;
                                    if (typeof cat === 'string') cat = parseInt(cat, 10);
                                    if (isNaN(cat as number)) return ExpenseCategory.Other;
                                    return Number(cat) as ExpenseCategory;
                                  })();
                                  
                                  console.log('Select onChange triggered:', {
                                    selectedValue: e.target.value,
                                    newCategory,
                                    currentCategory,
                                    expenseId: expense.id,
                                    expenseCategoryRaw: expense.category
                                  });
                                  
                                  if (!isNaN(newCategory) && newCategory !== currentCategory) {
                                    await handleCategoryChange(expense.id, newCategory);
                                  } else {
                                    console.log('Skipping update - same category or invalid value');
                                  }
                                }}
                                className="border border-gray-300 rounded-md px-2 py-1 text-sm text-black bg-white cursor-pointer"
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
                                  className="text-blue-600 hover:text-blue-900 font-medium"
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
                                className="text-red-600 hover:text-red-900"
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

