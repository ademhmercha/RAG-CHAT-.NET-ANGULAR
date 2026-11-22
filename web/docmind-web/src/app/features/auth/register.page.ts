import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { startWith } from 'rxjs';
import { AuthStore, sanitizeReturnUrl } from '../../core/auth/auth.store';
import { ProblemMessages, statusOf, toProblem } from '../../core/http/problem';
import { FormErrorComponent } from '../../shared/form-error/form-error.component';
import {
  DISPLAY_NAME_MAX_LENGTH,
  EMAIL_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  PASSWORD_REQUIREMENTS,
  applyProblemToForm,
  passwordStrength,
  unmetPasswordRequirements,
} from '../../shared/forms/validators';

@Component({
  selector: 'dm-register-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    TranslocoPipe,
    FormErrorComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.page.html',
  styleUrl: './register.page.scss',
})
export class RegisterPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly messages = inject(ProblemMessages);
  private readonly returnUrl = sanitizeReturnUrl(
    inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'),
  );

  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(DISPLAY_NAME_MAX_LENGTH)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(EMAIL_MAX_LENGTH)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(PASSWORD_MIN_LENGTH),
        Validators.maxLength(PASSWORD_MAX_LENGTH),
        passwordStrength,
      ],
    ],
  });

  private readonly password = toSignal(
    this.form.controls.password.valueChanges.pipe(startWith('')),
    { initialValue: '' },
  );

  protected readonly requirements = computed(() => {
    const unmet = unmetPasswordRequirements(this.password());
    return PASSWORD_REQUIREMENTS.map((requirement) => ({
      requirement,
      met: !unmet.includes(requirement),
    }));
  });

  protected readonly minLength = PASSWORD_MIN_LENGTH;
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly passwordVisible = signal(false);
  protected readonly loginQueryParams = this.returnUrl ? { returnUrl: this.returnUrl } : {};

  protected togglePasswordVisibility(): void {
    this.passwordVisible.update((visible) => !visible);
  }

  protected async submit(): Promise<void> {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    try {
      await this.auth.register(this.form.getRawValue());
      await this.router.navigateByUrl(this.returnUrl ?? '/chat');
    } catch (error) {
      this.showFailure(error);
    } finally {
      this.submitting.set(false);
    }
  }

  private showFailure(error: unknown): void {
    const problem = error instanceof HttpErrorResponse ? toProblem(error) : null;

    if (statusOf(error) === 409) {
      this.form.controls.email.setErrors({ server: this.messages.describe(error) });
      this.form.controls.email.markAsTouched();
      return;
    }

    if (!applyProblemToForm(this.form, problem)) {
      this.errorMessage.set(this.messages.describe(error));
    }
  }
}
