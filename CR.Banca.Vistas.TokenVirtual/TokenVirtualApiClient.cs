using System.Net.Http.Json;
using CR.Banca.Vistas.TokenVirtual.Models;

namespace CR.Banca.Vistas.TokenVirtual;

/// <summary>
/// Reemplaza al consumo directo de LogicaTokenVirtual (DLL): la validación/generación del OTP
/// (incluyendo el caso de token físico que antes resolvía "tieneTokenFisico") ahora vive del lado
/// del servicio REST, así que este cliente solo reenvía los datos y relaya la respuesta.
///
/// TODO: las rutas de GenerarOTP y MetodosEnvio son un supuesto razonable (simétrico a
/// ValidarOTP) porque el contrato real de esos dos endpoints no fue provisto todavía — ajustalas
/// (y los nombres de campo de los records) al contrato real del servicio.
/// </summary>
public class TokenVirtualApiClient
{
    private const string RutaValidarOtp = "api/TokenVirtual/ValidarOTP";
    private const string RutaGenerarOtp = "api/TokenVirtual/GenerarOTP";
    private const string RutaMetodosEnvio = "api/TokenVirtual/MetodosEnvio";

    private readonly HttpClient httpClient;

    public TokenVirtualApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public record ValidarOtpRemotoRequest(string CodigoOtp);

    public record ValidarOtpRemotoResponse(bool Resultado, string? MensajeResultado);

    public async Task<ValidarOtpRemotoResponse> ValidarOtpAsync(string codigoOtp, CancellationToken cancellationToken)
    {
        using var respuestaHttp = await httpClient.PostAsJsonAsync(
            RutaValidarOtp,
            new ValidarOtpRemotoRequest(codigoOtp),
            cancellationToken);

        respuestaHttp.EnsureSuccessStatusCode();

        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<ValidarOtpRemotoResponse>(
            cancellationToken: cancellationToken);

        return respuesta ?? new ValidarOtpRemotoResponse(false, null);
    }

    public record GenerarOtpRemotoRequest(string TipoEnvio);

    public record GenerarOtpRemotoResponse(bool Resultado, string? MensajeResultado);

    /// <summary>Reemplaza a LogicaTokenVirtual.generarOTP(strType, resType).</summary>
    public async Task<GenerarOtpRemotoResponse> GenerarOtpAsync(TipoEnvioOtp tipoEnvio, CancellationToken cancellationToken)
    {
        using var respuestaHttp = await httpClient.PostAsJsonAsync(
            RutaGenerarOtp,
            new GenerarOtpRemotoRequest(tipoEnvio.ToString().ToLowerInvariant()),
            cancellationToken);

        respuestaHttp.EnsureSuccessStatusCode();

        var respuesta = await respuestaHttp.Content.ReadFromJsonAsync<GenerarOtpRemotoResponse>(
            cancellationToken: cancellationToken);

        return respuesta ?? new GenerarOtpRemotoResponse(false, null);
    }

    public record MetodosEnvioOtpResponse(bool EnvioHabilitado, bool SmsHabilitado, bool EmailHabilitado);

    /// <summary>
    /// Reemplaza a LogicaTokenVirtual.sinEnvioToken() + obtenerMetodosAutControl() (antes un
    /// DataSet con filas ID/HABILITAR para SMS=1 y Email=2), consolidados acá en una sola llamada.
    /// </summary>
    public async Task<MetodosEnvioOtpResponse> ObtenerMetodosEnvioAsync(CancellationToken cancellationToken)
    {
        var respuesta = await httpClient.GetFromJsonAsync<MetodosEnvioOtpResponse>(RutaMetodosEnvio, cancellationToken);
        return respuesta ?? new MetodosEnvioOtpResponse(false, false, false);
    }
}
