using CR.Banca.Vistas.TokenVirtual.Models;
using Microsoft.AspNetCore.Mvc;

namespace CR.Banca.Vistas.TokenVirtual.Controllers;

/// <summary>
/// Reemplaza a btn_GenerarSMS_Click / btn_GenerarEmail_Click del code-behind original. Ambos
/// hacían lo mismo salvo por el String_Type (sms/email) que le pasaban a generarOTP, por eso acá
/// es un único endpoint parametrizado en vez de dos.
/// </summary>
[ApiController]
[Route("api/generar-token")]
public class GenerarTokenApiController : ControllerBase
{
    private const string MensajeErrorGenerico = "Ha ocurrido un error, intentelo mas tarde por favor!";

    private readonly TokenVirtualApiClient tokenVirtualApiClient;

    public GenerarTokenApiController(TokenVirtualApiClient tokenVirtualApiClient)
    {
        this.tokenVirtualApiClient = tokenVirtualApiClient;
    }

    public record GenerarOtpRequest(string? TipoEnvio);

    public record GenerarOtpResponse(bool Resultado, string? MensajeResultado);

    [HttpPost("generar")]
    public async Task<ActionResult<GenerarOtpResponse>> Generar(
        [FromBody] GenerarOtpRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TipoEnvioOtp>(request?.TipoEnvio, ignoreCase: true, out var tipoEnvio))
        {
            return BadRequest();
        }

        try
        {
            var respuesta = await tokenVirtualApiClient.GenerarOtpAsync(tipoEnvio, cancellationToken);
            return Ok(new GenerarOtpResponse(respuesta.Resultado, respuesta.MensajeResultado));
        }
        catch (Exception)
        {
            return Ok(new GenerarOtpResponse(false, MensajeErrorGenerico));
        }
    }
}
