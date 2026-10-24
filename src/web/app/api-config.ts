import { InjectionToken } from '@angular/core';

export interface IdentityConfig {
  authority: string;
  clientId: string;
}

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
export const IDENTITY_CONFIG = new InjectionToken<IdentityConfig | null>('IDENTITY_CONFIG');
export const AUTH_MODE = new InjectionToken<'local' | 'oidc'>('AUTH_MODE');
