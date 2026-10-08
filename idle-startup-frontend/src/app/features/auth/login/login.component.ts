import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { toErrorMessage } from '../../../core/http/api-error';
import { IconComponent } from '../../../shared/components/icon/icon.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent],
  templateUrl: './login.component.html',
  styleUrl: '../auth.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  email = '';
  password = '';
  readonly loading = signal(false);
  readonly error = signal('');

  onSubmit(): void {
    if (this.loading()) return;

    if (!this.email.trim() || !this.password) {
      this.error.set('Please fill in all fields.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.auth.login({ email: this.email.trim(), password: this.password }).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (err: unknown) => {
        // Here a 401 means bad credentials, not an expired session — and the server
        // deliberately sends an empty body so it cannot say which field was wrong.
        this.error.set(
          err instanceof HttpErrorResponse && err.status === 401
            ? 'Invalid email or password.'
            : toErrorMessage(err, 'Could not sign you in.'),
        );
        this.loading.set(false);
      },
    });
  }
}
