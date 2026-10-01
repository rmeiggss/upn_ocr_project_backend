using System.Collections.Generic;

namespace Shohin.Application.DTOs.Common;

public class ApiResponse<T>
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public T? Datos { get; set; }
    public List<string>? Errores { get; set; }

    public static ApiResponse<T> Ok(T datos, string mensaje = "Operación exitosa")
    {
        return new ApiResponse<T> { Exito = true, Mensaje = mensaje, Datos = datos };
    }

    public static ApiResponse<T> Fail(string mensaje, List<string>? errores = null)
    {
        return new ApiResponse<T> { Exito = false, Mensaje = mensaje, Errores = errores };
    }
}
