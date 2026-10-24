import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { registerLocaleData } from '@angular/common';
import { EnvironmentProviders, LOCALE_ID } from '@angular/core';
import localePtBr from '@angular/common/locales/pt';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { AppComponent } from './app/app.component';
import { API_BASE_URL, AUTH_MODE, IDENTITY_CONFIG, IdentityConfig } from './app/api-config';
import { localTokenInterceptor } from './app/local-token.interceptor';

interface RuntimeConfig {
  apiBaseUrl?: string;
  authMode?: 'local' | 'oidc';
  identity?: IdentityConfig | null;
}

registerLocaleData(localePtBr);

const configResponse = await fetch('/assets/app-config.json', { cache: 'no-store' });
if (!configResponse.ok) {
  throw new Error(`Could not load application configuration (${configResponse.status}).`);
}
const runtimeConfig = await configResponse.json() as RuntimeConfig;
const apiBaseUrl = runtimeConfig.apiBaseUrl?.trim().replace(/\/$/, '') ?? '';
const authMode = runtimeConfig.authMode === 'oidc' ? 'oidc' : 'local';
const identity = authMode === 'oidc' && runtimeConfig.identity?.authority && runtimeConfig.identity.clientId
  ? runtimeConfig.identity
  : null;
if (authMode === 'oidc' && !identity) {
  throw new Error('OIDC mode requires identity.authority and identity.clientId in app-config.json.');
}
const identityProviders: EnvironmentProviders[] = identity
  ? [provideAuth({
      config: {
        authority: identity.authority,
        clientId: identity.clientId,
        redirectUrl: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
        scope: 'openid profile email offline_access',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        secureRoutes: [`${apiBaseUrl}/api/`],
      },
    })]
  : [];
const httpInterceptors = identity
  ? [authInterceptor(), localTokenInterceptor]
  : [localTokenInterceptor];

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors(httpInterceptors)),
    ...identityProviders,
    { provide: API_BASE_URL, useValue: apiBaseUrl },
    { provide: AUTH_MODE, useValue: authMode },
    { provide: IDENTITY_CONFIG, useValue: identity },
    { provide: LOCALE_ID, useValue: 'pt-BR' },
  ],
}).catch((error: unknown) => console.error(error));
