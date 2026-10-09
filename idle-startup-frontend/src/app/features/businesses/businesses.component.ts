import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { GameService } from '../../core/services/game.service';
import { prestigeColor, prestigeLabel } from '../../core/game/prestige';
import { sectorIcon } from '../../core/game/sectors';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const ALL = 'All';

@Component({
  selector: 'app-businesses',
  standalone: true,
  imports: [MoneyPipe, RatePipe, IconComponent, TranslatePipe],
  templateUrl: './businesses.component.html',
  styleUrl: './businesses.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BusinessesComponent implements OnInit {
  readonly game = inject(GameService);

  readonly loading = signal(true);
  readonly openingId = signal<string | null>(null);
  readonly successId = signal<string | null>(null);
  readonly errorMsg = signal('');
  readonly selectedSector = signal<string>(ALL);
  readonly expandedId = signal<string | null>(null);

  readonly all = ALL;
  readonly sectorIcon = sectorIcon;
  readonly prestigeColor = prestigeColor;
  readonly prestigeLabel = prestigeLabel;

  readonly sectors = computed(() => [ALL, ...new Set(this.game.catalogue().map((b) => b.sector))]);

  readonly filtered = computed(() => {
    const sector = this.selectedSector();
    const list = this.game.catalogue();
    return sector === ALL ? list : list.filter((b) => b.sector === sector);
  });

  readonly expanded = computed(
    () => this.game.catalogue().find((b) => b.id === this.expandedId()) ?? null,
  );

  ngOnInit(): void {
    // GameService survives navigation — only bootstrap when there is nothing loaded.
    this.game.ensureLoaded().subscribe({
      next: () => this.fetchCatalogue(),
      error: () => this.fetchCatalogue(),
    });
  }

  private fetchCatalogue(): void {
    this.game.loadCatalogue().subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.errorMsg.set(toErrorMessage(err, 'businesses.loadError'));
        this.loading.set(false);
      },
    });
  }

  /**
   * `canAfford` is computed server-side against its last recorded cash, so it lags the
   * live ticker by up to one sync interval. The server stays the authority — it still
   * answers "Insufficient funds." — but the button follows the simulated purse so it
   * does not stay greyed out while the player watches the money arrive.
   */
  affordable(openingCost: number, canAfford: boolean): boolean {
    return canAfford || this.game.cash() >= openingCost;
  }

  openBusiness(catalogueId: string): void {
    if (this.openingId()) return;

    this.openingId.set(catalogueId);
    this.errorMsg.set('');

    this.game.openBusiness(catalogueId).subscribe({
      next: () => {
        this.openingId.set(null);
        this.successId.set(catalogueId);
        this.game.refreshCompany().subscribe({ error: () => void 0 });
        this.game.loadCatalogue().subscribe({ error: () => void 0 });
        setTimeout(() => this.successId.set(null), 3000);
      },
      error: (err: unknown) => {
        this.errorMsg.set(toErrorMessage(err, 'businesses.openError'));
        this.openingId.set(null);
      },
    });
  }

  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }
}
