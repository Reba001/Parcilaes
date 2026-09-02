using CR.Banca.Modelos.Token.TokenVirtual;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CR.Banca.Vistas.TokenVirtual;

public static class TokenVirtualServiceCollectionExtensions
{
    /// <summary>
    /// Registra las dependencias que necesita la librería de vistas parciales de Token Virtual
    /// (el endpoint de validación usa LogicaTokenVirtual). Usa TryAddScoped para no pisar un
    /// registro que la app anfitriona ya haya hecho.
    /// </summary>
    public static IServiceCollection AddTokenVirtualVistas(this IServiceCollection services)
    {
        services.TryAddScoped<LogicaTokenVirtual>();
        return services;
    }
}
