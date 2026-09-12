export class ServiceError extends Error {
  constructor(
    readonly code: string,
    readonly errors: Record<string, string[]> = {},
  ) {
    super(code);
  }
}
