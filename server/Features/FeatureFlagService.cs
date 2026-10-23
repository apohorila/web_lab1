using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace server.Features;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IOptionsMonitor<FeatureFlagsOptions> _options;
    private readonly IPlanService _plans;

    public FeatureFlagService(IOptionsMonitor<FeatureFlagsOptions> options, IPlanService plans)
    {
        _options = options;
        _plans = plans;
    }

    public async Task<bool> IsEnabledAsync(string flag, ClaimsPrincipal? user) =>
        (await GetStateAsync(flag, user)).Access;

    public Task<FeatureFlagState> GetStateAsync(string flag, ClaimsPrincipal? user) =>
        EvaluateAsync(flag, user, new Lazy<Task<PlanInfo>>(() => _plans.GetPlanAsync(user)));

    public async Task<IReadOnlyDictionary<string, FeatureFlagState>> GetAllAsync(
        ClaimsPrincipal? user,
        PlanInfo? knownPlan = null
    )
    {

        var plan = new Lazy<Task<PlanInfo>>(
            () => knownPlan != null ? Task.FromResult(knownPlan) : _plans.GetPlanAsync(user)
        );

        var result = new Dictionary<string, FeatureFlagState>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _options.CurrentValue.Flags.Keys)
        {
            result[name] = await EvaluateAsync(name, user, plan);
        }
        return result;
    }

    private async Task<FeatureFlagState> EvaluateAsync(
        string flag,
        ClaimsPrincipal? user,
        Lazy<Task<PlanInfo>> plan
    )
    {
        var options = _options.CurrentValue;

        if (!options.Flags.TryGetValue(flag, out var definition) || !definition.Enabled)
        {
            return new FeatureFlagState(false, false);
        }

        if (definition.Plans.Count == 0 && definition.Users.Count == 0)
        {
            return new FeatureFlagState(true, true);
        }

        if (options.AdminBypass && user?.IsInRole("Admin") == true)
        {
            return new FeatureFlagState(true, true);
        }

        var email = user.GetEmail();
        if (email != null && definition.Users.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            return new FeatureFlagState(true, true);
        }

        if (definition.Plans.Count > 0 && user.GetUserId() != null)
        {
            var current = (await plan.Value).Plan;
            if (definition.Plans.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                return new FeatureFlagState(true, true);
            }
        }

        return new FeatureFlagState(true, false);
    }
}
