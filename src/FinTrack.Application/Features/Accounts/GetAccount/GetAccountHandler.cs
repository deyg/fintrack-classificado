using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Accounts.GetAccount;

public sealed class GetAccountHandler(IAccountRepository repository)
{
    public async Task<Result<AccountResult>> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Id < 1)
        {
            return Result<AccountResult>.Invalid(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        var account = await repository.GetByIdAsync(query.Id, cancellationToken);

        return account is null
            ? Result<AccountResult>.NotFound()
            : Result<AccountResult>.Success(AccountResult.From(account));
    }
}
