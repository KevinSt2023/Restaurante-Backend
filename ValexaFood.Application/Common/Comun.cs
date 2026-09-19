using System.Text.RegularExpressions;

namespace ValexaFood.Application.Common;

// Error de negocio con mensaje para el usuario. El middleware lo convierte en
// 4xx con { mensaje, ...datos }: nunca debe llegar al cliente como 500.
public class ReglaNegocioException : Exception
{
    public int Status { get; }
    public object? Datos { get; }

    public ReglaNegocioException(string mensaje, int status = 400, object? datos = null) : base(mensaje)
    {
        Status = status;
        Datos = datos;
    }

    public static ReglaNegocioException NoEncontrado(string que) => new($"{que} no encontrado(a).", 404);
}

// Día calendario de Lima (UTC-5, sin horario de verano). La base guarda
// instantes en UTC real; aquí se traduce "hoy" y "medianoche" de Lima.
public static class DiaLima
{
    private static readonly TimeSpan Desfase = TimeSpan.FromHours(-5);

    public static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow + Desfase);

    // Instante UTC en que empieza ese día en Lima (00:00 Lima = 05:00 UTC).
    public static DateTime Inicio(DateOnly dia) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue) - Desfase, DateTimeKind.Utc);

    public static DateOnly DeInstante(DateTime utc) => DateOnly.FromDateTime(utc + Desfase);
}

public static class Texto
{
    public static string? Limpio(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return Regex.Replace(s.Trim(), @"\s+", " ");
    }

    public static string Requerido(string? s, string campo, int max, int min = 1)
    {
        var v = Limpio(s);
        if (v == null || v.Length < min)
            throw new ReglaNegocioException(min > 1 ? $"{campo}: mínimo {min} caracteres." : $"{campo} es obligatorio.");
        if (v.Length > max) throw new ReglaNegocioException($"{campo}: máximo {max} caracteres.");
        return v;
    }

    public static string? Opcional(string? s, string campo, int max)
    {
        var v = Limpio(s);
        if (v != null && v.Length > max) throw new ReglaNegocioException($"{campo}: máximo {max} caracteres.");
        return v;
    }

    public static string Soles(decimal v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
}

public static class Documento
{
    // RUC peruano: prefijo válido + dígito verificador (módulo 11).
    public static bool RucValido(string? ruc)
    {
        if (ruc == null || !Regex.IsMatch(ruc, @"^(10|15|16|17|20)\d{9}$")) return false;
        int[] pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var suma = 0;
        for (var i = 0; i < 10; i++) suma += (ruc[i] - '0') * pesos[i];
        var dv = 11 - suma % 11;
        if (dv == 10) dv = 0;
        else if (dv == 11) dv = 1;
        return dv == ruc[10] - '0';
    }

    public static string? NormalizarRuc(string? ruc)
    {
        var v = Texto.Limpio(ruc)?.Replace(" ", "");
        if (v == null) return null;
        if (!RucValido(v)) throw new ReglaNegocioException("El RUC no es válido: revisa los 11 dígitos.");
        return v;
    }

    public static string? NormalizarDni(string? dni)
    {
        var v = Texto.Limpio(dni)?.Replace(" ", "");
        if (v == null) return null;
        if (!Regex.IsMatch(v, @"^\d{8}$")) throw new ReglaNegocioException("El DNI debe tener 8 dígitos.");
        return v;
    }

    public static string NormalizarEmail(string? email, string campo = "Correo")
    {
        var v = Texto.Requerido(email, campo, 120).ToLowerInvariant();
        if (!Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) throw new ReglaNegocioException($"{campo}: no es un correo válido.");
        return v;
    }

    public static string? NormalizarTelefono(string? tel)
    {
        var v = Texto.Limpio(tel)?.Replace(" ", "").Replace("-", "");
        if (v == null) return null;
        if (v.StartsWith("+51")) v = v[3..];
        if (!Regex.IsMatch(v, @"^\d{6,9}$")) throw new ReglaNegocioException("El teléfono debe tener entre 6 y 9 dígitos.");
        return v;
    }

    public static bool EsCelularPeru(string? tel) => tel != null && Regex.IsMatch(tel, @"^9\d{8}$");
}

public static class Clave
{
    public static void ValidarFortaleza(string? password)
    {
        if (password == null || password.Length < 8)
            throw new ReglaNegocioException("La contraseña debe tener al menos 8 caracteres.");
        if (password.Length > 72) throw new ReglaNegocioException("La contraseña admite hasta 72 caracteres.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ReglaNegocioException("La contraseña debe combinar letras y números.");
    }

    // Temporal legible para dictar por teléfono: sin 0/O ni 1/l.
    public static string GenerarTemporal()
    {
        const string letras = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
        const string digitos = "23456789";
        static int r(int max) => System.Security.Cryptography.RandomNumberGenerator.GetInt32(max);
        var chars = new char[10];
        for (var i = 0; i < 6; i++) chars[i] = letras[r(letras.Length)];
        for (var i = 6; i < 9; i++) chars[i] = digitos[r(digitos.Length)];
        chars[9] = '*';
        return new string(chars);
    }

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 11);
}
