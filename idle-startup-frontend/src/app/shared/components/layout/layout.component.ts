import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { GameService } from '../../../core/services/game.service';
import { prestigeColor, prestigeLabel, prestigeShort } from '../../../core/game/prestige';
import { formatCountdown, formatElapsed } from '../../../core/game/format';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../pipes/money.pipe';
import { IconComponent, IconName } from '../icon/icon.component';
import { AvatarComponent } from '../avatar/avatar.component';
import { LangSwitcherComponent } from '../lang-switcher/lang-switcher.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

interface NavItem {
  path: string;
  /** Translation key. */
  label: string;
  icon: IconName;
}

const DASHBOARD: NavItem = { path: '/dashboard', label: 'nav.dashboard', icon: 'dashboard' };
const BUSINESSES: NavItem = { path: '/businesses', label: 'nav.businesses', icon: 'briefcase' };
const STORE: NavItem = { path: '/store', label: 'nav.store', icon: 'store' };
const BANK: NavItem = { path: '/bank', label: 'nav.bank', icon: 'bank' };
const LUXURY: NavItem = { path: '/luxury', label: 'nav.luxury', icon: 'gem' };
const LEADERBOARD: NavItem = { path: '/leaderboard', label: 'nav.leaderboard', icon: 'trophy' };
const PROFILE: NavItem = { path: '/profile', label: 'nav.profile', icon: 'contact' };

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive, MoneyPipe, RatePipe, IconComponent, AvatarComponent, LangSwitcherComponent, TranslatePipe],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LayoutComponent implements OnInit {
  readonly game = inject(GameService);
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly prestigeShort = prestigeShort;
  readonly prestigeColor = prestigeColor;
  readonly prestigeLabel = prestigeLabel;
  readonly formatElapsed = formatElapsed;
  readonly formatCountdown = formatCountdown;

  /** Desktop top nav: every page. Players only — admins have their own app. */
  readonly nav: readonly NavItem[] = [DASHBOARD, BUSINESSES, STORE, BANK, LUXURY, LEADERBOARD, PROFILE];

  /** Phone tab bar: four pages around the Store, the rest behind "More". */
  readonly tabs: readonly NavItem[] = [DASHBOARD, BUSINESSES];
  readonly tabsAfterStore: readonly NavItem[] = [BANK];
  readonly moreItems: readonly NavItem[] = [LUXURY, LEADERBOARD, PROFILE];

  readonly moreOpen = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** "More" lights up when the current page is one of its items. */
  readonly moreActive = computed(() => this.moreItems.some((i) => this.url().startsWith(i.path)));


  // Welcome-back "double it" (contract §6e)
  readonly doubling = signal(false);
  readonly doubled = signal(false);
  readonly doubleError = signal('');

  ngOnInit(): void {
    // The service outlives navigation — reuse live state, bootstrap only when empty.
    // A 404 here just means "no company yet"; the dashboard handles that. Anything else
    // has already been retried for about a minute (GameService.bootstrap): rather than
    // leave the player on an endless spinner, send them back to sign in.
    this.game.ensureLoaded().subscribe({ error: () => this.sessionLost() });
  }

  doubleOffline(): void {
    if (this.doubling()) return;
    this.doubling.set(true);
    this.doubleError.set('');
    this.game.doubleOffline().subscribe({
      next: () => {
        this.doubling.set(false);
        this.doubled.set(true);
      },
      error: (err: unknown) => {
        this.doubling.set(false);
        this.doubleError.set(toErrorMessage(err, 'store.error'));
      },
    });
  }

  closeOffline(): void {
    this.game.dismissOfflineEarnings();
    this.doubled.set(false);
    this.doubleError.set('');
  }

  private sessionLost(): void {
    this.game.stopAll();
    this.game.reset();
    this.auth.logout('expired');
  }

  logout(): void {
    this.moreOpen.set(false);
    this.game.stopAll();
    this.game.reset();
    this.auth.logout();
  }
}
