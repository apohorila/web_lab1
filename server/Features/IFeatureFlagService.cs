using System.Security.Claims;

namespace server.Features;

public interface IFeatureFlagService
{

    Task<bool> IsEnabledAsync(string flag, ClaimsPrincipal? user);

    Task<FeatureFlagState> GetStateAsync(string flag, ClaimsPrincipal? user);


    Task<IReadOnlyDictionary<string, FeatureFlagState>> GetAllAsync(
        ClaimsPrincipal? user,
        PlanInfo? knownPlan = null
    );
}
