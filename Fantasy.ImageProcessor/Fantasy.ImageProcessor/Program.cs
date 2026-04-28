using Fantasy.ImageProcessor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ImageMedalProcessor>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
    app = "Fantasy.ImageProcessor",
    endpoints = new[]
    {
        "POST /api/images/normalize-medal",
        "POST /api/images/normalize-medal?size=512"
    }
}));

app.MapPost("/api/images/normalize-medal", async (
    IFormFile file,
    ImageMedalProcessor processor,
    int size = 512,
    CancellationToken cancellationToken = default) =>
{
    if (file.Length == 0)
        return Results.BadRequest("Arquivo vazio.");

    if (size < 64 || size > 2048)
        return Results.BadRequest("O tamanho deve estar entre 64 e 2048 pixels.");

    var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(extension))
        return Results.BadRequest("Envie uma imagem PNG, JPG, JPEG ou WEBP.");

    await using var inputStream = file.OpenReadStream();
    var result = await processor.ProcessAsync(inputStream, size, cancellationToken);

    var outputName = Path.GetFileNameWithoutExtension(file.FileName) + $"-{size}x{size}.png";
    return Results.File(result, "image/png", outputName);
})
.DisableAntiforgery()
.WithName("NormalizeMedal");
//.WithOpenApi();

app.Run();
