import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  imports: [EmptyStateComponent],
  template: `<dm-empty-state icon="folder" title="Nothing here" description="Add something"
    ><button>Add</button></dm-empty-state
  >`,
})
class HostComponent {}

describe('EmptyStateComponent', () => {
  it('renders its title, description and projected actions', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('h2')?.textContent).toContain('Nothing here');
    expect(element.querySelector('.description')?.textContent).toContain('Add something');
    expect(element.querySelector('button')?.textContent).toBe('Add');
    expect(element.querySelector('[role="status"]')).not.toBeNull();
  });
});
