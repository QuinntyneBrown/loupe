import { Component } from '@angular/core';
import { PhotographCollection } from 'domain';

@Component({
  selector: 'lp-my-work',
  imports: [PhotographCollection],
  templateUrl: './my-work.html',
  styleUrl: './my-work.css',
})
export class MyWork {}
