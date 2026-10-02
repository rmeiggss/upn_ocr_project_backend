using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;
using Shohin.Domain.Enums;

namespace Shohin.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context, IJwtService jwtService)
    {
        // 1. Parametros
        if (!await context.Parametros.AnyAsync())
        {
            var parametros = new[]
            {
                new Parametro { Grupo = ParametroConstantes.Grupos.TipoUsuario, Clave = ParametroConstantes.TipoUsuario.Interno, Valor = "Personal Interno de la Organización" },
                new Parametro { Grupo = ParametroConstantes.Grupos.TipoUsuario, Clave = ParametroConstantes.TipoUsuario.Auditor, Valor = "Auditor Externo / SUNAT" },
                new Parametro { Grupo = ParametroConstantes.Grupos.TipoUsuario, Clave = ParametroConstantes.TipoUsuario.Admin, Valor = "Administrador Técnico de Plataforma" },

                new Parametro { Grupo = ParametroConstantes.Grupos.TipoDocumento, Clave = ParametroConstantes.TipoDocumento.Factura, Valor = "Factura Electrónica de Proveedor" },
                new Parametro { Grupo = ParametroConstantes.Grupos.TipoDocumento, Clave = ParametroConstantes.TipoDocumento.Boleta, Valor = "Boleta de Venta" },
                new Parametro { Grupo = ParametroConstantes.Grupos.TipoDocumento, Clave = ParametroConstantes.TipoDocumento.NotaCredito, Valor = "Nota de Crédito" },

                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoTicket, Clave = ParametroConstantes.EstadoTicket.Pendiente, Valor = "Pendiente de escaneo o recepción física" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoTicket, Clave = ParametroConstantes.EstadoTicket.Procesando, Valor = "Archivos encolados y en extracción OCR" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoTicket, Clave = ParametroConstantes.EstadoTicket.Procesado, Valor = "OCR completado y disponible para revisión" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoTicket, Clave = ParametroConstantes.EstadoTicket.Observado, Valor = "Lote observado o discrepancia en conteo físico" },

                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoDocumento, Clave = ParametroConstantes.EstadoDocumento.Pendiente, Valor = "Documento subido a la nube en espera de procesamiento OCR" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoDocumento, Clave = ParametroConstantes.EstadoDocumento.Correcto, Valor = "Documento validado y conforme con alta confianza" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoDocumento, Clave = ParametroConstantes.EstadoDocumento.Observado, Valor = "Campos con baja confianza o inconsistencia numérica" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoDocumento, Clave = ParametroConstantes.EstadoDocumento.Reprocesar, Valor = "Documento borroso/manchado devuelto a archivo" },
                new Parametro { Grupo = ParametroConstantes.Grupos.EstadoDocumento, Clave = ParametroConstantes.EstadoDocumento.Ilegible, Valor = "Original físico dañado o inservible (descarte)" }
            };

            await context.Parametros.AddRangeAsync(parametros);
            await context.SaveChangesAsync();
        }

        // 2. Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new Rol { NombreRol = ParametroConstantes.Roles.Administrador, Estado = true },
                new Rol { NombreRol = ParametroConstantes.Roles.Contable, Estado = true },
                new Rol { NombreRol = ParametroConstantes.Roles.PersonalArchivo, Estado = true },
                new Rol { NombreRol = ParametroConstantes.Roles.Sunat, Estado = true }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // 3. Usuarios de Demostración
        if (!await context.Usuarios.AnyAsync())
        {
            var rolAdmin = await context.Roles.FirstAsync(r => r.NombreRol == ParametroConstantes.Roles.Administrador);
            var rolContable = await context.Roles.FirstAsync(r => r.NombreRol == ParametroConstantes.Roles.Contable);
            var rolArchivo = await context.Roles.FirstAsync(r => r.NombreRol == ParametroConstantes.Roles.PersonalArchivo);
            var rolSunat = await context.Roles.FirstAsync(r => r.NombreRol == ParametroConstantes.Roles.Sunat);

            var tipoAdmin = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoUsuario && p.Clave == ParametroConstantes.TipoUsuario.Admin);
            var tipoInterno = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoUsuario && p.Clave == ParametroConstantes.TipoUsuario.Interno);
            var tipoAuditor = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoUsuario && p.Clave == ParametroConstantes.TipoUsuario.Auditor);

            var passHash = jwtService.HashPassword("Shohin2026*");

            var usuarios = new[]
            {
                new Usuario { CodigoUsuario = "admin", PasswordHash = passHash, Nombres = "Carlos Administrador", Correo = "admin@shohin.com.pe", IdRol = rolAdmin.IdRol, IdParametroTipoUsuario = tipoAdmin.IdParametro, Estado = true },
                new Usuario { CodigoUsuario = "contable", PasswordHash = passHash, Nombres = "María Contadora Senior", Correo = "mcontable@shohin.com.pe", IdRol = rolContable.IdRol, IdParametroTipoUsuario = tipoInterno.IdParametro, Estado = true },
                new Usuario { CodigoUsuario = "archivo", PasswordHash = passHash, Nombres = "Jorge Personal de Archivo", Correo = "jarchivo@shohin.com.pe", IdRol = rolArchivo.IdRol, IdParametroTipoUsuario = tipoInterno.IdParametro, Estado = true },
                new Usuario { CodigoUsuario = "sunat", PasswordHash = passHash, Nombres = "Fiscalizador SUNAT Perú", Correo = "auditor.sunat@sunat.gob.pe", IdRol = rolSunat.IdRol, IdParametroTipoUsuario = tipoAuditor.IdParametro, Estado = true }
            };

            await context.Usuarios.AddRangeAsync(usuarios);
            await context.SaveChangesAsync();
        }

        // 4. Tickets de Demostración
        if (!await context.Tickets.AnyAsync())
        {
            var estadoPendiente = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Pendiente);
            var estadoProcesado = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Procesado);
            var estadoObservado = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Observado);

            var t1 = new TicketDigitalizacion { CodigoTicket = "TCK-2026-0001", IdEstadoParametro = estadoProcesado.IdParametro, TotalDocumentosEsperados = 2, TotalDocumentosProcesados = 2, Observaciones = "Lote 01 - Facturas Proveedores Lima Setiembre" };
            var t2 = new TicketDigitalizacion { CodigoTicket = "TCK-2026-0002", IdEstadoParametro = estadoPendiente.IdParametro, TotalDocumentosEsperados = 15, TotalDocumentosProcesados = 0, Observaciones = "Lote 02 - Solicitud de Búsqueda Física Almacén Callao" };
            var t3 = new TicketDigitalizacion { CodigoTicket = "TCK-2026-0003", IdEstadoParametro = estadoObservado.IdParametro, TotalDocumentosEsperados = 5, TotalDocumentosProcesados = 5, Observaciones = "Lote 03 - Conteo reportó más de 100 folios físicos" };

            await context.Tickets.AddRangeAsync(t1, t2, t3);
            await context.SaveChangesAsync();

            // 5. Documentos iniciales
            var tipoFactura = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ParametroConstantes.TipoDocumento.Factura);
            var docCorrecto = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == ParametroConstantes.EstadoDocumento.Correcto);
            var docObservado = await context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == ParametroConstantes.EstadoDocumento.Observado);

            var d1 = new DocumentoContable
            {
                IdTicket = t1.IdTicket,
                IdParametroTipoDocumento = tipoFactura.IdParametro,
                RucEmisor = "20512345678",
                RazonSocial = "DISTRIBUIDORA FERRETERA DEL SUR S.A.C.",
                SerieComprobante = "F001",
                NumeroComprobante = "00045210",
                FechaEmision = DateTime.UtcNow.AddDays(-15),
                MontoSubTotal = 1000.00m,
                MontoIgv = 180.00m,
                MontoTotal = 1180.00m,
                Moneda = "PEN",
                IdEstadoParametro = docCorrecto.IdParametro,
                RutaBlobStorage = "https://stshohinprod.blob.core.windows.net/digitalizacion/2026/09/factura_f001_45210.pdf",
                NombreArchivo = "factura_f001_45210.pdf",
                HashIntegridad = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
            };

            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "RucEmisor", ValorExtraido = "20512345678", NivelConfianza = 99.80m });
            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "RazonSocial", ValorExtraido = "DISTRIBUIDORA FERRETERA DEL SUR S.A.C.", NivelConfianza = 98.40m });
            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "FechaEmision", ValorExtraido = DateTime.UtcNow.AddDays(-15).ToString("yyyy-MM-dd"), NivelConfianza = 97.90m });
            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoSubTotal", ValorExtraido = "1000.00", NivelConfianza = 99.10m });
            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoIgv", ValorExtraido = "180.00", NivelConfianza = 98.50m });
            d1.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoTotal", ValorExtraido = "1180.00", NivelConfianza = 99.90m });

            var d2 = new DocumentoContable
            {
                IdTicket = t3.IdTicket,
                IdParametroTipoDocumento = tipoFactura.IdParametro,
                RucEmisor = "20498765432",
                RazonSocial = "SERVICIOS LOGISTICOS GLOBALES S.A.",
                SerieComprobante = "F003",
                NumeroComprobante = "00012890",
                FechaEmision = DateTime.UtcNow.AddDays(-10),
                MontoSubTotal = 2500.00m,
                MontoIgv = 350.00m,
                MontoTotal = 2950.00m,
                Moneda = "PEN",
                IdEstadoParametro = docObservado.IdParametro,
                RutaBlobStorage = "https://stshohinprod.blob.core.windows.net/digitalizacion/2026/09/factura_f003_12890.pdf",
                NombreArchivo = "factura_f003_12890.pdf",
                HashIntegridad = "4a5d3c87e492b67891fa3023e4d82b1c98034afc28903e1a8b27c3e4f5a6b7c8"
            };

            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "RucEmisor", ValorExtraido = "20498765432", NivelConfianza = 96.20m });
            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "RazonSocial", ValorExtraido = "SERVICIOS LOGISTICOS GLOBALES S.A.", NivelConfianza = 95.00m });
            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "FechaEmision", ValorExtraido = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd"), NivelConfianza = 92.00m });
            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoSubTotal", ValorExtraido = "2500.00", NivelConfianza = 88.00m });
            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoIgv", ValorExtraido = "350.00", NivelConfianza = 65.40m }); // Disparador CUS-05
            d2.CamposExtraidos.Add(new CampoExtraidoOCR { NombreCampo = "MontoTotal", ValorExtraido = "2950.00", NivelConfianza = 94.10m });

            await context.Documentos.AddRangeAsync(d1, d2);
            await context.SaveChangesAsync();
        }
    }
}
