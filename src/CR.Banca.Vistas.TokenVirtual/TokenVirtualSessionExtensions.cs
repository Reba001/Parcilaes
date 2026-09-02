using Microsoft.AspNetCore.Http;

namespace CR.Banca.Vistas.TokenVirtual;

/// <summary>
/// Reemplaza a la propiedad "ValidarToken" que el UserControl original exponía como fachada de
/// Session["ValidarToken"]. ASP.NET Core Session solo admite string/byte[]/int, por eso se
/// persiste como "true"/"false" en lugar de un bool serializado.
/// </summary>
public static class TokenVirtualSessionExtensions
{
    private const string ClaveValidarToken = "ValidarToken";

    public static bool ObtenerValidarToken(this ISession session) =>
        session.GetString(ClaveValidarToken) == "true";

    public static void EstablecerValidarToken(this ISession session, bool valor) =>
        session.SetString(ClaveValidarToken, valor ? "true" : "false");
}
