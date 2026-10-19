using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.CreateCategory;

namespace FinTrack.Api.Endpoints.Categories;

public sealed record CreateCategoryRequest(string? Name, string? Nature);

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

        return api;
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
