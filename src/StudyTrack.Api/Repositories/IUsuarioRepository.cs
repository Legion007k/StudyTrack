using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
