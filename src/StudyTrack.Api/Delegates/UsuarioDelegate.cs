using StudyTrack.Api.Delegates.Exceptions;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Api.Delegates;

public class UsuarioDelegate(IUsuarioRepository repository) : IUsuarioDelegate
{
    public Task<Usuario?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    public async Task<Usuario> RegistrarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        var nuevo = new Usuario
        {
            Id = usuario.Id.Trim(),
            Nombre = usuario.Nombre.Trim(),
            Email = usuario.Email.Trim()
        };

        if (await repository.ExisteAsync(nuevo.Id, cancellationToken))
            throw new ConflictException($"Ya existe un usuario con id '{nuevo.Id}'.");

        await repository.AddAsync(nuevo, cancellationToken);
        return nuevo;
    }
}
