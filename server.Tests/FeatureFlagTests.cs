using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using server.Controllers;
using server.Features;
using Xunit;

namespace server.Tests;


public class FeatureFlagTests
{

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  

    private sealed class Monitor<T> : IOptionsMonitor<T>
        where T : class
    {
        public Monitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeSpend : ISpendProvider
    {
        private readonly Dictionary<int, decimal> _spent = new();

        public int Lookups { get; private set; }

        public FakeSpend Set(int userId, decimal amount)
        {
            _spent[userId] = amount;
            return this;
        }

        public Task<decimal> GetLifetimeSpendAsync(int userId)
        {
            Lookups++;
            return Task.FromResult(_spent.TryGetValue(userId, out var amount) ? amount : 0m);
        }
    }

    private static FeatureFlagsOptions FlagOptions(bool adminBypass = true) =>
        new()
        {
            AdminBypass = adminBypass,
            Flags =
            {
                ["PremiumLounge"] = new FeatureFlagDefinition { Enabled = true, Plans = { "Premium" } },
                ["Open"] = new FeatureFlagDefinition { Enabled = true },
                ["Off"] = new FeatureFlagDefinition { Enabled = false, Plans = { "Premium" } },
            },
        };

    private static ClaimsPrincipal User(int id, string email = "user@example.com", string role = "Customer") =>
        new(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role),
                },
                "Test"
            )
        );

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private sealed record Setup(
        FeatureFlagService Flags,
        PlanService Plans,
        FakeSpend Spend,
        Monitor<FeatureFlagsOptions> FlagMonitor,
        Monitor<PlanOptions> PlanMonitor
    );

    private static Setup Create(bool adminBypass = true, decimal threshold = 20000m)
    {
        var spend = new FakeSpend();
        var planMonitor = new Monitor<PlanOptions>(new PlanOptions { PremiumSpendThreshold = threshold });
        var flagMonitor = new Monitor<FeatureFlagsOptions>(FlagOptions(adminBypass));
        var plans = new PlanService(planMonitor, spend);
        return new Setup(new FeatureFlagService(flagMonitor, plans), plans, spend, flagMonitor, planMonitor);
    }



    [Fact]
    public async Task Anonymous_IsFree_WithoutTouchingTheDatabase()
    {
        var s = Create();

        var plan = await s.Plans.GetPlanAsync(Anonymous());

        Assert.Equal("Free", plan.Plan);
        Assert.Equal(0m, plan.Spent);
        Assert.Equal(20000m, plan.Threshold);
        Assert.Equal(0, s.Spend.Lookups);
    }

    [Fact]
    public async Task BelowTheThreshold_IsFree_AndReportsProgress()
    {
        var s = Create();
        s.Spend.Set(1, 19999.99m);

        var plan = await s.Plans.GetPlanAsync(User(1));

        Assert.Equal("Free", plan.Plan);
        Assert.Equal(19999.99m, plan.Spent);
    }

    [Fact]
    public async Task ExactlyTheThreshold_IsPremium()
    {
        var s = Create();
        s.Spend.Set(1, 20000m);

        Assert.Equal("Premium", (await s.Plans.GetPlanAsync(User(1))).Plan);
    }

    [Fact]
    public async Task AboveTheThreshold_IsPremium()
    {
        var s = Create();
        s.Spend.Set(1, 54300m);

        Assert.Equal("Premium", (await s.Plans.GetPlanAsync(User(1))).Plan);
    }

    [Fact]
    public async Task ClientWithoutOrders_IsFree()
    {
        var s = Create();

        Assert.Equal("Free", (await s.Plans.GetPlanAsync(User(77))).Plan);
    }

    [Fact]
    public async Task ThePlanFollowsTheSpend_BuyingUpgrades_CancellingDowngrades()
    {
        var s = Create();
        var client = User(1); 
        s.Spend.Set(1, 15000m);
        Assert.Equal("Free", (await s.Plans.GetPlanAsync(client)).Plan);

        s.Spend.Set(1, 21500m); 
        Assert.Equal("Premium", (await s.Plans.GetPlanAsync(client)).Plan);

        s.Spend.Set(1, 12000m);
        Assert.Equal("Free", (await s.Plans.GetPlanAsync(client)).Plan);
    }

    [Fact]
    public async Task TheThresholdComesFromConfiguration_AndCanBeChangedWithoutRestart()
    {
        var s = Create(threshold: 20000m);
        s.Spend.Set(1, 6000m);
        Assert.Equal("Free", (await s.Plans.GetPlanAsync(User(1))).Plan);

        s.PlanMonitor.CurrentValue = new PlanOptions { PremiumSpendThreshold = 5000m }; // правка appsettings.json

        var plan = await s.Plans.GetPlanAsync(User(1));
        Assert.Equal("Premium", plan.Plan);
        Assert.Equal(5000m, plan.Threshold);
    }

  

    [Fact]
    public async Task UnknownFlag_IsDisabled()
    {
        var s = Create();

        Assert.False(await s.Flags.IsEnabledAsync("DoesNotExist", User(1)));
    }

    [Fact]
    public async Task DisabledFlag_IsOffForEveryone_EvenAdminAndPremium()
    {
        var s = Create();
        s.Spend.Set(1, 50000m);

        Assert.False(await s.Flags.IsEnabledAsync("Off", User(1)));
        Assert.False(await s.Flags.IsEnabledAsync("Off", User(2, role: "Admin")));
    }

    [Fact]
    public async Task FlagWithoutRestrictions_IsOpenToEveryoneIncludingAnonymous()
    {
        var s = Create();

        Assert.True(await s.Flags.IsEnabledAsync("Open", Anonymous()));
        Assert.True(await s.Flags.IsEnabledAsync("Open", User(1)));
    }

    [Fact]
    public async Task PremiumLounge_FreeClient_IsEnabledButHasNoAccess()
    {
        var s = Create();
        s.Spend.Set(1, 3000m);

        var state = await s.Flags.GetStateAsync("PremiumLounge", User(1));

        Assert.True(state.Enabled);
        Assert.False(state.Access);
    }

    [Fact]
    public async Task PremiumLounge_PremiumClient_HasAccess()
    {
        var s = Create();
        s.Spend.Set(1, 25000m);

        Assert.True(await s.Flags.IsEnabledAsync("PremiumLounge", User(1)));
    }

    [Fact]
    public async Task PremiumLounge_Anonymous_HasNoAccess()
    {
        var s = Create();

        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", Anonymous()));
        Assert.Equal(0, s.Spend.Lookups);
    }

    [Fact]
    public async Task AccessAppearsAutomatically_WhenTheClientCrossesTheThreshold()
    {
        var s = Create();
        var client = User(1);
        s.Spend.Set(1, 19000m);
        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", client));

        s.Spend.Set(1, 20500m);

        Assert.True(await s.Flags.IsEnabledAsync("PremiumLounge", client));
    }

    [Fact]
    public async Task Admin_SeesPremiumFeatures_WhenAdminBypassIsOn()
    {
        var s = Create(adminBypass: true);

        Assert.True(await s.Flags.IsEnabledAsync("PremiumLounge", User(9, role: "Admin")));
    }

    [Fact]
    public async Task Admin_HasNoSpecialAccess_WhenAdminBypassIsOff()
    {
        var s = Create(adminBypass: false);

        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", User(9, role: "Admin")));
    }

    [Fact]
    public async Task AllowListedEmail_GetsAccessWithoutPremium_CaseInsensitive()
    {
        var s = Create();
        s.FlagMonitor.CurrentValue.Flags["PremiumLounge"].Users.Add("VIP@Example.com");

        Assert.True(await s.Flags.IsEnabledAsync("PremiumLounge", User(5, "vip@example.com")));
        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", User(6, "other@example.com")));
    }

    [Fact]
    public async Task ConfigurationChange_IsPickedUpWithoutRestart()
    {
        var s = Create();
        var free = User(1);
        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", free));

        var reloaded = FlagOptions();
        reloaded.Flags["PremiumLounge"].Plans.Clear(); 
        s.FlagMonitor.CurrentValue = reloaded;
        Assert.True(await s.Flags.IsEnabledAsync("PremiumLounge", free));

        reloaded.Flags["PremiumLounge"].Enabled = false;
        Assert.False(await s.Flags.IsEnabledAsync("PremiumLounge", free));
    }

    [Fact]
    public async Task GetAll_ReturnsEveryFlag_WithASingleSpendLookup()
    {
        var s = Create();
        s.Spend.Set(1, 30000m);

        var all = await s.Flags.GetAllAsync(User(1));

        Assert.Equal(3, all.Count);
        Assert.True(all["PremiumLounge"].Access);
        Assert.True(all["Open"].Access);
        Assert.False(all["Off"].Enabled);
        Assert.Equal(1, s.Spend.Lookups);
    }

    [Fact]
    public async Task GetAll_WithAKnownPlan_DoesNotQueryTheSpendAgain()
    {
        var s = Create();

        var all = await s.Flags.GetAllAsync(User(1), new PlanInfo("Premium", 25000m, 20000m));

        Assert.True(all["PremiumLounge"].Access);
        Assert.Equal(0, s.Spend.Lookups);
    }

    

    private static async Task<(IActionResult? result, int nextCalls)> RunGate(
        IFeatureFlagService flags,
        ClaimsPrincipal user,
        string feature = "PremiumLounge"
    )
    {
        var services = new ServiceCollection().AddSingleton<IFeatureFlagService>(flags).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, User = user };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object()
        );

        var calls = 0;
        ActionExecutionDelegate next = () =>
        {
            calls++;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        };

        await new RequireFeatureAttribute(feature).OnActionExecutionAsync(executing, next);
        return (executing.Result, calls);
    }

    private static JsonElement BodyOf(IActionResult? result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<ObjectResult>(result).Value, Web)).RootElement;

    [Fact]
    public async Task Gate_FreeClient_Gets403AndTheActionIsNotExecuted()
    {
        var s = Create();
        s.Spend.Set(1, 500m);

        var (result, calls) = await RunGate(s.Flags, User(1));

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal("plan_required", BodyOf(result).GetProperty("reason").GetString());
        Assert.Equal("PremiumLounge", BodyOf(result).GetProperty("feature").GetString());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Gate_PremiumClient_ReachesTheAction()
    {
        var s = Create();
        s.Spend.Set(1, 22000m);

        var (result, calls) = await RunGate(s.Flags, User(1));

        Assert.Null(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Gate_FlagTurnedOff_Gets403WithReasonDisabled()
    {
        var s = Create();
        s.Spend.Set(1, 22000m);

        var (result, calls) = await RunGate(s.Flags, User(1), "Off");

        Assert.Equal("disabled", BodyOf(result).GetProperty("reason").GetString());
        Assert.Equal(0, calls);
    }

    

    private static T WithUser<T>(T controller, ClaimsPrincipal user)
        where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }

    [Fact]
    public async Task GetFeatures_Anonymous_HasNoPlan_ButShowsTheFlagsAndTheGoal()
    {
        var s = Create();
        var controller = WithUser(new FeaturesController(s.Flags, s.Plans), Anonymous());

        var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.GetFeatures()).Value, Web)).RootElement;

        Assert.Equal(JsonValueKind.Null, json.GetProperty("plan").ValueKind);
        Assert.Equal(0m, json.GetProperty("spent").GetDecimal());
        Assert.Equal(20000m, json.GetProperty("threshold").GetDecimal());
        Assert.True(json.GetProperty("flags").GetProperty("PremiumLounge").GetProperty("enabled").GetBoolean());
        Assert.False(json.GetProperty("flags").GetProperty("PremiumLounge").GetProperty("access").GetBoolean());
    }

    [Fact]
    public async Task GetFeatures_FreeClient_SeesTheProgressToPremium()
    {
        var s = Create();
        s.Spend.Set(1, 8200m);
        var controller = WithUser(new FeaturesController(s.Flags, s.Plans), User(1));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.GetFeatures()).Value, Web)).RootElement;

        Assert.Equal("Free", json.GetProperty("plan").GetString());
        Assert.Equal(8200m, json.GetProperty("spent").GetDecimal());
        Assert.False(json.GetProperty("flags").GetProperty("PremiumLounge").GetProperty("access").GetBoolean());
        Assert.Equal(1, s.Spend.Lookups); // план і прапорці рахуються одним запитом
    }

    [Fact]
    public async Task GetFeatures_PremiumClient_GetsAccess()
    {
        var s = Create();
        s.Spend.Set(1, 31000m);
        var controller = WithUser(new FeaturesController(s.Flags, s.Plans), User(1));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.GetFeatures()).Value, Web)).RootElement;

        Assert.Equal("Premium", json.GetProperty("plan").GetString());
        Assert.True(json.GetProperty("flags").GetProperty("PremiumLounge").GetProperty("access").GetBoolean());
    }

    [Fact]
    public void Lounge_RequiresLogin_AndThePremiumLoungeFlag()
    {
        var method = typeof(PremiumController).GetMethod("GetLounge")!;

        Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        var gate = (RequireFeatureAttribute?)method.GetCustomAttributes(typeof(RequireFeatureAttribute), true).SingleOrDefault();
        Assert.NotNull(gate);
        Assert.Equal("PremiumLounge", gate!.Feature);
    }

    [Fact]
    public void FeaturesEndpoint_IsPublic()
    {
        var method = typeof(FeaturesController).GetMethod("GetFeatures")!;

        Assert.Equal(0, method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Length);
    }

    [Fact]
    public void Lounge_ReturnsThePromoCodePerksAndUpcomingCollections()
    {
        var ok = Assert.IsType<OkObjectResult>(new PremiumController().GetLounge());
        var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, Web)).RootElement;

        Assert.Equal("CLUB10", json.GetProperty("promoCode").GetString());
        Assert.True(json.GetProperty("perks").GetArrayLength() >= 3);
        Assert.True(json.GetProperty("upcoming").GetArrayLength() >= 1);
    }
}
