import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../admin.service';
import { ManagerNameRow } from '../admin.models';
import { managerAvatar } from '../../../core/game/managers';
import { toErrorMessage } from '../../../core/http/api-error';

@Component({
  selector: 'app-admin-managers',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card add">
      <div>
        <h2>Manager names</h2>
        <p class="muted">Hiring picks one at random. {{ names().length }} in the pool · {{ usedCount() }} used.</p>
      </div>
      <form class="add__form" (ngSubmit)="add()">
        <input class="form-input" name="name" maxlength="40" placeholder="New name, e.g. Biscotte"
               aria-label="New manager name" [ngModel]="newName()" (ngModelChange)="newName.set($event)" />
        <button type="submit" class="btn btn--primary" [disabled]="!newName().trim() || saving()">Add</button>
      </form>
      @if (error()) { <div class="alert alert--error" role="alert">{{ error() }}</div> }
    </section>

    <input class="form-input filter" type="search" placeholder="Filter…" aria-label="Filter names"
           [ngModel]="filter()" (ngModelChange)="filter.set($event)" />

    @if (loading()) {
      <div class="loading-screen"><span class="spinner" aria-hidden="true"></span><p>Loading names…</p></div>
    } @else {
      <ul class="chips">
        @for (n of filtered(); track n.id) {
          <li class="chip" [class.chip--used]="n.inUse > 0">
            <span aria-hidden="true">{{ avatar(n.name) }}</span>
            <span class="chip__name">{{ n.name }}</span>
            @if (n.inUse > 0) {
              <span class="chip__count num" title="Businesses using this name">{{ n.inUse }}</span>
            } @else {
              <button type="button" class="chip__remove" [attr.aria-label]="'Delete ' + n.name"
                      [disabled]="deletingId() === n.id" (click)="remove(n)">✕</button>
            }
          </li>
        } @empty {
          <li class="muted">No names match.</li>
        }
      </ul>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 14px; }
    .muted { font-size: 13px; color: var(--text-3); }
    .add { display: flex; flex-direction: column; gap: 12px; h2 { font-size: 16px; } }
    .add__form { display: flex; gap: 8px; .form-input { flex: 1; max-width: 360px; } }
    .filter { max-width: 280px; }
    .chips { list-style: none; display: flex; flex-wrap: wrap; gap: 8px; }
    .chip {
      display: inline-flex; align-items: center; gap: 6px;
      padding: 6px 6px 6px 10px; border-radius: 999px;
      background: var(--surface); border: 1px solid var(--border-strong);
      font-size: 14px; font-weight: 600;
      &--used { background: var(--success-soft); border-color: #A7F3D0; padding-right: 8px; }
      &__count { min-width: 20px; padding: 0 6px; border-radius: 999px; background: var(--success); color: #fff; font-size: 11px; text-align: center; }
      &__remove {
        width: 24px; height: 24px; border: none; border-radius: 50%;
        background: var(--surface-2); color: var(--text-3); cursor: pointer; font-size: 11px;
        &:hover { background: var(--danger-soft); color: var(--danger); }
      }
    }
  `,
})
export class AdminManagersComponent implements OnInit {
  private readonly admin = inject(AdminService);

  readonly names = signal<ManagerNameRow[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly deletingId = signal<number | null>(null);
  readonly error = signal('');
  readonly newName = signal('');
  readonly filter = signal('');

  readonly avatar = managerAvatar;
  readonly usedCount = computed(() => this.names().filter((n) => n.inUse > 0).length);
  readonly filtered = computed(() => {
    const f = this.filter().trim().toLowerCase();
    return f ? this.names().filter((n) => n.name.toLowerCase().includes(f)) : this.names();
  });

  ngOnInit(): void {
    this.admin.managerNames().subscribe({
      next: (rows) => {
        this.names.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'Could not load names.'));
        this.loading.set(false);
      },
    });
  }

  add(): void {
    const name = this.newName().trim();
    if (!name || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.admin.addManagerName(name).subscribe({
      next: (row) => {
        this.names.update((rows) => [...rows, row].sort((a, b) => a.name.localeCompare(b.name)));
        this.newName.set('');
        this.saving.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'Could not add that name.'));
        this.saving.set(false);
      },
    });
  }

  remove(n: ManagerNameRow): void {
    this.deletingId.set(n.id);
    this.error.set('');
    this.admin.deleteManagerName(n.id).subscribe({
      next: () => {
        this.names.update((rows) => rows.filter((r) => r.id !== n.id));
        this.deletingId.set(null);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'Could not delete that name.'));
        this.deletingId.set(null);
      },
    });
  }
}
