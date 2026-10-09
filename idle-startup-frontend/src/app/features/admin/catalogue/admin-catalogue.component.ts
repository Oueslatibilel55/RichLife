import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../admin.service';
import { AdminCatalogueEntry } from '../admin.models';
import { PrestigeLevel } from '../../../core/models/game.models';
import { PRESTIGE_ORDER, prestigeColor, prestigeLabel } from '../../../core/game/prestige';
import { sectorIcon } from '../../../core/game/sectors';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../../shared/pipes/money.pipe';
import { CatalogueEditorComponent } from './catalogue-editor.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

const ALL = 'All';

@Component({
  selector: 'app-admin-catalogue',
  standalone: true,
  imports: [FormsModule, MoneyPipe, RatePipe, CatalogueEditorComponent, TranslatePipe],
  templateUrl: './admin-catalogue.component.html',
  styleUrl: './admin-catalogue.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCatalogueComponent implements OnInit {
  private readonly admin = inject(AdminService);

  readonly entries = signal<AdminCatalogueEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly search = signal('');
  readonly level = signal<string>(ALL);
  readonly showRetired = signal(true);

  /** undefined = editor closed, null = creating, an entry = editing it. */
  readonly editing = signal<AdminCatalogueEntry | null | undefined>(undefined);

  readonly all = ALL;
  readonly levels = PRESTIGE_ORDER;
  readonly prestigeLabel = prestigeLabel;
  readonly prestigeColor = prestigeColor;
  readonly sectorIcon = sectorIcon;

  readonly groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const level = this.level();
    const rows = this.entries().filter(
      (e) =>
        (this.showRetired() || e.isActive) &&
        (level === ALL || e.requiredPrestige === level) &&
        (!q || e.name.toLowerCase().includes(q) || e.id.includes(q)),
    );
    return PRESTIGE_ORDER.map((l: PrestigeLevel) => ({ level: l, items: rows.filter((e) => e.requiredPrestige === l) }))
      .filter((g) => g.items.length > 0);
  });

  readonly activeCount = computed(() => this.entries().filter((e) => e.isActive).length);

  ngOnInit(): void {
    this.admin.catalogue().subscribe({
      next: (rows) => {
        this.entries.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadCatalogue'));
        this.loading.set(false);
      },
    });
  }

  /** The editor saved: put the fresh entry in the list (insert when it is new). */
  onSaved(entry: AdminCatalogueEntry): void {
    this.entries.update((rows) =>
      rows.some((r) => r.id === entry.id) ? rows.map((r) => (r.id === entry.id ? entry : r)) : [...rows, entry],
    );
    if (this.editing() === null) this.editing.set(entry); // created → keep editing (assets)
  }

  /** Minutes for the base income alone to pay the opening cost back; null without base income. */
  payback(e: AdminCatalogueEntry): number | null {
    return e.baseIncomePerSecond > 0 ? Math.round(e.openingCost / e.baseIncomePerSecond / 60) : null;
  }
}
