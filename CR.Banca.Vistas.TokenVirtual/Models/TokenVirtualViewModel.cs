namespace CR.Banca.Vistas.TokenVirtual.Models;

/// <summary>
/// Equivalente al antiguo enum "uxToken_Type" (Banca.TokenVirtual_DLL.Constantes) que decidía
/// qué layout dibujaba el UserControl de WebForms.
/// </summary>
public enum TipoVistaToken
{
    BiEnLinea_Perfil,
    BiEnLinea,
    BiBanking,
    Westrust_ACH,
    Conexion_Regional_Login,
    Conexion_Regional
}

/// <summary>
/// Modelo de la partial view de Token Virtual. Reemplaza a las propiedades públicas que exponía
/// el UserControl (btn_TokenVirtualIngresar_CssClass, lbl_Error_Text, etc.) para que la página
/// anfitriona pueda configurar el control antes de renderizarlo.
/// </summary>
public class TokenVirtualViewModel
{
    public TipoVistaToken Tipo { get; set; } = TipoVistaToken.BiEnLinea_Perfil;

    public string ContainerId { get; set; } = "pn_Token";

    public bool MostrarEtiquetaIngresarToken { get; set; } = true;

    public string LabelIngresarTokenTexto { get; set; } = "Ingresa tu TOKEN:";

    public string LabelCssClass { get; set; } = "tokensms-label";

    public string InputCssClass { get; set; } = "campoTexto tokensms-input";

    public int InputMaxLength { get; set; } = 8;

    public string InputPlaceholder { get; set; } = "TOKEN";

    public string BotonIngresarTexto { get; set; } = "Ingresar";

    public string BotonIngresarCssClass { get; set; } = "tokensms-botones";

    public bool BotonIngresarHabilitado { get; set; } = true;

    public string BotonCancelarTexto { get; set; } = "Cancelar";

    public string BotonCancelarCssClass { get; set; } = "tokensms-botones";

    public string ErrorCssClass { get; set; } = "tokensms-error";

    public string? ImagenTokenUrl { get; set; }

    /// <summary>
    /// Nombre (soporta notación "objeto.metodo") de la función JS global que la página anfitriona
    /// define para continuar el flujo cuando el OTP se valida correctamente. Reemplaza al antiguo
    /// delegado "accionRealizar_Click" del code-behind.
    /// </summary>
    public string? OnSuccessCallback { get; set; }

    /// <summary>
    /// Nombre de la función JS invocada al presionar "Cancelar". Reemplaza al antiguo
    /// btn_TokenVirtualCancelar_Click (que en el UserControl original quedaba a cargo de cada página).
    /// </summary>
    public string? OnCancelCallback { get; set; }

    /// <summary>
    /// Endpoint que valida el OTP vía fetch. Reemplaza al postback parcial del UpdatePanel.
    /// </summary>
    public string ValidarUrl { get; set; } = "/api/token-virtual/validar";
}
