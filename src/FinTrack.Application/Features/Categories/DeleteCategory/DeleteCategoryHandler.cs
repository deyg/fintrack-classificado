using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler(ICategoryRepository repository)
{
    public async Task<Result<bool>> HandleAsync(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Result<bool>.Invalid(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        if (await repository.GetByIdAsync(command.Id, cancellationToken) is null)
        {
            return Result<bool>.NotFound();
        }

        // RN10: sem exclusão em cascata; categoria com contas vinculadas não pode ser excluída.
        if (await repository.HasAccountsAsync(command.Id, cancellationToken))
        {
            return Result<bool>.Conflict("A categoria possui contas vinculadas e não pode ser excluída.");
        }

        return await repository.DeleteAsync(command.Id, cancellationToken)
            ? Result<bool>.Success(true)
            : Result<bool>.NotFound();
    }
}
