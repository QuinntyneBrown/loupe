import { RouterLink } from '@angular/router';
import { Component } from '@angular/core';
import { ReferenceCollection } from 'domain';
@Component({
  selector: 'lp-inspiration-page',
  imports: [ReferenceCollection, RouterLink],
  templateUrl: './inspiration-page.html',
  styleUrl: './inspiration-page.css',
})
export class InspirationPage {}
