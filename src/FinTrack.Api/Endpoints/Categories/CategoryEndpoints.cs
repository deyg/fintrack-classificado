using FinTrack.Api.Errors;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Application.Features.Categories.DeleteCategory;
using FinTrack.Application.Features.Categories.GetCategory;
using FinTrack.Application.Features.Categories.ListCategories;
using FinTrack.Application.Features.Categories.UpdateCategory;

namespace FinTrack.Api.Endpoints.Categories;

public sealed record CreateCategoryRequest(string? Name, string? Nature);

public sealed record UpdateCategoryRequest(string? Name, string? Nature);

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        var categories = api.MapGroup("/categories").WithTags("Categories");

        categories.MapPost("/", CreateCategoryAsync)
            .WithName("createCategory")
            .Produces<CategoryResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        categories.MapGet("/", ListCategoriesAsync)
            .WithName("listCategories")
            .Produces<IReadOnlyList<CategoryResult>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        categories.MapGet("/{id}", GetCategoryAsync)
            .WithName("getCategory")
            .Produces<CategoryResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPatch("/{id}", UpdateCategoryAsync)
            .WithName("updateCategory")
            .Produces<CategoryResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        categories.MapDelete("/{id}", DeleteCategoryAsync)
            .WithName("deleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }

    private static async Task<IResult> DeleteCategoryAsync(
        int id,
        HttpContext http,
        DeleteCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteCategoryCommand(id), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Categoria {id} não encontrada.");
        }

        if (result.IsConflict)
        {
            return ProblemResults.Conflict(http, result.ConflictMessage!);
        }

        return result.IsSuccess
            ? Results.NoContent()
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", result.ValidationErrors!);
    }

    private static async Task<IResult> UpdateCategoryAsync(
        int id,
        UpdateCategoryRequest request,
        HttpContext http,
        UpdateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateCategoryCommand(id, request.Name, request.Nature), cancellationToken);

        if (result.IsNotFound)
        {
            return ProblemResults.NotFound(http, $"Categoria {id} não encontrada.");
        }

        if (result.IsConflict)
        {
            return ProblemResults.Conflict(http, result.ConflictMessage!);
        }

        if (!result.IsSuccess)
        {
            var errors = result.ValidationErrors!;
            return errors.ContainsKey(UpdateCategoryHandler.IdField)
                ? ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors)
                : ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetCategoryAsync(
        int id,
        HttpContext http,
        GetCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCategoryQuery(id), cancellationToken);

        if (result.IsNotFound)
        {
            return Results.Problem(
                title: "Not Found",
                detail: $"Categoria {id} não encontrada.",
                type: "https://httpstatuses.com/404",
                instance: http.Request.Path,
                statusCode: StatusCodes.Status404NotFound);
        }

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.ValidationProblem(
                result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value),
                title: "Bad Request",
                type: "https://httpstatuses.com/400",
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> ListCategoriesAsync(
        string? nature,
        ListCategoriesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListCategoriesQuery(nature), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.ValidationProblem(
                result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value),
                title: "Bad Request",
                type: "https://httpstatuses.com/400",
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> CreateCategoryAsync(
        CreateCategoryRequest request,
        CreateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateCategoryCommand(request.Name, request.Nature),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.ValidationProblem(
                result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value),
                title: "Unprocessable Entity",
                type: "https://httpstatuses.com/422",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var category = result.Value!;
        return Results.Created($"/api/v1/categories/{category.Id}", category);
    }
}
