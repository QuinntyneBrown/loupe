import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SESSION_SERVICE } from 'api';

@Component({ selector: 'lp-sign-in', templateUrl: './sign-in.html', styleUrl: './sign-in.css' })
export class SignIn {
  private readonly session = inject(SESSION_SERVICE);
  private readonly route = inject(ActivatedRoute);
  continue(): void {
    this.session.signIn(this.route.snapshot.queryParamMap.get('returnUrl') ?? '/my-work');
  }
}
