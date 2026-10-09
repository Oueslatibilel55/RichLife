import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminService } from '../admin.service';
import { AdminLuxuryItem, CreateLuxuryRequest } from '../admin.models';
import { PRESTIGE_ORDER, prestigeLabel } from '../../../core/game/prestige';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';
import { LUXURY_CATEGORY_ICONS, LUXURY_CATEGORY_LABELS, LuxuryCategory } from '../../luxury/luxury.models';

interface LuxuryForm extends CreateLuxuryRequest {
  isActive: boolean;
}

const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * Create (`/admin/luxury/new`) or edit (`/admin/luxury/:id`) one luxury item — contract §7d.
 * `id` comes from the route (component input binding); absent means create.
 */
@Component({
  selector: 'app-luxury-editor',
  standalone: true,
  imports: [FormsModule, RouterLink, MoneyPipe, TranslatePipe],
  templateUrl: './luxury-editor.component.html',
  styleUrls: ['../catalogue/catalogue-editor.component.scss', './luxury-editor.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LuxuryEditorComponent implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly router = inject(Router);

  /** Route param; undefined on /admin/luxury/new. */
  readonly id = input<string>();

  form: LuxuryForm = this.blank();
  readonly owners = signal(0);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  /** The preview could not load imageUrl (missing file, typo). */
  readonly imageBroken = signal(false);

  readonly categories = Object.keys(LUXURY_CATEGORY_LABELS) as LuxuryCategory[];
  readonly categoryLabels = LUXURY_CATEGORY_LABELS;
  readonly categoryIcons = LUXURY_CATEGORY_ICONS;
  readonly levels = PRESTIGE_ORDER;
  readonly prestigeLabel = prestigeLabel;

  get isNew(): boolean {
    return !this.id();
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    // Arriving straight after a create: say so here, the list was skipped.
    if ((history.state as { created?: boolean } | null)?.created) this.flash(t('admin.luxury.created'));
    this.loading.set(true);
    this.admin.getLuxuryItem(id).subscribe({
      next: (item) => {
        this.fill(item);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadLuxury'));
        this.loading.set(false);
      },
    });
  }

  onImageUrlChange(url: string): void {
    this.form.imageUrl = url;
    this.imageBroken.set(false);
  }

  /** Mirrors the server's validation (§7d); returns its exact message, translated via dict/server.ts. */
  private validate(): string | null {
    const f = this.form;
    const url = f.imageUrl.trim();
    if (this.isNew && (!SLUG.test(f.id.trim()) || f.id.trim().length > 60))
      return 'Item id must be lowercase letters, digits and single dashes, at most 60 characters.';
    if (!f.name.trim() || f.name.trim().length > 80) return 'Name is required and must be at most 80 characters.';
    if (f.description.length > 300) return 'Description must be at most 300 characters.';
    if (!(Number(f.price) > 0)) return 'Price must be positive.';
    if (!url || url.length > 300 || !(url.startsWith('/') || url.startsWith('https://')))
      return 'Image URL is required, at most 300 characters, and must start with / or https://.';
    if (f.imageCredit.length > 200) return 'Image credit must be at most 200 characters.';
    if (f.imageSourceUrl.length > 500) return 'Image source URL must be at most 500 characters.';
    return null;
  }

  save(): void {
    if (this.saving()) return;
    const invalid = this.validate();
    if (invalid) {
      this.error.set(t(invalid));
      return;
    }
    const { id, isActive, ...rest } = this.form;
    const fields = {
      ...rest,
      name: rest.name.trim(),
      imageUrl: rest.imageUrl.trim(),
      price: Number(rest.price),
      displayOrder: Number(rest.displayOrder) || 0,
    };

    const call: Observable<AdminLuxuryItem> = this.isNew
      ? this.admin.createLuxury({ id: id.trim(), ...fields })
      : this.admin.updateLuxury(id, { ...fields, isActive });

    this.saving.set(true);
    this.error.set('');
    call.subscribe({
      next: (item) => {
        this.saving.set(false);
        if (this.isNew) {
          this.router.navigate(['/admin/luxury', item.id], { replaceUrl: true, state: { created: true } });
          return;
        }
        this.fill(item);
        this.flash(t('admin.editor.saved'));
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(toErrorMessage(err, 'admin.error.save'));
      },
    });
  }

  private fill(i: AdminLuxuryItem): void {
    this.form = {
      id: i.id, name: i.name, category: i.category, description: i.description, price: i.price,
      requiredPrestige: i.requiredPrestige, imageUrl: i.imageUrl, imageCredit: i.imageCredit,
      imageSourceUrl: i.imageSourceUrl, displayOrder: i.displayOrder, isActive: i.isActive,
    };
    this.owners.set(i.owners);
    this.imageBroken.set(false);
  }

  private flash(message: string): void {
    this.notice.set(message);
    setTimeout(() => this.notice.set(''), 3000);
  }

  private blank(): LuxuryForm {
    return {
      id: '', name: '', category: 'Watch', description: '', price: 100000, requiredPrestige: 'SmallBusiness',
      imageUrl: '/luxury/', imageCredit: '', imageSourceUrl: '', displayOrder: 100, isActive: true,
    };
  }
}
