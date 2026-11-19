using Microsoft.EntityFrameworkCore;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public class UsuarioRepository(ApplicationDbContext context) : IUsuarioRepository
{
    public Task<Usuario?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> ExisteAsync(string id, CancellationToken cancellationToken = default) =>
        context.Usuarios.AnyAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync(cancellationToken);
    }
}
