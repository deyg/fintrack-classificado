using FinTrack.Api.Errors;
using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.CreateTransaction;

namespace FinTrack.Api.Endpoints.Transactions;

public sealed record CreateTransactionRequest(int? AccountId, decimal? Amount, DateOnly? RealizedAt, string? Description);

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder api)
    {
        var transactions = api.MapGroup("/transactions").WithTags("Transactions");

        transactions.MapPost("/", CreateTransactionAsync)
            .WithName("createTransaction")
            .Produces<TransactionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> CreateTransactionAsync(
        CreateTransactionRequest request,
        HttpContext http,
        CreateTransactionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateTransactionCommand(request.AccountId, request.Amount, request.RealizedAt, request.Description),
            cancellationToken);

        if (result.IsConflict)
        {
            return ProblemResults.Conflict(http, result.ConflictMessage!);
        }

        return result.IsSuccess
            ? Results.Created($"/api/v1/transactions/{result.Value!.Id}", result.Value)
            : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", result.ValidationErrors!);
    }
}
