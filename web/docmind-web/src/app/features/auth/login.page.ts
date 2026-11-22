import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthStore, sanitizeReturnUrl } from '../../core/auth/auth.store';
import { ProblemMessages, toProblem } from '../../core/http/problem';
import { FormErrorComponent } from '../../shared/form-error/form-error.component';
import {
  EMAIL_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  applyProblemToForm,
} from '../../shared/forms/validators';

@Component({
  selector: 'dm-login-page',
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
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly messages = inject(ProblemMessages);
  private readonly returnUrl = sanitizeReturnUrl(
    inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'),
  );

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(EMAIL_MAX_LENGTH)]],
    password: ['', [Validators.required, Validators.maxLength(PASSWORD_MAX_LENGTH)]],
  });

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly passwordVisible = signal(false);

  protected readonly registerQueryParams = this.returnUrl ? { returnUrl: this.returnUrl } : {};

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
      await this.auth.login(this.form.getRawValue());
      await this.router.navigateByUrl(this.returnUrl ?? '/chat');
    } catch (error) {
      this.showFailure(error);
    } finally {
      this.submitting.set(false);
    }
  }

  private showFailure(error: unknown): void {
    const problem = error instanceof HttpErrorResponse ? toProblem(error) : null;

    if (!applyProblemToForm(this.form, problem)) {
      this.errorMessage.set(this.messages.describe(error));
    }
  }
}
