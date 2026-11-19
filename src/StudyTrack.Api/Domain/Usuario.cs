namespace StudyTrack.Api.Domain;

/// <summary>
/// Estudiante registrado en el sistema. No almacena contrasenas ni credenciales:
/// el sistema no tiene autenticacion y el usuario se identifica con su Id.
/// </summary>
public class Usuario
{
    public string Id { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
