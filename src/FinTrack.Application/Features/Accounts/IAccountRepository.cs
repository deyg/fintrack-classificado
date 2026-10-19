using FinTrack.Domain.Accounts;

namespace FinTrack.Application.Features.Accounts;

// Porta de persistência: implementada na Infrastructure; expõe apenas tipos de domínio.
public interface IAccountRepository
{
    // Persiste a conta e atribui o Id gerado. A categoria deve existir (FK); caso contrário a operação falha.
    Task AddAsync(Account account, CancellationToken cancellationToken);

    // Retorna nulo quando a conta não existe.
    Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken);

    // Filtros opcionais combinados com AND; ordenação determinística por Id crescente.
    Task<IReadOnlyList<Account>> ListAsync(
        int? categoryId,
        bool? active,
        ExpenseClassifier? classifier,
        CancellationToken cancellationToken);
}
