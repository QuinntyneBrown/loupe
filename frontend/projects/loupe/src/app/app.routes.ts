import { Routes } from '@angular/router';
import { sessionGuard } from './session.guard';
import { SignIn } from './pages/sign-in/sign-in';
import { MyWork } from './pages/my-work/my-work';
import { PhotographDetailPage } from './pages/photograph-detail/photograph-detail-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'my-work' },
  { path: 'sign-in', component: SignIn, title: 'Sign in · Loupe' },
  { path: 'my-work', component: MyWork, canActivate: [sessionGuard], title: 'My Work · Loupe' },
  {
    path: 'my-work/:id',
    component: PhotographDetailPage,
    canActivate: [sessionGuard],
    title: 'Photograph · Loupe',
  },
];
