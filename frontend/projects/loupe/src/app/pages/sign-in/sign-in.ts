import { Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { SESSION_SERVICE } from 'api';

@Component({ selector: 'lp-sign-in', templateUrl: './sign-in.html', styleUrl: './sign-in.css' })
export class SignIn {
  private readonly session = inject(SESSION_SERVICE);
  private readonly router = inject(Router);
  readonly error = input<string>();
  readonly returnUrl = input<string>();
  continue(): void {
    this.session.signIn(this.destination());
  }
  retry(): void {
    void this.router.navigateByUrl(this.destination());
  }

  private destination(): string {
    const requested = this.returnUrl() ?? '/my-work';
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
    return destination;
  }
}
