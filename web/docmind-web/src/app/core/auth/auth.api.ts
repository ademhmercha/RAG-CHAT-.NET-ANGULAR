import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import type { AuthResponse, LoginRequest, RegisterRequest, UserDto } from '../api/api-types';
import { SKIP_ERROR_NOTIFICATION } from '../http/http-context';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  private readonly silent = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  register(request: RegisterRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.apiUrl.build('/auth/register'), request, {
      context: this.silent,
    });
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.apiUrl.build('/auth/login'), request, {
      context: this.silent,
    });
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.apiUrl.build('/auth/refresh'), null, {
      context: this.silent,
    });
  }

  logout(): Observable<void> {
    return this.http.post<void>(this.apiUrl.build('/auth/logout'), null, { context: this.silent });
  }
}
