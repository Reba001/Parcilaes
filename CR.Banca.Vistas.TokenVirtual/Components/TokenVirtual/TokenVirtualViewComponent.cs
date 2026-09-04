using CR.Banca.Vistas.TokenVirtual.Models;
using Microsoft.AspNetCore.Mvc;

namespace CR.Banca.Vistas.TokenVirtual.Components;

/// <summary>
/// Reemplaza al UserControl "TokenVirtual.ascx". El antiguo Page_Load leía Session["uxToken_View"]
/// y llamaba a uno de los métodos dibujar_uxToken_*; acá el "tipo" se puede pasar explícito como
/// parámetro (recomendado) o, si se omite, se sigue leyendo de Session para no romper a las
/// páginas que ya seteaban esa variable antes de renderizar el control.
///
/// De los seis layouts originales, dibujar_uxToken_BiEnLinea y dibujar_uxToken_WestrustACH tenían
/// el cuerpo comentado y dibujar_uxToken_BiBanking estaba vacío: en la práctica esos tres tipos
/// nunca dibujaban nada distinto de los controles base del .ascx. Por eso acá los tres mapean a la
/// vista "_Default" (igual que BiEnLinea_Perfil); Conexion_Regional_Login y Conexion_Regional eran
/// literalmente el mismo método duplicado, así que comparten "_ConexionRegional".
/// </summary>
public class TokenVirtualViewComponent : ViewComponent
{
    private const string ClaveSessionTipo = "uxToken_View";

    public IViewComponentResult Invoke(
        TipoVistaToken? tipo = null,
        string? onSuccessCallback = null,
        string? onCancelCallback = null,
        string? validarUrl = null,
        string containerId = "pn_Token")
    {
        var tipoResuelto = tipo ?? LeerTipoDesdeSession();

        var model = new TokenVirtualViewModel
        {
            Tipo = tipoResuelto,
            ContainerId = containerId,
            OnSuccessCallback = onSuccessCallback,
            OnCancelCallback = onCancelCallback,
        };

        if (!string.IsNullOrWhiteSpace(validarUrl))
        {
            model.ValidarUrl = validarUrl;
        }

        ConfigurarPorTipo(model);

        var nombreVista = tipoResuelto is TipoVistaToken.Conexion_Regional_Login or TipoVistaToken.Conexion_Regional
            ? "_ConexionRegional"
            : "_Default";

        return View(nombreVista, model);
    }

    private TipoVistaToken LeerTipoDesdeSession()
    {
        var valor = HttpContext.Session.GetString(ClaveSessionTipo);
        return Enum.TryParse<TipoVistaToken>(valor, out var tipoSesion)
            ? tipoSesion
            : TipoVistaToken.BiEnLinea_Perfil;
    }

    private static void ConfigurarPorTipo(TokenVirtualViewModel model)
    {
        switch (model.Tipo)
        {
            case TipoVistaToken.Conexion_Regional_Login:
            case TipoVistaToken.Conexion_Regional:
                model.MostrarEtiquetaIngresarToken = false;
                model.InputCssClass = "campoTexto margenCampoBottonToken";
                model.BotonIngresarCssClass = "botonEnviar";
                model.BotonIngresarTexto = "Ingresar";
                model.ErrorCssClass = "textoMensajes";
                model.ImagenTokenUrl = "media/imagenes/pasoUno/tokenImagen.png";
                break;
            default:
                // BiEnLinea_Perfil / BiEnLinea / BiBanking / Westrust_ACH: estilos base del .ascx.
                break;
        }
    }
}
