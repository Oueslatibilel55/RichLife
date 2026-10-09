import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LANGS, Lang, currentLang, setLang } from '../../../core/i18n/i18n';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Compact language picker — in the game top bar, the admin top bar and on the auth pages. */
@Component({
  selector: 'app-lang-switcher',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <select
      class="lang-select"
      [attr.aria-label]="'lang.label' | t"
      [title]="'lang.label' | t"
      [value]="lang()"
      (change)="change($any($event.target).value)"
    >
      @for (l of langs; track l.code) {
        <option [value]="l.code" [selected]="l.code === lang()">{{ l.label }}</option>
      }
    </select>
  `,
  styles: `
    .lang-select {
      height: 32px;
      padding: 0 8px;
      border: 1px solid var(--border);
      border-radius: var(--radius-sm, 8px);
      background: var(--surface);
      color: var(--text);
      font: inherit;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
    }
    .lang-select:focus-visible { outline: 2px solid var(--primary-ring); outline-offset: 1px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LangSwitcherComponent {
  readonly langs = LANGS;
  readonly lang = currentLang;

  change(code: string): void {
    setLang(code as Lang);
  }
}
