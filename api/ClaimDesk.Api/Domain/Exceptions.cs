namespace ClaimDesk.Api.Domain;

// Thrown by the rule classes and services.
// GlobalExceptionHandler turns each one into the HTTP status shown next to it.

public class UnauthorizedException(string message) : Exception(message);   // 401

public class ForbiddenException(string message) : Exception(message);      // 403

public class NotFoundException(string message) : Exception(message);       // 404

public class BusinessRuleException(string message) : Exception(message);   // 409
