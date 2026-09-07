export class ServiceError extends Error {
  constructor(readonly code: string) {
    super(code);
  }
}
