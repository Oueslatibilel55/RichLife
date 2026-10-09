import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AvatarComponent } from '../../shared/components/avatar/avatar.component';
import { ProfileService } from './profile.service';
import { OwnedBadge, Profile } from './profile.models';
import { prestigeColor, prestigeLabel } from '../../core/game/prestige';
import { toErrorMessage } from '../../core/http/api-error';
import { currentLang } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';
import { LangSwitcherComponent } from '../../shared/components/lang-switcher/lang-switcher.component';

type Filter = 'all' | 'unlocked' | 'locked';

/** Date styles used on the page, formatted in the current UI language. */
const DATE_FORMATS = {
  monthYear: { month: 'long', year: 'numeric' },
  day: { day: 'numeric', month: 'short', year: 'numeric' },
  dayTime: { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' },
} satisfies Record<string, Intl.DateTimeFormatOptions>;

/** "TN" -> 🇹🇳 via regional indicator symbols. */
function flagOf(country: string): string {
  if (!/^[A-Za-z]{2}$/.test(country)) return '';
  return String.fromCodePoint(...country.toUpperCase().split('').map((c) => 0x1f1e6 + c.charCodeAt(0) - 65));
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [RouterLink, DecimalPipe, MoneyPipe, RatePipe, TranslatePipe, LangSwitcherComponent, AvatarComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit {
  private readonly profiles = inject(ProfileService);

  readonly profile = signal<Profile | null>(null);
  readonly loading = signal(true);
  readonly filter = signal<Filter>('all');
  /** The raw failure, translated in `errorText` so a language switch re-renders it. */
  private readonly failure = signal<{ err: unknown } | null>(null);
  readonly errorText = computed(() => {
    const f = this.failure();
    return f ? toErrorMessage(f.err, 'profile.loadError') : '';
  });

  readonly filters: readonly Filter[] = ['all', 'unlocked', 'locked'];

  readonly flagOf = flagOf;
  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;


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
        this.failure.set({ err });
        this.loading.set(false);
      },
    });
  }

  /** Formats a UTC ISO date in the current UI language (reads the language signal, so it re-renders on a switch). */
  /** The featured badge, if the player chose one and still has it. */
  featured(p: Profile): OwnedBadge | null {
    return p.badges.find((b) => b.id === p.featuredBadgeId) ?? null;
  }

  date(iso: string | null, style: keyof typeof DATE_FORMATS): string {
    if (!iso) return '';
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? '' : d.toLocaleString(currentLang(), DATE_FORMATS[style]);
  }

  percent(current: number, target: number): number {
    return target <= 0 ? 100 : Math.min(100, (current / target) * 100);
  }
}
