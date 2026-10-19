using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Categories;

// Porta de persistência: implementada na Infrastructure; expõe apenas tipos de domínio.
public interface ICategoryRepository
{
    // Persiste a categoria e atribui o Id gerado.
    Task AddAsync(Category category, CancellationToken cancellationToken);
}
