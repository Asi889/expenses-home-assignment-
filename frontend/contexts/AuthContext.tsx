'use client';

import React, { createContext, useContext, useState, useEffect } from 'react';
import { api, AuthResponse } from '@/lib/api';

interface AuthContextType {
  user: { id: number; email: string } | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => void;
  loading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<{ id: number; email: string } | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Check if user is authenticated by trying to get current user
    // Cookie is httpOnly, so we can't read it directly - we check by API call
    api.getCurrentUser()
      .then((userData) => {
        setUser(userData);
      })
      .catch(() => {
        // Not authenticated or token expired
        setUser(null);
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  const login = async (email: string, password: string) => {
    const response: AuthResponse = await api.login({ email, password });
    // Token is now in httpOnly cookie, automatically sent with requests
    const userData = await api.getCurrentUser();
    setUser(userData);
  };

  const register = async (email: string, password: string) => {
    const response: AuthResponse = await api.register({ email, password });
    // Token is now in httpOnly cookie, automatically sent with requests
    const userData = await api.getCurrentUser();
    setUser(userData);
  };

  const logout = async () => {
    try {
      await api.logout();
    } catch (error) {
      // Ignore errors on logout
    }
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, register, logout, loading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

