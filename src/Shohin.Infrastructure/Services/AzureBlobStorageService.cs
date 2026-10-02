using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _defaultContainer;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["BlobStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("No se encontró la cadena de conexión de Azure Storage.");

        _blobServiceClient = new BlobServiceClient(connectionString);
        _defaultContainer = configuration["BlobStorage:ContainerName"] ?? "digitalizacion";
    }

    public async Task<string> SubirArchivoAsync(Stream archivoStream, string nombreArchivo, string carpeta = "digitalizacion")
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobPath = $"{carpeta.Trim('/')}/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}_{nombreArchivo}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        archivoStream.Position = 0;
        await blobClient.UploadAsync(archivoStream, new BlobHttpHeaders { ContentType = "application/pdf" });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream?> DescargarArchivoAsync(string rutaBlob)
    {
        try
        {
            var uri = new Uri(rutaBlob);
            var blobClient = new BlobClient(uri);

            if (!await blobClient.ExistsAsync())
            {
                // Intentar resolver relativo al defaultContainer
                var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
                var relativeBlobClient = containerClient.GetBlobClient(rutaBlob.Trim('/'));
                if (await relativeBlobClient.ExistsAsync())
                {
                    var response = await relativeBlobClient.DownloadStreamingAsync();
                    return response.Value.Content;
                }
                return null;
            }

            var downloadResponse = await blobClient.DownloadStreamingAsync();
            return downloadResponse.Value.Content;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> EliminarArchivoAsync(string rutaBlob)
    {
        try
        {
            var uri = new Uri(rutaBlob);
            var blobClient = new BlobClient(uri);
            var result = await blobClient.DeleteIfExistsAsync();
            return result.Value;
        }
        catch
        {
            return false;
        }
    }
}
