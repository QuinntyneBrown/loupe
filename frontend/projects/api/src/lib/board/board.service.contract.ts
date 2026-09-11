import { InjectionToken } from '@angular/core';
import { ReferenceResult } from '../reference/reference-result';
import { BoardResult } from './board-result';

export interface IBoardService {
  list(): Promise<BoardResult[]>;
  create(name: string): Promise<BoardResult>;
  rename(id: string, revision: number, name: string): Promise<BoardResult>;
  delete(id: string, revision: number): Promise<void>;
  setMemberships(id: string, revision: number, boardIds: string[]): Promise<ReferenceResult>;
}
export const BOARD_SERVICE = new InjectionToken<IBoardService>('BOARD_SERVICE');
