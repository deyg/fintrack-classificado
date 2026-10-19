using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Categories;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class CategoryRepository(FinTrackDbContext context) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
    }
}
