using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DocMind.IntegrationTests.Infrastructure;

internal sealed class FaultInjectionStartupFilter : IStartupFilter
{
    public const string FaultPath = "/__test/fault";
    public const string FaultMessage = "sensitive-internal-detail";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use(async (context, pipelineNext) =>
        {
            if (context.Request.Path == FaultPath)
            {
                throw new InvalidOperationException(FaultMessage);
            }

            await pipelineNext(context);
        });
    };
}
