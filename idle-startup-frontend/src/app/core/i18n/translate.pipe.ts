import { Pipe, PipeTransform } from '@angular/core';
import { t } from './i18n';

/**
 * `{{ 'nav.dashboard' | t }}` · `{{ 'x.y' | t: { n: 3 } }}`.
 * Impure on purpose: the result depends on the language signal, not only on the key.
 * `t()` reads that signal during template execution, so an OnPush view is marked dirty
 * when the language changes and this pipe runs again.
 */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  transform(key: string, params?: Readonly<Record<string, string | number>>): string {
    return t(key, params);
  }
}
