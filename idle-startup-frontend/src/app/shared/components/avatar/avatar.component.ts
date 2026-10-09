import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AvatarDto } from '../../../core/models/game.models';

/**
 * A player's profile picture (contract §6e): the chosen avatar's emoji on its gradient, or the
 * name's initial on the brand gradient when none is chosen. Sized in pixels.
 */
@Component({
  selector: 'app-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', style: 'display:inline-flex;flex-shrink:0' },
  template: `
    <span
      class="avatar"
      [style.width.px]="size()"
      [style.height.px]="size()"
      [style.font-size.px]="avatar() ? size() * 0.56 : size() * 0.42"
      [style.background]="background()"
    >{{ avatar()?.icon ?? initial() }}</span>
  `,
  styles: `
    .avatar {
      display: grid;
      place-items: center;
      border-radius: 50%;
      color: #fff;
      font-weight: 800;
      line-height: 1;
      box-shadow: 0 2px 6px rgba(15, 23, 42, 0.12);
      user-select: none;
    }
  `,
})
export class AvatarComponent {
  readonly avatar = input<AvatarDto | null | undefined>(null);
  /** Whose initial to show when there is no avatar. */
  readonly name = input<string | null | undefined>('');
  readonly size = input(32);

  readonly initial = computed(() => (this.name() || '?').charAt(0).toUpperCase());

  readonly background = computed(() => {
    const a = this.avatar();
    return a
      ? `linear-gradient(135deg, ${a.from}, ${a.to})`
      : 'linear-gradient(135deg, #6366F1, #7C3AED)';
  });
}
