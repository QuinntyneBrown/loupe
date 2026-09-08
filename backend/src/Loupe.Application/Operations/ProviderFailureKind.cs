namespace Loupe.Application.Operations;

public enum ProviderFailureKind { Transient, RateLimited, AccessDenied, InvalidCredentials, UnsupportedInput, Disabled, InvalidOutput }
