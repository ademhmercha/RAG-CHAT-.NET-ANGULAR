import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import type { AbstractControl, ValidationErrors } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { map, startWith, switchMap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';

export interface FormErrorMessage {
  readonly key: string;
  readonly params: Record<string, unknown>;
}

const ERROR_PRIORITY = [
  'required',
  'email',
  'minlength',
  'maxlength',
  'passwordStrength',
  'mismatch',
  'server',
] as const;

export function firstFormError(errors: ValidationErrors | null): FormErrorMessage | null {
  if (!errors) {
    return null;
  }

  const code = ERROR_PRIORITY.find((candidate) => candidate in errors) ?? Object.keys(errors)[0];
  if (!code) {
    return null;
  }

  const detail: unknown = errors[code];
  if (code === 'server') {
    return { key: '', params: { message: String(detail) } };
  }

  const params =
    typeof detail === 'object' && detail !== null ? (detail as Record<string, unknown>) : {};
  return { key: `forms.errors.${code}`, params };
}

@Component({
  selector: 'dm-form-error',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message(); as current) {
      {{ current.key ? (current.key | transloco: current.params) : current.params['message'] }}
    }
  `,
})
export class FormErrorComponent {
  readonly control = input.required<AbstractControl>();

  private readonly errors = toSignal(
    toObservable(this.control).pipe(
      switchMap((control) =>
        control.events.pipe(
          startWith(null),
          map(() => control.errors),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected readonly message = computed(() => firstFormError(this.errors()));
}
