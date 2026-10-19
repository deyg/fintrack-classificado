using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class CategoryRepository(FinTrackDbContext context) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken)
    {
        var query = context.Categories.AsNoTracking();
        if (nature is { } filter)
        {
            query = query.Where(category => category.Nature == filter);
        }

        return await query.OrderBy(category => category.Id).ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.Categories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
}
