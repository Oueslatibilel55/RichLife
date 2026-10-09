import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminService } from '../admin.service';
import { AdminLuxuryItem } from '../admin.models';
import { PrestigeLevel } from '../../../core/models/game.models';
import { PRESTIGE_ORDER, prestigeColor, prestigeLabel } from '../../../core/game/prestige';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { LUXURY_CATEGORY_ICONS, LuxuryCategory, luxuryCategoryLabel } from '../../luxury/luxury.models';

const ALL = 'All';

/**
 * The luxury catalogue (contract §7d), grouped by prestige like the business catalogue —
 * whose list styles it reuses. Create / edit happen on their own route (luxury-editor).
 */
@Component({
  selector: 'app-admin-luxury',
  standalone: true,
  imports: [FormsModule, RouterLink, MoneyPipe, TranslatePipe],
  templateUrl: './admin-luxury.component.html',
  styleUrls: ['../catalogue/admin-catalogue.component.scss', './admin-luxury.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLuxuryComponent implements OnInit {
  private readonly admin = inject(AdminService);

  readonly items = signal<AdminLuxuryItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly search = signal('');
  readonly level = signal<string>(ALL);
  readonly showRetired = signal(true);

  readonly all = ALL;
  readonly levels = PRESTIGE_ORDER;
  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;
  readonly categoryLabel = luxuryCategoryLabel;

  /** The server already orders by prestige, display order, price; grouping keeps that order. */
  readonly groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const level = this.level();
    const rows = this.items().filter(
      (i) =>
        (this.showRetired() || i.isActive) &&
        (level === ALL || i.requiredPrestige === level) &&
        (!q || i.name.toLowerCase().includes(q) || i.id.includes(q)),
    );
    return PRESTIGE_ORDER.map((l: PrestigeLevel) => ({ level: l, items: rows.filter((i) => i.requiredPrestige === l) }))
      .filter((g) => g.items.length > 0);
  });

  readonly activeCount = computed(() => this.items().filter((i) => i.isActive).length);

  ngOnInit(): void {
    this.admin.getLuxury().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadLuxury'));
        this.loading.set(false);
      },
    });
  }

  categoryIcon(category: string): string {
    return LUXURY_CATEGORY_ICONS[category as LuxuryCategory] ?? '✨';
  }
}
