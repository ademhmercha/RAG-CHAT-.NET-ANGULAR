import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import type { RouterStateSnapshot } from '@angular/router';
import { TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';

export const APP_NAME = 'DocMind';

@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    this.title.setTitle(key ? `${this.transloco.translate(key)} · ${APP_NAME}` : APP_NAME);
  }
}
