using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.Interfaces;
using Shohin.Domain.Common;
using Shohin.Domain.Entities;

namespace Shohin.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TicketDigitalizacion> Tickets => Set<TicketDigitalizacion>();
    public DbSet<RevisionTicket> Revisiones => Set<RevisionTicket>();
    public DbSet<DocumentoContable> Documentos => Set<DocumentoContable>();
    public DbSet<CampoExtraidoOCR> CamposOCR => Set<CampoExtraidoOCR>();
    public DbSet<RegistroReporte> Reportes => Set<RegistroReporte>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)

    {
        var entries = ChangeTracker.Entries<AuditableEntity>();
        var currentUsername = _currentUserService.GetCurrentUsername() ?? "SYSTEM";
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.FechaCreacion = now;
                entry.Entity.UsuarioCreacion = string.IsNullOrWhiteSpace(entry.Entity.UsuarioCreacion) || entry.Entity.UsuarioCreacion == "SYSTEM"
                    ? currentUsername
                    : entry.Entity.UsuarioCreacion;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.FechaModificacion = now;
                entry.Entity.UsuarioModificacion = currentUsername;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Parametro
        modelBuilder.Entity<Parametro>(entity =>
        {
            entity.ToTable("Parametro");
            entity.HasKey(e => e.IdParametro);
            entity.Property(e => e.Grupo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Clave).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Valor).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => new { e.Grupo, e.Clave }).IsUnique();
        });

        // 2. Rol
        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Rol");
            entity.HasKey(e => e.IdRol);
            entity.Property(e => e.NombreRol).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.NombreRol).IsUnique();
        });

        // 3. Usuario (Herencia TPH)
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuario");
            entity.HasKey(e => e.IdUsuario);
            entity.Property(e => e.CodigoUsuario).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Nombres).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Correo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Area).HasMaxLength(100);
            entity.Property(e => e.NumeroFotocheck).HasMaxLength(50);
            entity.Property(e => e.EntidadGubernamental).HasMaxLength(100);
            entity.Property(e => e.CodigoAuditor).HasMaxLength(50);
            entity.HasIndex(e => e.CodigoUsuario).IsUnique();

            entity.HasOne(e => e.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(e => e.IdRol)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TipoUsuarioParametro)
                .WithMany(p => p.Usuarios)
                .HasForeignKey(e => e.IdParametroTipoUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. TicketDigitalizacion
        modelBuilder.Entity<TicketDigitalizacion>(entity =>
        {
            entity.ToTable("TicketDigitalizacion");
            entity.HasKey(e => e.IdTicket);
            entity.Property(e => e.CodigoTicket).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RucProveedor).HasMaxLength(20);
            entity.Property(e => e.RazonSocialProveedor).HasMaxLength(200);
            entity.Property(e => e.NumeroCajaArchivador).HasMaxLength(50);
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.HasIndex(e => e.CodigoTicket).IsUnique();

            entity.HasOne(e => e.EstadoParametro)
                .WithMany(p => p.Tickets)
                .HasForeignKey(e => e.IdEstadoParametro)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PrioridadParametro)
                .WithMany()
                .HasForeignKey(e => e.IdPrioridadParametro)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TipoDocumentoParametro)
                .WithMany()
                .HasForeignKey(e => e.IdTipoDocumentoParametro)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. RevisionTicket
        modelBuilder.Entity<RevisionTicket>(entity =>
        {
            entity.ToTable("RevisionTicket");
            entity.HasKey(e => e.IdRevision);
            entity.Property(e => e.ResultadoAprobacion).HasMaxLength(50);
            entity.Property(e => e.Observaciones).HasMaxLength(500);

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Revisiones)
                .HasForeignKey(e => e.IdTicket)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Revisiones)
                .HasForeignKey(e => e.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. DocumentoContable (Herencia TPH)
        modelBuilder.Entity<DocumentoContable>(entity =>
        {
            entity.ToTable("DocumentoContable");
            entity.HasKey(e => e.IdDocumento);
            entity.Property(e => e.RucEmisor).HasMaxLength(20);
            entity.Property(e => e.RazonSocial).HasMaxLength(200);
            entity.Property(e => e.SerieComprobante).HasMaxLength(20);
            entity.Property(e => e.NumeroComprobante).HasMaxLength(30);
            entity.Property(e => e.MontoSubTotal).HasPrecision(18, 2);
            entity.Property(e => e.MontoIgv).HasPrecision(18, 2);
            entity.Property(e => e.MontoTotal).HasPrecision(18, 2);
            entity.Property(e => e.Moneda).HasMaxLength(10).HasDefaultValue("PEN");
            entity.Property(e => e.RutaBlobStorage).HasMaxLength(500);
            entity.Property(e => e.NombreArchivo).HasMaxLength(255);
            entity.Property(e => e.HashIntegridad).HasMaxLength(64);
            entity.Property(e => e.NumeroFacturaReferencia).HasMaxLength(50);
            entity.Property(e => e.MotivoAnulacion).HasMaxLength(250);
            entity.Property(e => e.ScoreConfianza).HasPrecision(5, 2);

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Documentos)
                .HasForeignKey(e => e.IdTicket)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TipoDocumentoParametro)
                .WithMany(p => p.DocumentosPorTipo)
                .HasForeignKey(e => e.IdParametroTipoDocumento)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoParametro)
                .WithMany(p => p.DocumentosPorEstado)
                .HasForeignKey(e => e.IdEstadoParametro)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. CampoExtraidoOCR (Composición con Delete Cascade)
        modelBuilder.Entity<CampoExtraidoOCR>(entity =>
        {
            entity.ToTable("CampoExtraidoOCR");
            entity.HasKey(e => e.IdCampoExtraido);
            entity.Property(e => e.NombreCampo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ValorExtraido).HasMaxLength(255);
            entity.Property(e => e.ValorCorregido).HasMaxLength(255);
            entity.Property(e => e.NivelConfianza).HasPrecision(5, 2);

            entity.HasOne(e => e.Documento)
                .WithMany(d => d.CamposExtraidos)
                .HasForeignKey(e => e.IdDocumento)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 8. RegistroReporte (CUS-04 Auditoría de Reportes)
        modelBuilder.Entity<RegistroReporte>(entity =>
        {
            entity.ToTable("RegistroReporte");
            entity.HasKey(e => e.IdReporte);
            entity.Property(e => e.FormatoArchivo).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FiltroUsuario).HasMaxLength(500);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Reportes)
                .HasForeignKey(e => e.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }
}
