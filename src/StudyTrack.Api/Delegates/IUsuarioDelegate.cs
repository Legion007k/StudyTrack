using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Delegates;

public interface IUsuarioDelegate
{
    /// <summary>El usuario registrado, o null si no existe.</summary>
    Task<Usuario?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Registra un usuario. Lanza ConflictException si el Id ya esta registrado.</summary>
    Task<Usuario> RegistrarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
