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
}