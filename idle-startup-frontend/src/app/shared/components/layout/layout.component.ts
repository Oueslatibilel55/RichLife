import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { GameService } from '../../../core/services/game.service';
import { prestigeColor, prestigeShort } from '../../../core/game/prestige';
import { formatElapsed } from '../../../core/game/format';
import { MoneyPipe, RatePipe } from '../../pipes/money.pipe';
import { IconComponent, IconName } from '../icon/icon.component';

interface NavItem {
  path: string;
  label: string;
  icon: IconName;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive, MoneyPipe, RatePipe, IconComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LayoutComponent implements OnInit {
  readonly game = inject(GameService);
  readonly auth = inject(AuthService);

  readonly prestigeShort = prestigeShort;
  readonly prestigeColor = prestigeColor;
  readonly formatElapsed = formatElapsed;

  /** Rendered twice: top nav on desktop, bottom tab bar on phones. Players only — admins have their own app. */
  readonly nav: readonly NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/businesses', label: 'Businesses', icon: 'briefcase' },
    { path: '/luxury', label: 'Luxury', icon: 'gem' },
    { path: '/leaderboard', label: 'Leaderboard', icon: 'trophy' },
    { path: '/profile', label: 'Profile', icon: 'contact' },
  ];

  readonly initial = computed(() =>
    (this.auth.currentUser()?.username ?? '?').charAt(0).toUpperCase(),
  );

  ngOnInit(): void {
    // The service outlives navigation — reuse live state, bootstrap only when empty.
    // A 404 here just means "no company yet"; the dashboard handles that.
    this.game.ensureLoaded().subscribe({ error: () => void 0 });
  }

  logout(): void {
    this.game.stopAll();
    this.game.reset();
    this.auth.logout();
  }
}
