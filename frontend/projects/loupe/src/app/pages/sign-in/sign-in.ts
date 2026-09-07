import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SESSION_SERVICE } from 'api';

@Component({ selector: 'lp-sign-in', templateUrl: './sign-in.html', styleUrl: './sign-in.css' })
export class SignIn {
  private readonly session = inject(SESSION_SERVICE);
  private readonly route = inject(ActivatedRoute);
  protected readonly failed =
    this.route.snapshot.queryParamMap.get('error') === 'authentication_failed';
  continue(): void {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/my-work';
    let destination = '/my-work';
    try {
      const decoded = decodeURIComponent(requested);
      if (
        decoded.startsWith('/') &&
        !decoded.startsWith('//') &&
        !/[\\\u0000-\u001f\u007f]/u.test(decoded) &&
        decoded.length <= 2048
      )
        destination = requested;
    } catch {
      // Malformed percent encoding cannot be a return destination.
    }
    this.session.signIn(destination);
  }
}
