const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  email: string;
  message: string;
}

export enum DocumentType {
  Receipt = 'Receipt',                    // קבלה
  TaxInvoice = 'TaxInvoice',               // חשבונית מס
  TaxInvoiceReceipt = 'TaxInvoiceReceipt' // חשבונית מס קבלה
}

export interface Expense {
  id: number;
  businessName: string;
  transactionDate: string;
  amountBeforeVat: number;
  amountAfterVat: number;
  invoiceNumber?: string;
  category: ExpenseCategory;
  serviceProvided?: string;
  taxId?: string;
  isReceipt: boolean;
  documentType: DocumentType;
  fileName?: string;
}

export enum ExpenseCategory {
  Vehicle = 0,
  Food = 1,
  Operations = 2,
  IT = 3,
  Training = 4,
  Other = 5
}

export interface ExpenseFilter {
  startDate?: string;
  endDate?: string;
  minAmount?: number;
  maxAmount?: number;
  category?: ExpenseCategory;
  businessName?: string;
}

class ApiClient {
  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    // Add Authorization header if token exists in localStorage
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('authToken');
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }
    }

    // Remove Content-Type for FormData requests
    if (options.body instanceof FormData) {
      delete headers['Content-Type'];
    }

    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      ...options,
      headers,
      credentials: 'include', // Important: Include cookies in requests
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'An error occurred' }));
      throw new Error(error.message || 'Request failed');
    }

    // Handle 204 No Content (empty response)
    if (response.status === 204 || response.statusText === 'No Content') {
      return undefined as any;
    }

    // Check if response has content
    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      const text = await response.text();
      if (text.trim() === '') {
        return undefined as any;
      }
      return JSON.parse(text);
    }

    return undefined as any;
  }

  async register(data: RegisterRequest): Promise<AuthResponse> {
    const result = await this.request<AuthResponse & { token: string }>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    
    if (result && result.token) {
      localStorage.setItem('authToken', result.token);
    }
    
    return result;
  }

  async login(data: LoginRequest): Promise<AuthResponse> {
    const result = await this.request<AuthResponse & { token: string }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    });

    if (result && result.token) {
      localStorage.setItem('authToken', result.token);
    }

    return result;
  }

  async getCurrentUser() {
    return this.request<{ id: number; email: string }>('/auth/me');
  }

  async uploadInvoice(file: File): Promise<{ expense: Expense; analysis: any }> {
    const formData = new FormData();
    formData.append('file', file);
    // Document type will be detected automatically by OCR

    const response = await fetch(`${API_BASE_URL}/invoice/upload`, {
      method: 'POST',
      credentials: 'include', // Include cookies
      body: formData,
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Upload failed' }));
      throw new Error(error.message || 'Upload failed');
    }

    return response.json();
  }

  async getExpenses(filter?: ExpenseFilter): Promise<Expense[]> {
    const params = new URLSearchParams();
    if (filter) {
      if (filter.startDate) params.append('startDate', filter.startDate);
      if (filter.endDate) params.append('endDate', filter.endDate);
      if (filter.minAmount !== undefined) params.append('minAmount', filter.minAmount.toString());
      if (filter.maxAmount !== undefined) params.append('maxAmount', filter.maxAmount.toString());
      if (filter.category !== undefined) params.append('category', filter.category.toString());
      if (filter.businessName) params.append('businessName', filter.businessName);
    }

    const queryString = params.toString();
    return this.request<Expense[]>(`/expense${queryString ? `?${queryString}` : ''}`);
  }

  async logout(): Promise<void> {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('authToken');
    }
    return this.request<void>('/auth/logout', {
      method: 'POST',
    });
  }

  async updateExpenseCategory(expenseId: number, category: ExpenseCategory): Promise<Expense> {
    return this.request<Expense>(`/expense/${expenseId}/category`, {
      method: 'PUT',
      body: JSON.stringify({ category }),
    });
  }

  async updateExpenseDocumentType(expenseId: number, documentType: DocumentType): Promise<Expense> {
    return this.request<Expense>(`/expense/${expenseId}/document-type`, {
      method: 'PUT',
      body: JSON.stringify({ documentType }),
    });
  }

  async deleteExpense(expenseId: number): Promise<void> {
    return this.request<void>(`/expense/${expenseId}`, {
      method: 'DELETE',
    });
  }
}

export const api = new ApiClient();

