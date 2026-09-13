namespace StudyTrack.Api.Delegates.Exceptions;

/// <summary>El recurso solicitado no existe.</summary>
public class NotFoundException(string message) : Exception(message);
