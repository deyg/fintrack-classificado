using FinTrack.Api.Errors;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.ChangeAccountStatus;
using FinTrack.Application.Features.Accounts.CreateAccount;
using FinTrack.Application.Features.Accounts.GetAccount;
using FinTrack.Application.Features.Accounts.ListAccounts;
using FinTrack.Application.Features.Accounts.UpdateAccount;

namespace FinTrack.Api.Endpoints.Accounts;

public sealed record CreateAccountRequest(int? CategoryId, string? Name, string? Classifier);

public sealed record ChangeAccountStatusRequest(bool? Active);

// O setter só roda quando "classifier" está no JSON, distinguindo null explícito de ausência.
public sealed class UpdateAccountRequest
{
    private string? _classifier;

    public int? CategoryId { get; set; }

    public string? Name { get; set; }

    public string? Classifier
    {
        get => _classifier;
        set
        {
            _classifier = value;
            ClassifierProvided = true;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool ClassifierProvided { get; private set; }
}

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

        accounts.MapPatch("/{id}", UpdateAccountAsync)
            .WithName("updateAccount")
            .Produces<AccountResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        accounts.MapPatch("/{id}/status", ChangeAccountStatusAsync)
            .WithName("changeAccountStatus")
            .Produces<AccountResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> ChangeAccountStatusAsync(
        int id,
        ChangeAccountStatusRequest request,
        HttpContext http,
        ChangeAccountStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ChangeAccountStatusCommand(id, request.Active), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Conta {id} não encontrada.");
        }

        if (!result.IsSuccess)
        {
            var errors = result.ValidationErrors!;
            return errors.ContainsKey(ChangeAccountStatusHandler.IdField)
                ? ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors)
                : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateAccountAsync(
        int id,
        UpdateAccountRequest request,
        HttpContext http,
        UpdateAccountHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateAccountCommand(id, request.CategoryId, request.Name, request.ClassifierProvided, request.Classifier),
            cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Conta {id} não encontrada.");
        }

        if (result.IsConflict)
        {
            return ProblemResults.Conflict(http, result.ConflictMessage!);
        }

        if (!result.IsSuccess)
        {
            var errors = result.ValidationErrors!;
            return errors.ContainsKey(UpdateAccountHandler.IdField)
                ? ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors)
                : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors);
        }

        return Results.Ok(result.Value);
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
