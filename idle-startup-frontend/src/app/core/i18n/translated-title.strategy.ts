import { Injectable, effect, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { currentLang, t } from './i18n';

/** Route `title`s are translation keys; re-translates the tab title when the language changes. */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private key: string | undefined;

  constructor() {
    super();
    effect(() => {
      currentLang();
      if (this.key) this.title.setTitle(t(this.key));
    });
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.key = this.buildTitle(snapshot);
    if (this.key) this.title.setTitle(t(this.key));
  }
}
