import { Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SESSION_SERVICE, ServiceError } from 'api';

@Component({
  selector: 'lp-sign-in',
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.css',
})
export class SignIn {
  private readonly session = inject(SESSION_SERVICE);
  private readonly router = inject(Router);
  readonly error = input<string>();
  readonly returnUrl = input<string>();
  readonly email = signal('');
  readonly password = signal('');
  readonly pending = signal(false);
  readonly failure = signal('');
  readonly fields = signal<Record<string, string>>({});
  async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.pending()) return;
    this.failure.set('');
    const email = this.email().trim();
    const password = this.password();
    const fields: Record<string, string> = {};
    if (email.length > 254 || !/^[^\s@]+@[^\s@]+$/.test(email))
      fields['email'] = 'Enter a valid email address.';
    if (Array.from(password).length < 15 || Array.from(password).length > 128)
      fields['password'] = 'Use a password with 15 to 128 characters.';
    this.fields.set(fields);
    if (Object.keys(fields).length) {
      this.password.set('');
      return;
    }
    this.pending.set(true);
    try {
      await this.session.signIn({ email, password });
      this.password.set('');
      await this.router.navigateByUrl(this.destination());
    } catch (error) {
      this.failure.set(
        error instanceof ServiceError && error.code === 'invalid_credentials'
          ? 'Email or password is incorrect.'
          : error instanceof ServiceError && error.code === 'sign_in_limit'
            ? 'Too many sign-in attempts. Try again in a minute.'
            : 'Sign-in is unavailable. Please try again.',
      );
    } finally {
      this.password.set('');
      this.pending.set(false);
    }
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
