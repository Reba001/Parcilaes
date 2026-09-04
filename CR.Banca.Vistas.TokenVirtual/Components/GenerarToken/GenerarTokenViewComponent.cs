using CR.Banca.Vistas.TokenVirtual.Models;
using Microsoft.AspNetCore.Mvc;

namespace CR.Banca.Vistas.TokenVirtual.Components;

/// <summary>
/// Reemplaza al UserControl "GenerarToken.ascx" (el panel colapsable para reenviar el OTP por
/// SMS/Email). De las cuatro ramas de dibujarControlGenerarToken() del code-behind original, solo
/// la rama "else" (BiEnLinea / BiBanking / Westrust_ACH) hacía algo visible de verdad: cambiar el
/// texto del botón SMS a "Generar token" (BiEnLinea_Perfil, Conexion_Regional_Login y
/// Conexion_Regional tenían el cuerpo completamente comentado). Por eso acá esa es la única
/// diferencia que se replica.
/// </summary>
public class GenerarTokenViewComponent : ViewComponent
{
    private const string ClaveSessionTipo = "uxToken_View";

    private readonly TokenVirtualApiClient tokenVirtualApiClient;

    public GenerarTokenViewComponent(TokenVirtualApiClient tokenVirtualApiClient)
    {
        this.tokenVirtualApiClient = tokenVirtualApiClient;
    }

    public async Task<IViewComponentResult> InvokeAsync(TipoVistaToken? tipo = null, string containerId = "pn_GenToken")
    {
        var tipoResuelto = tipo ?? LeerTipoDesdeSession();

        var metodos = await tokenVirtualApiClient.ObtenerMetodosEnvioAsync(HttpContext.RequestAborted);

        var model = new GenerarTokenViewModel
        {
            ContainerId = containerId,
            BotonSmsTexto = ObtenerTextoBotonSms(tipoResuelto),

            // Igual que el original: el botón de SMS se deshabilita si el envío global está
            // apagado (antes "!sinEnvioToken()") o si el método puntual del usuario no está
            // habilitado (antes la fila ID=1/HABILITAR del DataSet de obtenerMetodosAutControl).
            BotonSmsHabilitado = metodos.EnvioHabilitado && metodos.SmsHabilitado,

            // El botón de Email nunca dependía de "sinEnvioToken" en el original, solo del
            // método puntual (fila ID=2/HABILITAR).
            BotonEmailHabilitado = metodos.EmailHabilitado,

            Mensaje = metodos.EnvioHabilitado ? null : "Inhabilitado el envío de Token.",
        };

        return View("Default", model);
    }

    private TipoVistaToken LeerTipoDesdeSession()
    {
        var valor = HttpContext.Session.GetString(ClaveSessionTipo);
        return Enum.TryParse<TipoVistaToken>(valor, out var tipoSesion)
            ? tipoSesion
            : TipoVistaToken.BiEnLinea_Perfil;
    }

    private static string ObtenerTextoBotonSms(TipoVistaToken tipo) =>
        tipo is TipoVistaToken.BiEnLinea or TipoVistaToken.BiBanking or TipoVistaToken.Westrust_ACH
            ? "Generar token"
            : "Mensaje de texto";
}
