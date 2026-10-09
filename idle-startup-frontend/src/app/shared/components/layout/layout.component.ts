import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { GameService } from '../../../core/services/game.service';
import { prestigeColor, prestigeLabel, prestigeShort } from '../../../core/game/prestige';
import { formatElapsed } from '../../../core/game/format';
import { MoneyPipe, RatePipe } from '../../pipes/money.pipe';
import { IconComponent, IconName } from '../icon/icon.component';
import { LangSwitcherComponent } from '../lang-switcher/lang-switcher.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

interface NavItem {
  path: string;
  /** Translation key. */
  label: string;
  icon: IconName;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive, MoneyPipe, RatePipe, IconComponent, LangSwitcherComponent, TranslatePipe],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LayoutComponent implements OnInit {
  readonly game = inject(GameService);
  readonly auth = inject(AuthService);

  readonly prestigeShort = prestigeShort;
  readonly prestigeColor = prestigeColor;
  readonly prestigeLabel = prestigeLabel;
  readonly formatElapsed = formatElapsed;

  /** Rendered twice: top nav on desktop, bottom tab bar on phones. Players only — admins have their own app. */
  readonly nav: readonly NavItem[] = [
    { path: '/dashboard', label: 'nav.dashboard', icon: 'dashboard' },
    { path: '/businesses', label: 'nav.businesses', icon: 'briefcase' },
    { path: '/luxury', label: 'nav.luxury', icon: 'gem' },
    { path: '/bank', label: 'nav.bank', icon: 'bank' },
    { path: '/leaderboard', label: 'nav.leaderboard', icon: 'trophy' },
    { path: '/profile', label: 'nav.profile', icon: 'contact' },
  ];

  readonly initial = computed(() =>
    (this.auth.currentUser()?.username ?? '?').charAt(0).toUpperCase(),
  );

  ngOnInit(): void {
    // The service outlives navigation — reuse live state, bootstrap only when empty.
    // A 404 here just means "no company yet"; the dashboard handles that. Anything else
    // has already been retried for about a minute (GameService.bootstrap): rather than
    // leave the player on an endless spinner, send them back to sign in.
    this.game.ensureLoaded().subscribe({ error: () => this.sessionLost() });
  }

  private sessionLost(): void {
    this.game.stopAll();
    this.game.reset();
    this.auth.logout('expired');
  }

  logout(): void {
    this.game.stopAll();
    this.game.reset();
    this.auth.logout();
  }
}
