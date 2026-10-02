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
            BlobClient blobClient;
            if (Uri.TryCreate(rutaBlob, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.Segments;
                if (segments.Length >= 2)
                {
                    var containerName = segments[1].Trim('/');
                    var blobName = string.Join("", segments.Skip(2));
                    var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                    blobClient = containerClient.GetBlobClient(blobName);
                }
                else
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
                    blobClient = containerClient.GetBlobClient(rutaBlob.Trim('/'));
                }
            }
            else
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
                blobClient = containerClient.GetBlobClient(rutaBlob.Trim('/'));
            }

            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadStreamingAsync();
                return response.Value.Content;
            }
            return null;
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
            BlobClient blobClient;
            if (Uri.TryCreate(rutaBlob, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.Segments;
                if (segments.Length >= 2)
                {
                    var containerName = segments[1].Trim('/');
                    var blobName = string.Join("", segments.Skip(2));
                    var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                    blobClient = containerClient.GetBlobClient(blobName);
                }
                else
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
                    blobClient = containerClient.GetBlobClient(rutaBlob.Trim('/'));
                }
            }
            else
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_defaultContainer);
                blobClient = containerClient.GetBlobClient(rutaBlob.Trim('/'));
            }

            var result = await blobClient.DeleteIfExistsAsync();
            return result.Value;
        }
        catch
        {
            return false;
        }
    }
}
