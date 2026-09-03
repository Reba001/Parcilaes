using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CR.Banca.Vistas.TokenVirtual;

public static class TokenVirtualServiceCollectionExtensions
{
    /// <summary>
    /// Registra las dependencias que necesita la librería de vistas parciales de Token Virtual: un
    /// HttpClient tipado hacia el servicio REST que valida el OTP (api/TokenVirtual/ValidarOTP).
    /// Requiere la clave de configuración "TokenVirtualApi:BaseUrl" (ej. "https://miservicio.interno/").
    /// </summary>
    public static IServiceCollection AddTokenVirtualVistas(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<TokenVirtualApiClient>(client =>
        {
            var baseUrl = configuration["TokenVirtualApi:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "Falta configurar 'TokenVirtualApi:BaseUrl' para el servicio REST de Token Virtual.");
            }

            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
