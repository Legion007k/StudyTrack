using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace StudyTrack.Api.Extensions;

/// <summary>Cuerpo de error del contrato: type, title, status y errors por campo.</summary>
public sealed record ErrorResponse(
    string Type,
    string Title,
    int Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Errors = null);

public static class ProblemDetailsResponses
{
    public const string ContentType = "application/problem+json";

    public static IResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        Crear(new ErrorResponse(
            "https://tools.ietf.org/html/rfc7231#section-6.5.1", "Validation Failed", StatusCodes.Status400BadRequest,
            Errors: errors));

    public static IResult ValidationFailed(string campo, string mensaje) =>
        ValidationFailed(new Dictionary<string, string[]> { [campo] = [mensaje] });

    public static IResult NotFound(string detail) =>
        Crear(new ErrorResponse(
            "https://tools.ietf.org/html/rfc7231#section-6.5.4", "Not Found", StatusCodes.Status404NotFound,
            Detail: detail));

    public static IResult UnsupportedMediaType() =>
        Crear(new ErrorResponse(
            "https://tools.ietf.org/html/rfc7231#section-6.5.13", "Unsupported Media Type", StatusCodes.Status415UnsupportedMediaType,
            Detail: "El cuerpo de la solicitud debe enviarse como application/json."));

    public static IResult BusinessRuleViolation(IReadOnlyDictionary<string, string[]> errors) =>
        Crear(new ErrorResponse(
            "https://tools.ietf.org/html/rfc4918#section-11.2", "Business Rule Violation", StatusCodes.Status422UnprocessableEntity,
            Errors: errors));

    public static IResult InternalServerError() =>
        Crear(new ErrorResponse(
            "https://tools.ietf.org/html/rfc7231#section-6.6.1", "Internal Server Error", StatusCodes.Status500InternalServerError,
            Detail: "Ocurrio un error inesperado al procesar la solicitud."));

    private static IResult Crear(ErrorResponse body) =>
        Results.Json(body, contentType: ContentType, statusCode: body.Status);
}
