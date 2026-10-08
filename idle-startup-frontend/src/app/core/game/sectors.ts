import { Sector } from '../models/game.models';

export const SECTOR_ICONS: Readonly<Record<Sector, string>> = {
  Transport: '🚛',
  RealEstate: '🏢',
  StockMarket: '📈',
  TechStartup: '💻',
  Hospitality: '🏨',
  Energy: '⚡',
  Services: '🔧',
};

export function sectorIcon(sector: Sector | string): string {
  return SECTOR_ICONS[sector as Sector] ?? '🏪';
}
