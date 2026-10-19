using FinTrack.Api.Errors;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.CreateAccount;
using FinTrack.Application.Features.Accounts.GetAccount;
using FinTrack.Application.Features.Accounts.ListAccounts;

namespace FinTrack.Api.Endpoints.Accounts;

public sealed record CreateAccountRequest(int? CategoryId, string? Name, string? Classifier);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder api)
    {
        var accounts = api.MapGroup("/accounts").WithTags("Accounts");

        accounts.MapPost("/", CreateAccountAsync)
            .WithName("createAccount")
            .Produces<AccountResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        accounts.MapGet("/", ListAccountsAsync)
            .WithName("listAccounts")
            .Produces<IReadOnlyList<AccountResult>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        accounts.MapGet("/{id}", GetAccountAsync)
            .WithName("getAccount")
            .Produces<AccountResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> GetAccountAsync(
        int id,
        HttpContext http,
        GetAccountHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountQuery(id), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Conta {id} não encontrada.");
        }

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", result.ValidationErrors!);
    }

    private static async Task<IResult> ListAccountsAsync(
        int? categoryId,
        bool? active,
        string? classifier,
        ListAccountsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListAccountsQuery(categoryId, active, classifier), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", result.ValidationErrors!);
    }

    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        CreateAccountHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateAccountCommand(request.CategoryId, request.Name, request.Classifier),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/accounts/{result.Value!.Id}", result.Value)
            : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", result.ValidationErrors!);
    }
}
