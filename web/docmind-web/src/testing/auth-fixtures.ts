import type { AuthResponse } from '../app/core/api/api-types';

export const FUTURE_EXPIRY = '2100-01-01T00:00:00Z';

export function authResponse(accessToken = 'token-1', expiresAt = FUTURE_EXPIRY): AuthResponse {
  return {
    accessToken,
    expiresAt,
    user: {
      id: '11111111-1111-1111-1111-111111111111',
      email: 'ada@example.com',
      displayName: 'Ada Lovelace',
    },
  } as AuthResponse;
}
