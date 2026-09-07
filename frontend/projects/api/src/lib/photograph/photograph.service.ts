import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IPhotographService } from './photograph.service.contract';
import { PhotographPage } from './photograph-page';

@Injectable()
export class PhotographService implements IPhotographService {
  private readonly http = inject(HttpClient);
  list(cursor?: string): Promise<PhotographPage> {
    return firstValueFrom(
      this.http.get<PhotographPage>('/api/photographs', {
        params: cursor ? { cursor } : {},
      }),
    );
  }
}
