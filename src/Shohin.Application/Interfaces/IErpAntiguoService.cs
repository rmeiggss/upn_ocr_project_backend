using System.Collections.Generic;
using System.Threading.Tasks;
using Shohin.Application.DTOs.Documentos;

namespace Shohin.Application.Interfaces;

public interface IErpAntiguoService
{
    Task<List<DocumentoDto>> ConsultarDocumentosLegacyAsync(string? rucEmisor, string? serieNumero, string? tipoDocumento);
}
