namespace Shohin.Domain.Enums;

/// <summary>
/// Constantes tipadas para el catálogo Parametro (Evita estados quemados / Magic Strings).
/// </summary>
public static class ParametroConstantes
{
    public static class Grupos
    {
        public const string TipoUsuario = "TIPO_USUARIO";
        public const string TipoDocumento = "TIPO_DOCUMENTO";
        public const string EstadoTicket = "ESTADO_TICKET";
        public const string EstadoDocumento = "ESTADO_DOCUMENTO";
    }

    public static class TipoUsuario
    {
        public const string Interno = "INTERNO";
        public const string Auditor = "AUDITOR";
        public const string Admin = "ADMIN";
    }

    public static class TipoDocumento
    {
        public const string Factura = "FACTURA";
        public const string Boleta = "BOLETA";
        public const string NotaCredito = "NOTA_CREDITO";
    }

    public static class EstadoTicket
    {
        public const string Pendiente = "PENDIENTE";
        public const string Procesando = "PROCESANDO";
        public const string Procesado = "PROCESADO";
        public const string Observado = "OBSERVADO";
    }

    public static class EstadoDocumento
    {
        public const string Correcto = "CORRECTO";
        public const string Observado = "OBSERVADO";
        public const string Reprocesar = "REPROCESAR";
        public const string Ilegible = "ILEGIBLE";
    }

    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Contable = "Contable";
        public const string PersonalArchivo = "Personal de archivo";
        public const string Sunat = "SUNAT";
    }
}
