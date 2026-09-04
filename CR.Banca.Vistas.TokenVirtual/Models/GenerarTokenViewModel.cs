namespace CR.Banca.Vistas.TokenVirtual.Models;

/// <summary>
/// Equivalente al antiguo enum "String_Type" (Banca.TokenVirtual_DLL.Constantes) que indicaba el
/// medio de envío del OTP en GenerarToken.ascx.cs.
/// </summary>
public enum TipoEnvioOtp
{
    Sms,
    Email
}

/// <summary>
/// Modelo de la partial view de GenerarToken (reenvío de OTP por SMS/Email). Reemplaza a las
/// propiedades públicas del UserControl (btn_GenerarSMS_CssClass, btn_GenerarSMS_Text, etc.).
/// </summary>
public class GenerarTokenViewModel
{
    public string ContainerId { get; set; } = "pn_GenToken";

    public string BotonSmsTexto { get; set; } = "Mensaje de texto";

    public string BotonSmsCssClass { get; set; } = "tokensms-botones";

    public bool BotonSmsHabilitado { get; set; } = true;

    public string BotonEmailTexto { get; set; } = "Correo electrónico";

    public string BotonEmailCssClass { get; set; } = "tokensms-botones";

    public bool BotonEmailHabilitado { get; set; } = true;

    /// <summary>Mensaje de éxito/error tras generar el OTP (equivalente a lbl_Mensaje.Text).</summary>
    public string? Mensaje { get; set; }

    public string MensajeCssClass { get; set; } = "tokensms-error";

    /// <summary>Endpoint que genera/reenvía el OTP vía fetch.</summary>
    public string GenerarUrl { get; set; } = "/api/generar-token/generar";
}
