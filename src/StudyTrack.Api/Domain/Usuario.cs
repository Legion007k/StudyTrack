namespace StudyTrack.Api.Domain;

/// <summary>
/// Representa a un estudiante o usuario del sistema.
/// No almacena contrasenas ni credenciales: el sistema opera bajo autorizacion pura.
/// </summary>
public class Usuario
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Nombre { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
