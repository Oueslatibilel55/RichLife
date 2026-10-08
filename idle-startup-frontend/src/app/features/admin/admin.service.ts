import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminCatalogueEntry,
  AdminPlayer,
  AdminStats,
  CatalogueAssetFields,
  CatalogueEntryFields,
  ManagerNameRow,
} from './admin.models';

/** HTTP for the admin panel. Stateless — each page holds its own signals. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/admin`;

  stats(): Observable<AdminStats> {
    return this.http.get<AdminStats>(`${this.api}/stats`);
  }

  // -- Players ----------------------------------------------------------------

  players(search: string): Observable<AdminPlayer[]> {
    const params = search.trim() ? new HttpParams().set('search', search.trim()) : undefined;
    return this.http.get<AdminPlayer[]>(`${this.api}/players`, { params });
  }

  setAdmin(id: string, isAdmin: boolean): Observable<AdminPlayer> {
    return this.http.put<AdminPlayer>(`${this.api}/players/${id}/role`, { isAdmin });
  }

  setCash(id: string, cash: number): Observable<AdminPlayer> {
    return this.http.put<AdminPlayer>(`${this.api}/players/${id}/cash`, { cash });
  }

  resetPlayer(id: string): Observable<AdminPlayer> {
    return this.http.post<AdminPlayer>(`${this.api}/players/${id}/reset`, null);
  }

  deletePlayer(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/players/${id}`);
  }

  // -- Catalogue (contract §7) --------------------------------------------------

  catalogue(): Observable<AdminCatalogueEntry[]> {
    return this.http.get<AdminCatalogueEntry[]>(`${this.api}/catalogue`);
  }

  createEntry(id: string, fields: Omit<CatalogueEntryFields, 'isActive'>): Observable<AdminCatalogueEntry> {
    return this.http.post<AdminCatalogueEntry>(`${this.api}/catalogue`, { id, ...fields });
  }

  updateEntry(id: string, fields: CatalogueEntryFields): Observable<AdminCatalogueEntry> {
    return this.http.put<AdminCatalogueEntry>(`${this.api}/catalogue/${id}`, fields);
  }

  addAsset(entryId: string, asset: CatalogueAssetFields): Observable<AdminCatalogueEntry> {
    return this.http.post<AdminCatalogueEntry>(`${this.api}/catalogue/${entryId}/assets`, asset);
  }

  updateAsset(entryId: string, asset: CatalogueAssetFields): Observable<AdminCatalogueEntry> {
    const { id, ...body } = asset;
    return this.http.put<AdminCatalogueEntry>(`${this.api}/catalogue/${entryId}/assets/${id}`, body);
  }

  removeAsset(entryId: string, assetId: string): Observable<AdminCatalogueEntry> {
    return this.http.delete<AdminCatalogueEntry>(`${this.api}/catalogue/${entryId}/assets/${assetId}`);
  }

  // -- Manager names --------------------------------------------------------------

  managerNames(): Observable<ManagerNameRow[]> {
    return this.http.get<ManagerNameRow[]>(`${this.api}/manager-names`);
  }

  addManagerName(name: string): Observable<ManagerNameRow> {
    return this.http.post<ManagerNameRow>(`${this.api}/manager-names`, { name });
  }

  deleteManagerName(id: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/manager-names/${id}`);
  }
}
