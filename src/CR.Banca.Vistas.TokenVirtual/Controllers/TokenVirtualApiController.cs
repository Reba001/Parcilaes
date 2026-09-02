using CR.Banca.Modelos.Token.TokenVirtual;
using Microsoft.AspNetCore.Mvc;

namespace CR.Banca.Vistas.TokenVirtual.Controllers;

/// <summary>
/// Reemplaza a btn_TokenVirtualIngresar_Click del code-behind original. Antes viajaba por un
/// postback parcial del UpdatePanel; ahora lo invoca el fetch de wwwroot/js/token-virtual.js.
/// </summary>
[ApiController]
[Route("api/token-virtual")]
public class TokenVirtualApiController : ControllerBase
{
    private const string MensajeTokenIncorrecto = "Número de token incorrecto.";
    private const string MensajeErrorGenerico = "Ha ocurrido un error, intentelo mas tarde por favor!";

    private readonly LogicaTokenVirtual logicaToken;

    public TokenVirtualApiController(LogicaTokenVirtual logicaToken)
    {
        this.logicaToken = logicaToken;
    }

    public record ValidarOtpRequest(string? CodigoOtp);

    public record ValidarOtpResponse(bool Resultado, string? MensajeResultado);

    [HttpPost("validar")]
    public ActionResult<ValidarOtpResponse> Validar([FromBody] ValidarOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.CodigoOtp))
        {
            return Ok(new ValidarOtpResponse(false, MensajeTokenIncorrecto));
        }

        try
        {
            // [tokensms][codigo para que no valide con DEVEL los tokens fisicos]
            if (logicaToken.tieneTokenFisico())
            {
                HttpContext.Session.EstablecerValidarToken(true);
                return Ok(new ValidarOtpResponse(true, null));
            }

            Respuesta<bool> respuesta = logicaToken.validarOTP(request.CodigoOtp, Result_Type.code);

            HttpContext.Session.EstablecerValidarToken(respuesta.Resultado);

            return Ok(new ValidarOtpResponse(
                respuesta.Resultado,
                respuesta.Resultado ? null : (respuesta.MensajeResultado ?? MensajeTokenIncorrecto)));
        }
        catch
        {
            return Ok(new ValidarOtpResponse(false, MensajeErrorGenerico));
        }
    }
}
