import { ChangeDetectionStrategy, Component, OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { AdminService } from '../admin.service';
import { AdminCatalogueEntry, CatalogueAssetFields, CatalogueEntryFields } from '../admin.models';
import { PrestigeLevel, Sector } from '../../../core/models/game.models';
import { PRESTIGE_ORDER, prestigeLabel } from '../../../core/game/prestige';
import { SECTOR_ICONS } from '../../../core/game/sectors';
import { toErrorMessage } from '../../../core/http/api-error';
import { RatePipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';

interface EntryForm extends CatalogueEntryFields {
  id: string;
}

/** Fixed income is edited as text: empty means "derive from price" (null on the wire). */
interface AssetForm extends Omit<CatalogueAssetFields, 'fixedIncomePerSecond'> {
  fixed: string;
  incomePerSecond: number | null;
}

const blankAsset = (): AssetForm => ({
  id: '', name: '', price: 0, unlockAtAssetCount: 0, fixed: '', displayOrder: 10, incomePerSecond: null,
});

/**
 * Create or edit one catalogue entry and its assets (contract §7). Create first, then the
 * assets section unlocks — the API adds assets to an existing entry.
 */
@Component({
  selector: 'app-catalogue-editor',
  standalone: true,
  imports: [FormsModule, RatePipe, IconComponent, TranslatePipe],
  templateUrl: './catalogue-editor.component.html',
  styleUrl: './catalogue-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueEditorComponent implements OnChanges {
  private readonly admin = inject(AdminService);

  /** null = create a new entry. */
  readonly entry = input<AdminCatalogueEntry | null>(null);
  readonly saved = output<AdminCatalogueEntry>();
  readonly closed = output<void>();

  form: EntryForm = this.blankEntry();
  readonly assets = signal<AssetForm[]>([]);
  newAsset: AssetForm = blankAsset();

  readonly saving = signal(false);
  readonly busyAsset = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');

  readonly sectors = Object.keys(SECTOR_ICONS) as Sector[];
  readonly sectorIcons = SECTOR_ICONS;
  readonly levels = PRESTIGE_ORDER;
  readonly prestigeLabel = prestigeLabel;

  get isNew(): boolean {
    return this.entry() === null;
  }

  ngOnChanges(): void {
    const e = this.entry();
    this.form = e
      ? {
          id: e.id, name: e.name, sector: e.sector, requiredPrestige: e.requiredPrestige,
          openingCost: e.openingCost, baseIncomePerSecond: e.baseIncomePerSecond,
          monthlySalaryCost: e.monthlySalaryCost, baseEmployeeCount: e.baseEmployeeCount,
          description: e.description, displayOrder: e.displayOrder, isActive: e.isActive,
        }
      : this.blankEntry();
    this.loadAssets(e);
    this.error.set('');
  }

  saveEntry(): void {
    if (this.saving()) return;
    const { id, isActive, ...fields } = this.form;
    const call = this.isNew
      ? this.admin.createEntry(id.trim(), fields)
      : this.admin.updateEntry(id, { ...fields, isActive });
    this.submit(call, this.isNew ? t('admin.editor.created') : t('admin.editor.saved'));
  }

  saveAsset(row: AssetForm): void {
    this.assetCall(row.id, this.admin.updateAsset(this.form.id, this.toWire(row)), t('admin.editor.assetSaved', { name: row.name }));
  }

  removeAsset(row: AssetForm): void {
    this.assetCall(row.id, this.admin.removeAsset(this.form.id, row.id), t('admin.editor.assetRemoved', { name: row.name }));
  }

  addAsset(): void {
    const row = { ...this.newAsset, id: this.newAsset.id.trim() };
    this.assetCall('__new', this.admin.addAsset(this.form.id, this.toWire(row)), t('admin.editor.assetAdded', { name: row.name }), () => {
      this.newAsset = { ...blankAsset(), displayOrder: (this.assets().at(-1)?.displayOrder ?? 0) + 10 };
    });
  }

  private submit(call: Observable<AdminCatalogueEntry>, message: string): void {
    this.saving.set(true);
    this.error.set('');
    call.subscribe({
      next: (entry) => {
        this.saving.set(false);
        this.flash(message);
        this.saved.emit(entry); // parent swaps the input → ngOnChanges reloads the form
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(toErrorMessage(err, 'admin.error.save'));
      },
    });
  }

  private assetCall(key: string, call: Observable<AdminCatalogueEntry>, message: string, after?: () => void): void {
    this.busyAsset.set(key);
    this.error.set('');
    call.subscribe({
      next: (entry) => {
        this.busyAsset.set(null);
        this.loadAssets(entry);
        after?.();
        this.flash(message);
        this.saved.emit(entry);
      },
      error: (err: unknown) => {
        this.busyAsset.set(null);
        this.error.set(toErrorMessage(err, 'admin.error.saveAsset'));
      },
    });
  }

  private loadAssets(e: AdminCatalogueEntry | null): void {
    this.assets.set(
      (e?.availableAssets ?? []).map((a) => ({
        id: a.id, name: a.name, price: a.price, unlockAtAssetCount: a.unlockAtAssetCount,
        fixed: a.fixedIncomePerSecond === null ? '' : String(a.fixedIncomePerSecond),
        displayOrder: a.displayOrder, incomePerSecond: a.incomePerSecond,
      })),
    );
  }

  private toWire(row: AssetForm): CatalogueAssetFields {
    const fixed = String(row.fixed ?? '').trim();
    return {
      id: row.id, name: row.name, price: Number(row.price), unlockAtAssetCount: Number(row.unlockAtAssetCount),
      fixedIncomePerSecond: fixed === '' ? null : Number(fixed), displayOrder: Number(row.displayOrder),
    };
  }

  private flash(message: string): void {
    this.notice.set(message);
    setTimeout(() => this.notice.set(''), 3000);
  }

  private blankEntry(): EntryForm {
    return {
      id: '', name: '', sector: 'Services', requiredPrestige: 'TheHustle' as PrestigeLevel,
      openingCost: 1000, baseIncomePerSecond: 1, monthlySalaryCost: 0, baseEmployeeCount: 0,
      description: '', displayOrder: 100, isActive: true,
    };
  }
}
