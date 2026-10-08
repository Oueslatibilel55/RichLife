import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProfileService } from './profile.service';
import { Profile } from './profile.models';
import { prestigeColor, prestigeLabel } from '../../core/game/prestige';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';

type Filter = 'all' | 'unlocked' | 'locked';

/** "TN" -> 🇹🇳 via regional indicator symbols. */
function flagOf(country: string): string {
  if (!/^[A-Za-z]{2}$/.test(country)) return '';
  return String.fromCodePoint(...country.toUpperCase().split('').map((c) => 0x1f1e6 + c.charCodeAt(0) - 65));
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [RouterLink, DatePipe, DecimalPipe, MoneyPipe, RatePipe],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit {
  private readonly profiles = inject(ProfileService);

  readonly profile = signal<Profile | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly filter = signal<Filter>('all');

  readonly flagOf = flagOf;
  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;

  readonly initial = computed(() => (this.profile()?.username ?? '?').charAt(0).toUpperCase());

  readonly achievements = computed(() => {
    const list = this.profile()?.achievements ?? [];
    const f = this.filter();
    return f === 'all' ? list : list.filter((a) => (f === 'unlocked' ? a.unlocked : !a.unlocked));
  });

  readonly overall = computed(() => {
    const p = this.profile();
    return p && p.achievementsTotal > 0 ? (p.achievementsUnlocked / p.achievementsTotal) * 100 : 0;
  });

  ngOnInit(): void {
    this.profiles.get().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'Could not load your profile.'));
        this.loading.set(false);
      },
    });
  }

  percent(current: number, target: number): number {
    return target <= 0 ? 100 : Math.min(100, (current / target) * 100);
  }
}
