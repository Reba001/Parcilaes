using Microsoft.AspNetCore.Mvc;

namespace CR.Banca.Vistas.TokenVirtual.Controllers;

/// <summary>
/// Reemplaza a btn_TokenVirtualIngresar_Click del code-behind original. Antes viajaba por un
/// postback parcial del UpdatePanel; ahora lo invoca el fetch de wwwroot/js/token-virtual.js, y la
/// validación en sí se delega al servicio REST "api/TokenVirtual/ValidarOTP" en vez de la DLL
/// LogicaTokenVirtual.
/// </summary>
[ApiController]
[Route("api/token-virtual")]
public class TokenVirtualApiController : ControllerBase
{
    private const string MensajeTokenIncorrecto = "Número de token incorrecto.";
    private const string MensajeErrorGenerico = "Ha ocurrido un error, intentelo mas tarde por favor!";

    private readonly TokenVirtualApiClient tokenVirtualApiClient;

    public TokenVirtualApiController(TokenVirtualApiClient tokenVirtualApiClient)
    {
        this.tokenVirtualApiClient = tokenVirtualApiClient;
    }

    public record ValidarOtpRequest(string? CodigoOtp);

    public record ValidarOtpResponse(bool Resultado, string? MensajeResultado);

    [HttpPost("validar")]
    public async Task<ActionResult<ValidarOtpResponse>> Validar(
        [FromBody] ValidarOtpRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.CodigoOtp))
        {
            return Ok(new ValidarOtpResponse(false, MensajeTokenIncorrecto));
        }

        try
        {
            var respuesta = await tokenVirtualApiClient.ValidarOtpAsync(request.CodigoOtp, cancellationToken);

            HttpContext.Session.EstablecerValidarToken(respuesta.Resultado);

            return Ok(new ValidarOtpResponse(
                respuesta.Resultado,
                respuesta.Resultado ? null : (respuesta.MensajeResultado ?? MensajeTokenIncorrecto)));
        }
        catch (Exception)
        {
            return Ok(new ValidarOtpResponse(false, MensajeErrorGenerico));
        }
    }
}
