import { Component } from '@angular/core';
import { PhotographerCollection } from 'domain';

@Component({
  selector: 'lp-photographers-page',
  imports: [PhotographerCollection],
  templateUrl: './photographers-page.html',
  styleUrl: './photographers-page.css',
})
export class PhotographersPage {}
