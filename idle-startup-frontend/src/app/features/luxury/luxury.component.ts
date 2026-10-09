import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GameService } from '../../core/services/game.service';
import { prestigeColor, prestigeLabel } from '../../core/game/prestige';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { LuxuryService } from './luxury.service';
import { LUXURY_CATEGORY_ICONS, LuxuryCategory, LuxuryItem, luxuryCategoryLabel } from './luxury.models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const ALL = 'All';

@Component({
  selector: 'app-luxury',
  standalone: true,
  imports: [RouterLink, MoneyPipe, TranslatePipe],
  templateUrl: './luxury.component.html',
  styleUrl: './luxury.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LuxuryComponent implements OnInit {
  readonly game = inject(GameService);
  private readonly luxury = inject(LuxuryService);

  readonly items = signal<LuxuryItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly category = signal<string>(ALL);
  readonly confirming = signal<LuxuryItem | null>(null);
  readonly buying = signal(false);
  readonly justBought = signal<string | null>(null);

  readonly all = ALL;
  readonly icons = LUXURY_CATEGORY_ICONS;
  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;

  readonly categories = computed(() => [ALL, ...new Set(this.items().map((i) => i.category))]);
  readonly visible = computed(() => {
    const c = this.category();
    return c === ALL ? this.items() : this.items().filter((i) => i.category === c);
  });
  readonly ownedCount = computed(() => this.items().filter((i) => i.isOwned).length);
  readonly collectionValue = computed(() => this.items().filter((i) => i.isOwned).reduce((s, i) => s + i.price, 0));

  ngOnInit(): void {
    this.game.ensureLoaded().subscribe({ error: () => void 0 });
    this.load();
  }

  load(): void {
    this.luxury.list().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'luxury.loadError'));
        this.loading.set(false);
      },
    });
  }

  categoryIcon(c: string): string {
    return this.icons[c as LuxuryCategory] ?? '✨';
  }

  categoryLabel(c: string): string {
    return luxuryCategoryLabel(c);
  }

  /** Like the business shop: the live purse can afford it even before the server's flag catches up. */
  affordable(item: LuxuryItem): boolean {
    return item.canAfford || this.game.cash() >= item.price;
  }

  buy(): void {
    const item = this.confirming();
    if (!item || this.buying()) return;
    this.buying.set(true);
    this.error.set('');

    this.luxury.buy(item).subscribe({
      next: () => {
        this.items.update((rows) => rows.map((r) => (r.id === item.id ? { ...r, isOwned: true } : r)));
        this.confirming.set(null);
        this.buying.set(false);
        this.justBought.set(item.id);
        setTimeout(() => this.justBought.set(null), 3000);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'luxury.buyError'));
        this.confirming.set(null);
        this.buying.set(false);
      },
    });
  }
}
