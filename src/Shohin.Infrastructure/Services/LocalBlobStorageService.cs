using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _baseStoragePath;
    private readonly string _baseUrl;

    public LocalBlobStorageService(IConfiguration configuration)
    {
        _baseStoragePath = configuration["BlobStorage:LocalPath"]
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage", "Blobs");

        _baseUrl = configuration["BlobStorage:BaseUrl"] ?? "/api/documentos/archivo/";

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> SubirArchivoAsync(Stream archivoStream, string nombreArchivo, string carpeta = "digitalizacion")
    {
        var carpetaDestino = Path.Combine(_baseStoragePath, carpeta, DateTime.UtcNow.ToString("yyyyMM"));
        if (!Directory.Exists(carpetaDestino))
        {
            Directory.CreateDirectory(carpetaDestino);
        }

        var nombreUnico = $"{Guid.NewGuid():N}_{nombreArchivo}";
        var rutaCompleta = Path.Combine(carpetaDestino, nombreUnico);

        using (var fileStream = new FileStream(rutaCompleta, FileMode.Create, FileAccess.Write))
        {
            await archivoStream.CopyToAsync(fileStream);
        }

        var rutaRelativa = Path.Combine(carpeta, DateTime.UtcNow.ToString("yyyyMM"), nombreUnico).Replace("\\", "/");
        return $"{_baseUrl}{rutaRelativa}";
    }

    public Task<Stream?> DescargarArchivoAsync(string rutaBlob)
    {
        var rutaLimpia = rutaBlob.Replace(_baseUrl, "").Replace("/", "\\");
        var rutaCompleta = Path.Combine(_baseStoragePath, rutaLimpia);

        if (!File.Exists(rutaCompleta))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> EliminarArchivoAsync(string rutaBlob)
    {
        var rutaLimpia = rutaBlob.Replace(_baseUrl, "").Replace("/", "\\");
        var rutaCompleta = Path.Combine(_baseStoragePath, rutaLimpia);

        if (File.Exists(rutaCompleta))
        {
            File.Delete(rutaCompleta);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
