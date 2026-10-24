import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL, AUTH_MODE } from './api-config';

export const localTokenInterceptor: HttpInterceptorFn = (request, next) => {
  if (inject(AUTH_MODE) !== 'local') {
    return next(request);
  }

  const apiBaseUrl = inject(API_BASE_URL);
  const apiRoot = new URL(`${apiBaseUrl}/api/`, window.location.origin).href;
  const requestUrl = new URL(request.url, window.location.origin).href;
  const token = window.sessionStorage.getItem('avanco-access-token');
  if (!token || !requestUrl.startsWith(apiRoot)) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
