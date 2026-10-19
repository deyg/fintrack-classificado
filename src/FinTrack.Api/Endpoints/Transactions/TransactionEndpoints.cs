using FinTrack.Api.Errors;
using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.CreateTransaction;
using FinTrack.Application.Features.Transactions.DeleteTransaction;
using FinTrack.Application.Features.Transactions.GetTransaction;
using FinTrack.Application.Features.Transactions.ListAccountTransactions;
using FinTrack.Application.Features.Transactions.ListTransactions;
using FinTrack.Application.Features.Transactions.UpdateTransaction;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Endpoints.Transactions;

public sealed record CreateTransactionRequest(int? AccountId, decimal? Amount, DateOnly? RealizedAt, string? Description);

// O setter só roda quando "description" está no JSON, distinguindo null explícito de ausência.
public sealed class UpdateTransactionRequest
{
    private string? _description;

    public int? AccountId { get; set; }

    public decimal? Amount { get; set; }

    public DateOnly? RealizedAt { get; set; }

    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            DescriptionProvided = true;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool DescriptionProvided { get; private set; }
}

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder api)
    {
        var transactions = api.MapGroup("/transactions").WithTags("Transactions");

        transactions.MapGet("/", ListTransactionsAsync)
            .WithName("listTransactions")
            .Produces<TransactionPageResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        transactions.MapPost("/", CreateTransactionAsync)
            .WithName("createTransaction")
            .Produces<TransactionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        transactions.MapGet("/{id}", GetTransactionAsync)
            .WithName("getTransaction")
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        transactions.MapPatch("/{id}", UpdateTransactionAsync)
            .WithName("updateTransaction")
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        transactions.MapDelete("/{id}", DeleteTransactionAsync)
            .WithName("deleteTransaction")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/accounts/{id}/transactions", ListAccountTransactionsAsync)
            .WithTags("Accounts")
            .WithName("listAccountTransactions")
            .Produces<TransactionPageResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> ListAccountTransactionsAsync(
        int id,
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo,
        int? page,
        int? pageSize,
        HttpContext http,
        ListAccountTransactionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListAccountTransactionsQuery(id, dateFrom, dateTo, page, pageSize), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Conta {id} não encontrada.");
        }

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var errors = result.ValidationErrors!;
        return errors.ContainsKey(ListTransactionsHandler.PeriodField)
            ? ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors);
    }

    private static async Task<IResult> DeleteTransactionAsync(
        int id,
        HttpContext http,
        DeleteTransactionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteTransactionCommand(id), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Lançamento {id} não encontrado.");
        }

        return result.IsSuccess
            ? Results.NoContent()
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", result.ValidationErrors!);
    }

    private static async Task<IResult> UpdateTransactionAsync(
        int id,
        UpdateTransactionRequest request,
        HttpContext http,
        UpdateTransactionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateTransactionCommand(
                id, request.AccountId, request.Amount, request.RealizedAt, request.DescriptionProvided, request.Description),
            cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Lançamento {id} não encontrado.");
        }

        if (result.IsConflict)
        {
            return ProblemResults.Conflict(http, result.ConflictMessage!);
        }

        if (!result.IsSuccess)
        {
            var errors = result.ValidationErrors!;
            return errors.ContainsKey(UpdateTransactionHandler.IdField)
                ? ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors)
                : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetTransactionAsync(
        int id,
        HttpContext http,
        GetTransactionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTransactionQuery(id), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Lançamento {id} não encontrado.");
        }

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", result.ValidationErrors!);
    }

    private static async Task<IResult> ListTransactionsAsync(
        int? accountId,
        int? categoryId,
        string? nature,
        string? classifier,
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo,
        int? page,
        int? pageSize,
        ListTransactionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListTransactionsQuery(accountId, categoryId, nature, classifier, dateFrom, dateTo, page, pageSize),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var errors = result.ValidationErrors!;
        return errors.ContainsKey(ListTransactionsHandler.PeriodField)
            ? ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors);
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
