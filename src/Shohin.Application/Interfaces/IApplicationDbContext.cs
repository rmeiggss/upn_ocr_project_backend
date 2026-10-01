using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Domain.Entities;

namespace Shohin.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Parametro> Parametros { get; }
    DbSet<Rol> Roles { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<TicketDigitalizacion> Tickets { get; }
    DbSet<RevisionTicket> Revisiones { get; }
    DbSet<DocumentoContable> Documentos { get; }
    DbSet<CampoExtraidoOCR> CamposOCR { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
