using FinTrack.Domain.Transactions;

namespace FinTrack.Application.Features.Transactions;

// Porta de persistência: implementada na Infrastructure; expõe apenas tipos de domínio.
public interface ITransactionRepository
{
    // Persiste o lançamento e atribui o Id gerado. A conta deve existir (FK); caso contrário a operação falha.
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    // Retorna nulo quando o lançamento não existe.
    Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken);

    // Persiste alterações de um lançamento existente.
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken);

    // Remove o lançamento; retorna falso quando ele não existe mais.
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);

    // Ordenação determinística: RealizedAt decrescente e Id decrescente; page é 1-based.
    Task<TransactionPageData> ListAsync(TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken);
}
