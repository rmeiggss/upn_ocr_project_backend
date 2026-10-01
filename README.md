# 🚀 Backend Shohin S.A. - .NET 10 Clean Architecture

**Proyecto:** Sistema de Digitalización Inteligente y Archivo Histórico Contable  
**Curso:** Diseño y Arquitectura de Software (UPN - Ciclo 8)  
**Tecnología:** .NET 10 (C#) | ASP.NET Core Web API | Entity Framework Core 10 | Swagger OpenAPI  

---

## 🏗️ Estructura de la Solución (Alineada al Patrón BCE)

| Proyecto | Capa Arquitectónica | Patrón BCE | Responsabilidad |
|---|---|---|---|
| **`Shohin.Domain`** | Domain (Core) | **Entity (E)** | Entidades puras (`DocumentoContable`, `TicketDigitalizacion`, `Usuario`, `Parametro`, `AuditableEntity`), constantes y contratos sin dependencias externas. |
| **`Shohin.Application`** | Application (Core) | **Control (C)** | Casos de uso de los 6 CUS (`AuthService`, `DigitalizacionService`, `ValidacionService`, `ArchivoHistoricoService`, `ReporteAuditoriaService`, `ParametroService`), DTOs e interfaces. |
| **`Shohin.Infrastructure`** | Infrastructure | - | Persistencia con EF Core 10 (`ApplicationDbContext`), auditoría automática (`SaveChangesAsync`), TPH, tokens JWT y soporte dual (Azure vs Mock/Local). |
| **`Shohin.Api`** | Presentation | **Boundary (B)** | API REST con controladores expuestos para Angular/Wireframe, documentación Swagger interactiva, CORS y autenticación JWT. |
| **`Shohin.Worker.Local`** | Host de Segundo Plano | **Boundary (B)** | Servicio Windows / BackgroundService que monitorea la carpeta física de escaneo e ingesta automáticamente los comprobantes. |

---

## 🏃 Cómo Ejecutar la API

### 1. Iniciar la Web API
```powershell
cd "c:\Users\User\Documents\UPN\Ciclo 8\Arquitectura\Proyecto\backend\src\Shohin.Api"
dotnet run
```
* **Swagger UI:** `http://localhost:5000/` o `https://localhost:5001/` (abierto directamente en la raíz).
* Al arrancar por primera vez, el sistema detecta si la base de datos está vacía y ejecuta automáticamente el sembrado inicial (`ApplicationDbContextSeed.cs`) con roles, usuarios y tickets de prueba.

### 2. Iniciar el Agente On-Premise (Worker Local)
```powershell
cd "c:\Users\User\Documents\UPN\Ciclo 8\Arquitectura\Proyecto\backend\src\Shohin.Worker.Local"
dotnet run
```
* Creará automáticamente las carpetas `ScanStation/Inbox` y `ScanStation/Processed`.
* Coloque cualquier archivo `.pdf` en `ScanStation/Inbox` y el Worker generará el ticket y ejecutará la extracción OCR automáticamente.

---

## 🔑 Credenciales para Pruebas (Swagger / Login)

| Usuario | Password | Rol |
|---|---|---|
| `admin` | `Shohin2026*` | Administrador |
| `contable` | `Shohin2026*` | Contable |
| `archivo` | `Shohin2026*` | Personal de archivo |
| `sunat` | `Shohin2026*` | SUNAT (Auditor) |
