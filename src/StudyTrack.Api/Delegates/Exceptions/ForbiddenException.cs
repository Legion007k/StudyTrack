namespace StudyTrack.Api.Delegates.Exceptions;

/// <summary>El usuario no tiene autorizacion para realizar la operacion solicitada.</summary>
public class ForbiddenException(string message) : Exception(message);
