using Microsoft.Extensions.Logging.Abstractions;
using Parcilaes.Api.Controllers;

namespace Parcilaes.Api.Tests;

public class WeatherForecastControllerTests
{
    [Fact]
    public void Get_ReturnsFiveForecasts()
    {
        var controller = new WeatherForecastController(NullLogger<WeatherForecastController>.Instance);

        var result = controller.Get();

        Assert.Equal(5, result.Count());
    }
}
