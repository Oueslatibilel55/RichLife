import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GameService } from '../../core/services/game.service';
import { HistoryRange, MarketDto, StockHistoryDto } from './market.models';

/**
 * The Bourse (contract §6f). A plain GET's cash is the server's last recorded figure — not
 * adopted; a trade syncs first and its answer's cash is adopted (GameService.syncThenAdoptCash).
 */
@Injectable({ providedIn: 'root' })
export class MarketService {
  private readonly http = inject(HttpClient);
  private readonly game = inject(GameService);
  private readonly api = `${environment.apiUrl}/game/market`;

  load(): Observable<MarketDto> {
    return this.http.get<MarketDto>(`${this.api}/`);
  }

  history(ticker: string, range: HistoryRange): Observable<StockHistoryDto> {
    return this.http.get<StockHistoryDto>(`${this.api}/${encodeURIComponent(ticker)}/history`, {
      params: new HttpParams().set('range', range),
    });
  }

  buy(ticker: string, shares: number): Observable<MarketDto> {
    return this.game.syncThenAdoptCash(() =>
      this.http.post<MarketDto>(`${this.api}/${encodeURIComponent(ticker)}/buy`, { shares }),
    );
  }

  sell(ticker: string, shares: number): Observable<MarketDto> {
    return this.game.syncThenAdoptCash(() =>
      this.http.post<MarketDto>(`${this.api}/${encodeURIComponent(ticker)}/sell`, { shares }),
    );
  }
}
