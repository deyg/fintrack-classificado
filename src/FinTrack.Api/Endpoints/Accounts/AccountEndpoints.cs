using FinTrack.Api.Errors;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.CreateAccount;

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

        return api;
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
