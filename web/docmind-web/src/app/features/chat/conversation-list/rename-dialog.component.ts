import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { FormErrorComponent } from '../../../shared/form-error/form-error.component';

export const CONVERSATION_TITLE_MAX_LENGTH = 200;

@Component({
  selector: 'dm-rename-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
    FormErrorComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()">
      <h2 mat-dialog-title>{{ 'chat.rename.title' | transloco }}</h2>
      <mat-dialog-content>
        <mat-form-field class="field" appearance="outline">
          <mat-label>{{ 'chat.rename.label' | transloco }}</mat-label>
          <input matInput formControlName="title" cdkFocusInitial autocomplete="off" />
          <mat-error><dm-form-error [control]="form.controls.title" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>
          {{ 'common.cancel' | transloco }}
        </button>
        <button mat-flat-button type="submit">{{ 'chat.rename.save' | transloco }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .field {
      width: 100%;
    }
  `,
})
export class RenameDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<RenameDialogComponent, string>>(MatDialogRef);
  private readonly currentTitle = inject<string>(MAT_DIALOG_DATA);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: [
      this.currentTitle,
      [Validators.required, Validators.maxLength(CONVERSATION_TITLE_MAX_LENGTH), noBlank],
    ],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.dialogRef.close(this.form.getRawValue().title.trim());
  }
}

function noBlank(control: { value: unknown }): { required: true } | null {
  return typeof control.value === 'string' && control.value.trim() === ''
    ? { required: true }
    : null;
}
