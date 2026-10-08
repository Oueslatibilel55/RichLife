import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { GameService } from '../../core/services/game.service';
import { BusinessDto, PrestigeLevel } from '../../core/models/game.models';
import {
  PRESTIGE_ORDER,
  nextPrestigeLabel,
  prestigeColor,
  prestigeLabel,
} from '../../core/game/prestige';
import { sectorIcon } from '../../core/game/sectors';
import { formatShiftLeft, managerAvatar, shiftMsLeft } from '../../core/game/managers';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';
import { ManageBusinessComponent } from './manage-business/manage-business.component';

interface PrestigeGroup {
  level: PrestigeLevel;
  label: string;
  color: string;
  items: BusinessDto[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [FormsModule, RouterLink, MoneyPipe, RatePipe, ManageBusinessComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  readonly game = inject(GameService);

  readonly companyName = signal('');
  readonly creating = signal(false);
  readonly createError = signal('');

  readonly prestigeLoading = signal(false);
  readonly prestigeSuccess = signal(false);
  readonly prestigeError = signal('');
  /** Prestige spends real cash, so it goes through a confirmation dialog first. */
  readonly confirmingPrestige = signal(false);

  readonly managedBusinessId = signal<string | null>(null);

  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;
  readonly sectorIcon = sectorIcon;
  readonly managerAvatar = managerAvatar;
  readonly shiftMsLeft = shiftMsLeft;
  readonly formatShiftLeft = formatShiftLeft;

  /**
   * Only prompt once we know there is no company — `loaded` separates "still
   * fetching" from "404, this player has none", so the modal no longer flashes
   * on every refresh.
   */
  readonly needsCompany = computed(() => this.game.loaded() && this.game.company() === null);

  readonly nextPrestigeName = computed(() => {
    const c = this.game.company();
    return c ? nextPrestigeLabel(c.prestigeLevel) : null;
  });

  /** Multiplier the player would hold after the next prestige: 1 + 0.18 x count. */
  readonly nextMultiplier = computed(() => {
    const c = this.game.company();
    return c ? 1 + 0.18 * (c.prestigeCount + 1) : 1;
  });

  readonly businessesByPrestige = computed<PrestigeGroup[]>(() => {
    const company = this.game.company();
    if (!company) return [];

    const groups = new Map<PrestigeLevel, BusinessDto[]>();
    for (const biz of company.businesses) {
      const key = biz.requiredPrestige;
      const bucket = groups.get(key);
      if (bucket) bucket.push(biz);
      else groups.set(key, [biz]);
    }

    return PRESTIGE_ORDER.filter((level) => (groups.get(level)?.length ?? 0) > 0).map((level) => ({
      level,
      label: prestigeLabel(level),
      color: prestigeColor(level),
      items: groups.get(level) ?? [],
    }));
  });

  ngOnInit(): void {
    // The catalogue backs the manage-assets modal; load it once per visit.
    if (this.game.catalogue().length === 0) {
      this.game.loadCatalogue().subscribe({ error: () => void 0 });
    }
  }

  createCompany(): void {
    const name = this.companyName().trim();
    if (!name || this.creating()) return;

    this.creating.set(true);
    this.createError.set('');

    this.game.createCompany(name).subscribe({
      next: () => {
        this.creating.set(false);
        this.game.loadCatalogue().subscribe({ error: () => void 0 });
      },
      error: (err: unknown) => {
        this.createError.set(toErrorMessage(err, 'Could not create that company.'));
        this.creating.set(false);
      },
    });
  }

  /**
   * Prestige is a cash purchase (businesses are kept), and the response is the new
   * truth — GameService adopts it, so there is nothing to refetch here.
   */
  triggerPrestige(): void {
    this.confirmingPrestige.set(false);
    if (!this.game.canPrestige() || this.prestigeLoading()) return;

    this.prestigeLoading.set(true);
    this.prestigeError.set('');

    this.game.prestige().subscribe({
      next: () => {
        this.prestigeLoading.set(false);
        this.prestigeSuccess.set(true);
        this.game.loadCatalogue().subscribe({ error: () => void 0 });
        setTimeout(() => this.prestigeSuccess.set(false), 3000);
      },
      error: (err: unknown) => {
        this.prestigeError.set(toErrorMessage(err, 'Prestige failed.'));
        this.prestigeLoading.set(false);
      },
    });
  }
}
