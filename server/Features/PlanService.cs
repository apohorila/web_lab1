using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace server.Features;

public interface IPlanService
{
    Task<PlanInfo> GetPlanAsync(ClaimsPrincipal? user);
}


public class PlanService : IPlanService
{
    private readonly IOptionsMonitor<PlanOptions> _options;
    private readonly ISpendProvider _spend;

    public PlanService(IOptionsMonitor<PlanOptions> options, ISpendProvider spend)
    {
        _options = options;
        _spend = spend;
    }

    public async Task<PlanInfo> GetPlanAsync(ClaimsPrincipal? user)
    {
        var threshold = _options.CurrentValue.PremiumSpendThreshold;

        var userId = user.GetUserId();
        if (userId == null)
        {
            return new PlanInfo(Plans.Free, 0m, threshold);
        }

        var spent = await _spend.GetLifetimeSpendAsync(userId.Value);
        return new PlanInfo(spent >= threshold ? Plans.Premium : Plans.Free, spent, threshold);
    }
}
