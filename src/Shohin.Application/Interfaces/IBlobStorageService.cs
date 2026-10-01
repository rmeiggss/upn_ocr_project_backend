using System.IO;
using System.Threading.Tasks;

namespace Shohin.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> SubirArchivoAsync(Stream archivoStream, string nombreArchivo, string carpeta = "digitalizacion");
    Task<Stream?> DescargarArchivoAsync(string rutaBlob);
    Task<bool> EliminarArchivoAsync(string rutaBlob);
}
