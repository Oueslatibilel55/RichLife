import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { AdminService } from '../admin.service';
import { AdminStats } from '../admin.models';
import { prestigeColor, prestigeLabel } from '../../../core/game/prestige';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';

@Component({
  selector: 'app-admin-overview',
  standalone: true,
  imports: [MoneyPipe, DatePipe, DecimalPipe, IconComponent, TranslatePipe],
  templateUrl: './admin-overview.component.html',
  styleUrl: './admin-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOverviewComponent implements OnInit {
  private readonly admin = inject(AdminService);

  readonly stats = signal<AdminStats | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;

  /** Bar widths relative to the busiest prestige level. */
  readonly maxPerLevel = computed(() =>
    Math.max(1, ...(this.stats()?.prestigeDistribution.map((p) => p.companies) ?? [0])),
  );

  readonly maxOwners = computed(() => Math.max(1, ...(this.stats()?.topBusinesses.map((b) => b.owners) ?? [0])));

  readonly maxPerAchievement = computed(() =>
    Math.max(1, ...(this.stats()?.achievementDistribution.map((a) => a.companies) ?? [0])),
  );

  readonly maxAvatarUse = computed(() =>
    Math.max(1, ...(this.stats()?.avatarDistribution.map((a) => Math.max(a.owners, a.inUse)) ?? [0])),
  );

  readonly maxPerBadge = computed(() =>
    Math.max(1, ...(this.stats()?.badgeDistribution.map((b) => b.owners) ?? [0])),
  );

  /** Translated title (`ach.<code>.title`); a code the client does not know yet shows the server's English. */
  achTitle(code: string, fallback: string): string {
    const key = `ach.${code}.title`;
    const text = t(key);
    return text === key ? fallback : text;
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.admin.stats().subscribe({
      next: (s) => {
        this.stats.set(s);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadStats'));
        this.loading.set(false);
      },
    });
  }
}
