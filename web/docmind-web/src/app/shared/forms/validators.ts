import type { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';
import type { ProblemDetails } from '../../core/api/api-types';

export const PASSWORD_MIN_LENGTH = 10;
export const PASSWORD_MAX_LENGTH = 128;
export const EMAIL_MAX_LENGTH = 256;
export const DISPLAY_NAME_MAX_LENGTH = 100;

export type PasswordRequirement = 'length' | 'lowercase' | 'uppercase' | 'digit';

export const PASSWORD_REQUIREMENTS: readonly PasswordRequirement[] = [
  'length',
  'lowercase',
  'uppercase',
  'digit',
];

export function unmetPasswordRequirements(password: string): PasswordRequirement[] {
  const checks: Record<PasswordRequirement, boolean> = {
    length: password.length >= PASSWORD_MIN_LENGTH,
    lowercase: /[a-z]/.test(password),
    uppercase: /[A-Z]/.test(password),
    digit: /\d/.test(password),
  };

  return PASSWORD_REQUIREMENTS.filter((requirement) => !checks[requirement]);
}

export const passwordStrength: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const value = control.value as string | null;
  if (!value) {
    return null;
  }

  const missing = unmetPasswordRequirements(value);
  return missing.length > 0 ? { passwordStrength: { missing } } : null;
};

export function applyProblemToForm(form: FormGroup, problem: ProblemDetails | null): boolean {
  let applied = false;

  for (const [field, messages] of Object.entries(problem?.errors ?? {})) {
    const control = form.get(field);
    const message = messages[0];

    if (control && message) {
      control.setErrors({ server: message });
      control.markAsTouched();
      applied = true;
    }
  }

  return applied;
}
