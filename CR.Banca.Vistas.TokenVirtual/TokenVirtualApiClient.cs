using System.Net.Http.Json;

namespace CR.Banca.Vistas.TokenVirtual;

/// <summary>
/// Reemplaza al consumo directo de LogicaTokenVirtual (DLL): la validación del OTP (incluyendo el
/// caso de token físico que antes resolvía "tieneTokenFisico") ahora vive del lado del servicio
/// REST, así que este cliente solo reenvía el código y relaya la respuesta.
/// </summary>
public class TokenVirtualApiClient
{
    private const string RutaValidarOtp = "api/TokenVirtual/ValidarOTP";

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
}
