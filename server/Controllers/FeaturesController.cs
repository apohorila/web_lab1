using Microsoft.AspNetCore.Mvc;
using server.Features;

namespace server.Controllers;

[ApiController]
[Route("api/features")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlagService _flags;
    private readonly IPlanService _plans;

    public FeaturesController(IFeatureFlagService flags, IPlanService plans)
    {
        _flags = flags;
        _plans = plans;
    }


    [HttpGet]
    public async Task<IActionResult> GetFeatures()
    {
        var plan = await _plans.GetPlanAsync(User);
        var flags = await _flags.GetAllAsync(User, plan);

        return Ok(
            new
            {
                plan = User.GetUserId() == null ? null : plan.Plan,
                spent = plan.Spent,
                threshold = plan.Threshold,
                flags,
            }
        );
    }
}
