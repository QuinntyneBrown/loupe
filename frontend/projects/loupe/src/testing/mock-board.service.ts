import { Injectable } from '@angular/core';
import { BoardResult, IBoardService, ReferenceResult, ServiceError } from 'api';

@Injectable()
export class MockBoardService implements IBoardService {
  list(): Promise<BoardResult[]> {
    return this.call('list', {});
  }
  create(name: string): Promise<BoardResult> {
    return this.call('create', { name });
  }
  rename(id: string, revision: number, name: string): Promise<BoardResult> {
    return this.call('rename', { id, revision, name });
  }
  delete(id: string, revision: number): Promise<void> {
    return this.call('delete', { id, revision });
  }
  setMemberships(id: string, revision: number, boardIds: string[]): Promise<ReferenceResult> {
    return this.call('setMemberships', { id, revision, boardIds });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeBoards?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeBoards;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error);
    return result.data as T;
  }
}
