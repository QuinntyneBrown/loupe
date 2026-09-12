import { Component, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BOARD_SERVICE, BoardResult } from 'api';

@Component({
  selector: 'lp-board-navigation',
  imports: [RouterLink],
  templateUrl: './board-navigation.html',
  styleUrl: './board-navigation.css',
})
export class BoardNavigation implements OnInit {
  private readonly service = inject(BOARD_SERVICE);
  private readonly destroyed = inject(DestroyRef);
  private generation = 0;
  readonly selectedId = input<string | null>(null);
  readonly totalCount = input<number | null>(null);
  readonly newBoard = output<void>();
  readonly loaded = output<BoardResult[]>();
  readonly boards = signal<BoardResult[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  ngOnInit(): void {
    void this.refresh();
  }
  async refresh(): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.failed.set(false);
    try {
      const boards = await this.service.list();
      if (this.destroyed.destroyed || generation !== this.generation) return;
      this.boards.set(boards);
      this.loaded.emit(boards);
    } catch {
      if (!this.destroyed.destroyed && generation === this.generation) this.failed.set(true);
    } finally {
      if (!this.destroyed.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
