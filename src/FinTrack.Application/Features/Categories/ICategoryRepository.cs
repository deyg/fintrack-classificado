using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Categories;

// Porta de persistência: implementada na Infrastructure; expõe apenas tipos de domínio.
public interface ICategoryRepository
{
    // Persiste a categoria e atribui o Id gerado.
    Task AddAsync(Category category, CancellationToken cancellationToken);

    // Lista por Id crescente; `nature` nulo significa sem filtro.
    Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken);

    // Retorna nulo quando a categoria não existe.
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
