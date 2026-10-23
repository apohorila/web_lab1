namespace server.Features;


public static class Plans
{
    public const string Free = "Free";
    public const string Premium = "Premium";
}


public static class FeatureNames
{
    public const string PremiumLounge = "PremiumLounge";
}


public class PlanOptions
{

    public decimal PremiumSpendThreshold { get; set; } = 20000m;
}


public class FeatureFlagDefinition
{
    public bool Enabled { get; set; }


    public List<string> Plans { get; set; } = new();


    public List<string> Users { get; set; } = new();
}

public class FeatureFlagsOptions
{

    public bool AdminBypass { get; set; } = true;

    public Dictionary<string, FeatureFlagDefinition> Flags { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}


public record FeatureFlagState(bool Enabled, bool Access);

public record PlanInfo(string Plan, decimal Spent, decimal Threshold);
