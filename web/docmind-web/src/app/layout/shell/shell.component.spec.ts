import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideApiTesting } from '../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../testing/transloco-testing';
import { ShellComponent } from './shell.component';

@Component({ template: '<p id="page">Chat page</p>' })
class ChatStubComponent {}

describe('ShellComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideTranslocoTesting(),
        provideRouter([
          {
            path: '',
            component: ShellComponent,
            children: [{ path: 'chat', component: ChatStubComponent }],
          },
        ]),
      ],
    });
  });

  it('exposes the main navigation and renders the routed page in the main landmark', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/chat');

    const element = harness.routeNativeElement as HTMLElement;
    const links = Array.from(element.querySelectorAll('nav a .mdc-list-item__primary-text')).map(
      (link) => link.textContent?.trim(),
    );

    expect(links).toEqual(['Chat', 'Documents', 'Settings']);
    expect(element.querySelector('main#main-content #page')?.textContent).toBe('Chat page');
  });

  it('marks the current page link', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/chat');
    harness.detectChanges();

    const element = harness.routeNativeElement as HTMLElement;
    const current = element.querySelector('nav a[aria-current="page"]');

    expect(current?.getAttribute('href')).toBe('/chat');
  });

  it('offers a skip link to the main content', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/chat');

    const element = harness.routeNativeElement as HTMLElement;

    expect(element.querySelector('a.skip-link')?.getAttribute('href')).toBe('#main-content');
  });
});
