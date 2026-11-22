import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../../testing/transloco-testing';
import { ApiProblemError, ProblemMessages, statusOf, toProblem } from './problem';

const httpError = (status: number, body: unknown) =>
  new HttpErrorResponse({ status, error: body, url: '/api/v1/x' });

describe('ProblemMessages', () => {
  let messages: ProblemMessages;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] });
    messages = TestBed.inject(ProblemMessages);
  });

  it('prefers the translation of the problem code', () => {
    const error = httpError(401, { code: 'auth.invalid_credentials' });

    expect(messages.describe(error)).toBe('The email or password is incorrect.');
  });

  it('falls back to the status when the code is unknown', () => {
    expect(messages.describe(httpError(404, { code: 'nope.nope' }))).toBe(
      'We could not find what you asked for.',
    );
  });

  it('describes network failures', () => {
    expect(messages.describe(httpError(0, null))).toContain('Cannot reach the server');
  });

  it('adds a trace reference to server errors', () => {
    const message = messages.describe(httpError(500, { traceId: 'abcdef1234567890' }));

    expect(message).toContain('(reference: abcdef12)');
  });

  it('understands streaming problems', () => {
    const error = new ApiProblemError(429, { code: 'rate_limit.exceeded' } as never);

    expect(statusOf(error)).toBe(429);
    expect(messages.describe(error)).toBe('You are going too fast. Please wait a few seconds.');
  });

  it('ignores non-object bodies', () => {
    expect(toProblem(httpError(500, 'boom'))).toBeNull();
  });
});
