import { Component } from '@angular/core';
import { LocationCollection } from 'domain';

@Component({
  selector: 'lp-locations-page',
  imports: [LocationCollection],
  templateUrl: './locations-page.html',
  styleUrl: './locations-page.css',
})
export class LocationsPage {}
