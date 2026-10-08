import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GameService } from '../../core/services/game.service';
import { LuxuryItem, OwnedLuxury } from './luxury.models';

/** The luxury shop (contract §6c). Buying is a spend, so it goes through GameService's sync-first flow. */
@Injectable({ providedIn: 'root' })
export class LuxuryService {
  private readonly http = inject(HttpClient);
  private readonly game = inject(GameService);
  private readonly api = `${environment.apiUrl}/game/luxury`;

  list(): Observable<LuxuryItem[]> {
    return this.http.get<LuxuryItem[]>(this.api);
  }

  buy(item: LuxuryItem): Observable<OwnedLuxury> {
    return this.game.spend(item.price, () => this.http.post<OwnedLuxury>(`${this.api}/${item.id}`, null));
  }
}
