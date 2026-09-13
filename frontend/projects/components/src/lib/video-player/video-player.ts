import { Component, computed, inject, input } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'lp-video-player',
  templateUrl: './video-player.html',
  styleUrl: './video-player.css',
})
export class VideoPlayer {
  readonly videoId = input.required<string>();
  readonly title = input.required<string>();
  private readonly sanitizer = inject(DomSanitizer);
  /** The privacy-enhanced embed: no cookies until playback starts, no related videos from other channels. */
  readonly source = computed<SafeResourceUrl>(() =>
    this.sanitizer.bypassSecurityTrustResourceUrl(
      `https://www.youtube-nocookie.com/embed/${encodeURIComponent(this.videoId())}?rel=0`,
    ),
  );
}
