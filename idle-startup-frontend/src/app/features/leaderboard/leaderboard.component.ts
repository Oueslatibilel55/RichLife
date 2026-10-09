import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { GameService } from '../../core/services/game.service';
import { AuthService } from '../../core/services/auth.service';
import { LeaderboardEntryDto } from '../../core/models/game.models';
import { prestigeColor, prestigeLabel } from '../../core/game/prestige';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { AvatarComponent } from '../../shared/components/avatar/avatar.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** "TN" -> 🇹🇳 via regional indicator symbols. Falls back to the raw code. */
function flagOf(country: string): string {
  if (!/^[A-Za-z]{2}$/.test(country)) return country;
  return String.fromCodePoint(
    ...country
      .toUpperCase()
      .split('')
      .map((c) => 0x1f1e6 + c.charCodeAt(0) - 65),
  );
}

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [MoneyPipe, IconComponent, AvatarComponent, TranslatePipe],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardComponent implements OnInit {
  private readonly game = inject(GameService);
  private readonly auth = inject(AuthService);

  readonly entries = signal<LeaderboardEntryDto[]>([]);
  readonly loading = signal(true);
  /** The raw failure, translated in `errorText` so a language switch re-renders it. */
  private readonly failure = signal<{ err: unknown } | null>(null);
  readonly errorText = computed(() => {
    const f = this.failure();
    return f ? toErrorMessage(f.err, 'leaderboard.loadError') : '';
  });

  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;
  readonly flagOf = flagOf;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failure.set(null);

    this.game.getLeaderboard(50).subscribe({
      next: (rows) => {
        this.entries.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.failure.set({ err });
        this.loading.set(false);
      },
    });
  }

  isMe(username: string): boolean {
    return this.auth.currentUser()?.username === username;
  }

  medal(rank: number): string {
    if (rank === 1) return '🥇';
    if (rank === 2) return '🥈';
    if (rank === 3) return '🥉';
    return `#${rank}`;
  }
}
