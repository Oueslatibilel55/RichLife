import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { GameService } from '../../../core/services/game.service';
import { toErrorMessage } from '../../../core/http/api-error';
import { formatShiftLeft } from '../../../core/game/managers';
import {
  TIER_ICONS,
  brokenUnits,
  faultLabel,
  placeIncomeNow,
  repairAllCost,
  repairMsLeft,
  unitStatus,
} from '../../../core/game/units';
import { BusinessDto, PlaceDto, PlaceKindDto, UnitDto } from '../../../core/models/game.models';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * The places of one business and the units in them (contract §5 Places and breakdowns): one
 * card per place — where it is, what it earns per day, its level, its units with OK or what
 * broke, one Repair action when something is broken and one Upgrade action — plus "add a unit"
 * and "open a new place". Lives inside ManageBusinessComponent.
 *
 * Unit states are derived on the 1-second clock (`GameService.now`), so a breakdown shows the
 * moment it happens without a request.
 */
@Component({
  selector: 'app-places',
  standalone: true,
  imports: [MoneyPipe, TranslatePipe],
  templateUrl: './places.component.html',
  styleUrl: './places.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlacesComponent {
  private readonly game = inject(GameService);

  readonly businessId = input.required<string>();

  readonly cash = this.game.cash;
  readonly now = this.game.now;
  /** One request at a time; the key names what is running ("repair:<id>", "level:<id>"…). */
  readonly busy = signal<string | null>(null);
  readonly error = signal('');
  /** The place whose "add a unit" list is open. */
  readonly pickerFor = signal<string | null>(null);

  readonly tierIcons = TIER_ICONS;
  readonly faultLabel = faultLabel;
  readonly levels = Array.from({ length: 10 }, (_, i) => i + 1);

  readonly business = computed<BusinessDto | null>(
    () => this.game.company()?.businesses.find((b) => b.id === this.businessId()) ?? null,
  );

  readonly entry = computed(() => {
    const biz = this.business();
    return biz ? this.game.catalogueEntry(biz.catalogueId) : null;
  });

  readonly broken = computed(() => {
    const biz = this.business();
    return biz ? brokenUnits(biz, this.now()) : [];
  });

  readonly repairAllPrice = computed(() => {
    const biz = this.business();
    return biz ? repairAllCost(biz, this.now()) : 0;
  });

  /** Place kinds still worth showing — none once the business has every place it may have. */
  readonly kinds = computed<PlaceKindDto[]>(() => {
    const biz = this.business();
    if (!biz || biz.places.length >= biz.maxPlaces) return [];
    return this.entry()?.placeKinds ?? [];
  });

  status(u: UnitDto) {
    return unitStatus(u, this.now());
  }

  backIn(u: UnitDto): string {
    return formatShiftLeft(repairMsLeft(u, this.now()));
  }

  /** What the place earns per day now, after the prestige multiplier — the card's headline. */
  perDay(p: PlaceDto): number {
    const multiplier = this.game.company()?.prestigeMultiplier ?? 1;
    return Math.max(0, placeIncomeNow(p, this.now())) * multiplier * 86_400;
  }

  /** What a new place of this kind would earn per day on its own, after its area and prestige. */
  kindPerDay(k: PlaceKindDto): number {
    const multiplier = this.game.company()?.prestigeMultiplier ?? 1;
    const level = this.business()?.levelMultiplier ?? 1;
    return k.baseIncomePerSecond * k.incomeMultiplier * level * multiplier * 86_400;
  }

  working(p: PlaceDto): number {
    return p.units.filter((u) => this.status(u) === 'Working').length;
  }

  togglePicker(placeId: string): void {
    this.pickerFor.update((id) => (id === placeId ? null : placeId));
  }

  repair(u: UnitDto, fast: boolean): void {
    const cost = fast ? u.fastRepairCost : u.repairCost;
    this.run(`repair:${u.id}`, () => this.game.repairUnit(u.id, fast, cost), 'places.error.repair');
  }

  repairAll(): void {
    const biz = this.business();
    if (!biz) return;
    this.run('repair-all', () => this.game.repairAll(biz.id, this.repairAllPrice()), 'places.error.repair');
  }

  levelUp(p: PlaceDto): void {
    if (p.nextLevelCost === null) return;
    const cost = p.nextLevelCost;
    this.run(`level:${p.id}`, () => this.game.levelUpPlace(p.id, cost), 'places.error.level');
  }

  buyUnit(p: PlaceDto, assetId: string, price: number): void {
    this.run(`buy:${p.id}:${assetId}`, () => this.game.buyUnit(p.id, assetId, price), 'manage.error.buyAsset', () =>
      // `isUnlocked` is computed server-side against the unit count.
      this.game.loadCatalogue().subscribe({ error: () => void 0 }),
    );
  }

  openPlace(kind: PlaceKindDto): void {
    const biz = this.business();
    if (!biz) return;
    this.run(`open:${kind.id}`, () => this.game.openPlace(biz.id, kind.id, kind.price), 'places.error.open');
  }

  private run(key: string, op: () => Observable<unknown>, fallback: string, then?: () => void): void {
    if (this.busy()) return;
    this.busy.set(key);
    this.error.set('');
    op().subscribe({
      next: () => {
        this.busy.set(null);
        then?.();
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, fallback));
        this.busy.set(null);
      },
    });
  }
}
