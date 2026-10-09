import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { IconComponent, IconName } from '../../shared/components/icon/icon.component';
import { LangSwitcherComponent } from '../../shared/components/lang-switcher/lang-switcher.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface Section {
  path: string;
  /** Translation key. */
  label: string;
  icon: IconName;
  exact: boolean;
}

/**
 * The admin app's shell. Admins are staff, not players: no cash HUD, no game pages, and
 * GameService never starts (its loops live behind the player LayoutComponent). Reuses the
 * player layout's shell styles so both apps look alike.
 */
@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, IconComponent, LangSwitcherComponent, TranslatePipe],
  templateUrl: './admin-layout.component.html',
  styleUrls: ['../../shared/components/layout/layout.component.scss', './admin-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLayoutComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly sections: readonly Section[] = [
    { path: '/admin', label: 'admin.section.overview', icon: 'dashboard', exact: true },
    { path: '/admin/players', label: 'admin.section.players', icon: 'users', exact: false },
    { path: '/admin/catalogue', label: 'admin.section.catalogue', icon: 'briefcase', exact: false },
    { path: '/admin/managers', label: 'admin.section.managers', icon: 'contact', exact: false },
  ];

  readonly initial = computed(() => (this.auth.currentUser()?.username ?? '?').charAt(0).toUpperCase());

  /** Heading + blurb of the current section: translation keys from the child route's `data`. */
  private readonly page = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.childData()),
    ),
    { initialValue: this.childData() },
  );
  readonly heading = computed(() => this.page()['heading'] ?? 'admin.badge');
  readonly blurb = computed(() => this.page()['blurb'] ?? '');

  logout(): void {
    this.auth.logout();
  }

  /** Deepest active route's data. Empty while the component is still being constructed. */
  private childData(): Record<string, string> {
    let r: ActivatedRoute | null = this.route;
    while (r?.firstChild) r = r.firstChild;
    return (r?.snapshot?.data ?? {}) as Record<string, string>;
  }
}
