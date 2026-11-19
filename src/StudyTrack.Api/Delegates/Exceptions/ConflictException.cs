namespace StudyTrack.Api.Delegates.Exceptions;

/// <summary>El recurso que se intenta crear ya existe (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);
