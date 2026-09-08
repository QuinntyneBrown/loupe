namespace Loupe.Application.ReferenceImports;

/// <summary>A missing robots.txt permits fetching; a matching disallow blocks it; an
/// authentication error, server error, timeout, or oversized response defers automated
/// fetching rather than guessing. See L2-011.1.</summary>
public enum RobotsDecision { Allowed, Disallowed, Unavailable }
