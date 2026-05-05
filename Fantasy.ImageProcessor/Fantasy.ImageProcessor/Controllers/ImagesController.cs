using Fantasy.ImageProcessor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fantasy.ImageProcessor.Controllers;

public class ImagesController : Controller
{
    private readonly ImageMedalProcessor _processor;

    public ImagesController(ImageMedalProcessor processor)
    {
        _processor = processor;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [RequestSizeLimit(104857600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            ViewBag.Error = "Selecione pelo menos uma imagem.";
            return View("Index");
        }

        var zipBytes = await _processor.NormalizeManyToZipAsync(files);

        return File(
            zipBytes,
            "application/zip",
            "medalhas-normalizadas.zip");
    }
    [HttpPost]
    [RequestSizeLimit(524288000)] // 500MB
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<IActionResult> Compress(
    List<IFormFile> files,
    int maxWidth = 1024,
    int quality = 75,
    string format = "webp",
    CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            ViewBag.Error = "Selecione pelo menos uma imagem.";
            return View("Index");
        }

        if (quality < 1 || quality > 100)
        {
            ViewBag.Error = "A qualidade deve estar entre 1 e 100.";
            return View("Index");
        }

        if (maxWidth < 100 || maxWidth > 4096)
        {
            ViewBag.Error = "A largura máxima deve estar entre 100 e 4096.";
            return View("Index");
        }

        var zipBytes = await _processor.CompressManyToZipAsync(
            files,
            maxWidth,
            quality,
            format,
            cancellationToken);

        return File(
            zipBytes,
            "application/zip",
            "imagens-comprimidas.zip");
    }
}