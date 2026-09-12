import { ReferenceLinkPage } from './pages/reference-link/reference-link-page';
import { photographerUnsavedGuard } from './photographer-unsaved.guard';
import { photographerDraftUnsavedGuard } from './photographer-draft-unsaved.guard';
import { referenceDraftUnsavedGuard } from './reference-draft-unsaved.guard';
import { referenceLinkUnsavedGuard } from './reference-link-unsaved.guard';
import { referenceUnsavedGuard } from './reference-unsaved.guard';
import { ReferenceUploadPage } from './pages/reference-upload/reference-upload-page';
import { referenceUploadUnsavedGuard } from './reference-upload-unsaved.guard';
import { InspirationPage } from './pages/inspiration/inspiration-page';
import { ReferenceDetailPage } from './pages/reference-detail/reference-detail-page';
import { Routes } from '@angular/router';
import { sessionGuard } from './session.guard';
import { locationsUnsavedGuard } from './locations-unsaved.guard';
import { locationUnsavedGuard } from './location-unsaved.guard';
import { SignIn } from './pages/sign-in/sign-in';
import { MyWork } from './pages/my-work/my-work';
import { PhotographDetailPage } from './pages/photograph-detail/photograph-detail-page';
import { photographUnsavedGuard } from './photograph-unsaved.guard';
import { photographUploadUnsavedGuard } from './photograph-upload-unsaved.guard';
import { DeletionPage } from './pages/deletion/deletion-page';
import { ComparePage } from './pages/compare/compare-page';

export const routes: Routes = [
  {
    path: 'locations/find',
    loadComponent: () =>
      import('./pages/find-location/find-location-page').then((module) => module.FindLocationPage),
    canActivate: [sessionGuard],
    title: 'Find a location \u00b7 Loupe',
  },
  {
    path: 'locations/:id',
    loadComponent: () =>
      import('./pages/location-detail/location-detail-page').then(
        (module) => module.LocationDetailPage,
      ),
    canActivate: [sessionGuard],
    canDeactivate: [locationUnsavedGuard],
    title: 'Location · Loupe',
  },
  {
    path: 'locations',
    loadComponent: () =>
      import('./pages/locations/locations-page').then((module) => module.LocationsPage),
    canActivate: [sessionGuard],
    canDeactivate: [locationsUnsavedGuard],
    title: 'Locations \u00b7 Loupe',
  },
  {
    path: 'search',
    loadComponent: () => import('./pages/search/search-page').then((module) => module.SearchPage),
    canActivate: [sessionGuard],
    title: 'Search · Loupe',
  },
  {
    path: 'photographers/:id',
    canDeactivate: [photographerUnsavedGuard],
    loadComponent: () =>
      import('./pages/photographer-detail/photographer-detail-page').then(
        (module) => module.PhotographerDetailPage,
      ),
    canActivate: [sessionGuard],
    title: 'Photographer · Loupe',
  },
  {
    path: 'photographers',
    loadComponent: () =>
      import('./pages/photographers/photographers-page').then((module) => module.PhotographersPage),
    canActivate: [sessionGuard],
    canDeactivate: [photographerDraftUnsavedGuard],
    title: 'Photographers · Loupe',
  },
  {
    path: 'inspiration',
    component: InspirationPage,
    canActivate: [sessionGuard],
    canDeactivate: [referenceDraftUnsavedGuard],
    title: 'Inspiration \u00b7 Loupe',
  },
  {
    path: 'inspiration/upload',
    component: ReferenceUploadPage,
    canActivate: [sessionGuard],
    canDeactivate: [referenceUploadUnsavedGuard],
    title: 'Upload reference \u00b7 Loupe',
  },
  {
    path: 'inspiration/link',
    component: ReferenceLinkPage,
    canActivate: [sessionGuard],
    canDeactivate: [referenceLinkUnsavedGuard],
    title: 'Save link \u00b7 Loupe',
  },
  {
    path: 'inspiration/:id',
    component: ReferenceDetailPage,
    canActivate: [sessionGuard],
    canDeactivate: [referenceUnsavedGuard],
    title: 'Reference \u00b7 Loupe',
  },
  {
    path: 'compare',
    component: ComparePage,
    canActivate: [sessionGuard],
    title: 'Compare attempts · Loupe',
  },
  { path: '', pathMatch: 'full', redirectTo: 'my-work' },
  { path: 'sign-in', component: SignIn, title: 'Sign in · Loupe' },
  {
    path: 'my-work',
    component: MyWork,
    canActivate: [sessionGuard],
    canDeactivate: [photographUploadUnsavedGuard],
    title: 'My Work · Loupe',
  },
  {
    path: 'deletions/:id',
    component: DeletionPage,
    canActivate: [sessionGuard],
    title: 'Deletion status · Loupe',
  },
  {
    path: 'my-work/upload',
    pathMatch: 'full',
    redirectTo: 'my-work',
  },
  {
    path: 'my-work/:id',
    component: PhotographDetailPage,
    canActivate: [sessionGuard],
    canDeactivate: [photographUnsavedGuard],
    title: 'Photograph · Loupe',
  },
];
