namespace FinTrack.Application.Features.Categories.UpdateCategory;

// Campos nulos significam "não informado" (o contrato exige ao menos um campo).
public sealed record UpdateCategoryCommand(int Id, string? Name, string? Nature);
