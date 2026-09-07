import { Component } from '@angular/core';
import { PhotographCollection } from 'domain';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'lp-my-work',
  imports: [PhotographCollection, RouterLink],
  templateUrl: './my-work.html',
  styleUrl: './my-work.css',
})
export class MyWork {}
