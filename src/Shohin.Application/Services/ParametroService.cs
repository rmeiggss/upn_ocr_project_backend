using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Reportes;
using Shohin.Application.Interfaces;

namespace Shohin.Application.Services;

public class ParametroService
{
    private readonly IApplicationDbContext _context;

    public ParametroService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<ParametroDto>>> ObtenerPorGrupoAsync(string grupo)
    {
        var paramsList = await _context.Parametros
            .Where(p => p.Grupo == grupo)
            .OrderBy(p => p.Clave)
            .Select(p => new ParametroDto
            {
                IdParametro = p.IdParametro,
                Grupo = p.Grupo,
                Clave = p.Clave,
                Valor = p.Valor
            })
            .ToListAsync();

        return ApiResponse<List<ParametroDto>>.Ok(paramsList);
    }

    public async Task<ApiResponse<List<ParametroDto>>> ObtenerTodosAsync()
    {
        var paramsList = await _context.Parametros
            .OrderBy(p => p.Grupo).ThenBy(p => p.Clave)
            .Select(p => new ParametroDto
            {
                IdParametro = p.IdParametro,
                Grupo = p.Grupo,
                Clave = p.Clave,
                Valor = p.Valor
            })
            .ToListAsync();

        return ApiResponse<List<ParametroDto>>.Ok(paramsList);
    }
}
