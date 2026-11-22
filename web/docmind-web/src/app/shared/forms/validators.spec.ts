import { FormControl, FormGroup } from '@angular/forms';
import type { ProblemDetails } from '../../core/api/api-types';
import { applyProblemToForm, passwordStrength, unmetPasswordRequirements } from './validators';

describe('password validation', () => {
  it('lists every unmet requirement', () => {
    expect(unmetPasswordRequirements('abc')).toEqual(['length', 'uppercase', 'digit']);
    expect(unmetPasswordRequirements('Abcdefghij1')).toEqual([]);
  });

  it('reports missing requirements through the validator', () => {
    expect(passwordStrength(new FormControl('short'))).toEqual({
      passwordStrength: { missing: ['length', 'uppercase', 'digit'] },
    });
    expect(passwordStrength(new FormControl('Valid1Passw0rd'))).toBeNull();
    expect(passwordStrength(new FormControl(''))).toBeNull();
  });
});

describe('applyProblemToForm', () => {
  const build = () => new FormGroup({ email: new FormControl(''), password: new FormControl('') });

  it('maps server validation errors onto the matching controls', () => {
    const form = build();
    const problem = { errors: { email: ['Already used'], unknown: ['x'] } } as ProblemDetails;

    expect(applyProblemToForm(form, problem)).toBe(true);
    expect(form.controls.email.errors).toEqual({ server: 'Already used' });
    expect(form.controls.email.touched).toBe(true);
    expect(form.controls.password.errors).toBeNull();
  });

  it('reports nothing applied when there is no field error', () => {
    expect(applyProblemToForm(build(), null)).toBe(false);
    expect(applyProblemToForm(build(), { title: 'x' } as ProblemDetails)).toBe(false);
  });
});
