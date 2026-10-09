import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { toErrorMessage } from '../../../core/http/api-error';
import { COUNTRIES } from '../../../core/game/countries';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { LangSwitcherComponent } from '../../../shared/components/lang-switcher/lang-switcher.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent, LangSwitcherComponent, TranslatePipe],
  templateUrl: './register.component.html',
  styleUrl: '../auth.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  username = '';
  email = '';
  password = '';
  country = 'TN';
  readonly loading = signal(false);
  readonly error = signal('');

  /** All 249 ISO 3166-1 countries, alphabetical (core/game/countries.ts). */
  readonly countries = COUNTRIES;

  /**
   * The API has NO input validation on /auth/register — a blank username or email
   * reaches Player.Create and throws, surfacing as a 500 rather than a 400. These
   * checks are therefore load-bearing, not just UX polish.
   */
  private validate(): string | null {
    const username = this.username.trim();
    const email = this.email.trim();

    if (!username || !email || !this.password) return t('auth.fillAll');
    if (username.length > 30) return t('auth.register.usernameTooLong');
    if (email.length > 254) return t('auth.register.emailTooLong');
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return t('auth.register.emailInvalid');
    if (this.password.length < 6) return t('auth.register.passwordTooShort');
    if (!/^[A-Za-z]{2}$/.test(this.country)) return t('auth.register.countryRequired');
    return null;
  }

  onSubmit(): void {
    if (this.loading()) return;

    const invalid = this.validate();
    if (invalid) {
      this.error.set(invalid);
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.auth
      .register({
        username: this.username.trim(),
        email: this.email.trim(),
        password: this.password,
        country: this.country,
      })
      .subscribe({
        next: () => void this.router.navigate(['/dashboard']),
        error: (err: unknown) => {
          this.error.set(toErrorMessage(err, 'auth.register.failed'));
          this.loading.set(false);
        },
      });
  }
}
