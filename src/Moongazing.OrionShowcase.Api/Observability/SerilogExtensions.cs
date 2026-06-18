namespace Moongazing.OrionShowcase.Api.Observability;

using Microsoft.AspNetCore.Builder;
using Serilog;

public static class SerilogExtensions
{
    public static WebApplicationBuilder UseSerilogForOrion(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               // OrionLens correlation: stamp every event with the ambient correlation id so
               // logs across the request flow tie back to one logical operation.
               .Enrich.With<CorrelationEnricher>()
               .WriteTo.Console()
               .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"]!);
        });
        return builder;
    }
}
