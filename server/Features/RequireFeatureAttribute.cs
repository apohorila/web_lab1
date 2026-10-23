using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace server.Features;


[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    public RequireFeatureAttribute(string feature)
    {
        Feature = feature;
    }

    public string Feature { get; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        var state = await flags.GetStateAsync(Feature, context.HttpContext.User);

        if (state.Access)
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(
            new
            {
                feature = Feature,
                reason = state.Enabled ? "plan_required" : "disabled",
                message = state.Enabled
                    ? "Ця функція доступна лише в тарифі Premium"
                    : "Ця функція зараз вимкнена",
            }
        )
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
