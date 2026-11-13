namespace BrimDeck.Sensors;

// Same strings as BrimDeck.Core.ProductIdentity. The service does not reference that project.
// ArchitectureChecks compares the two so they cannot drift.
internal static class SensorIdentity
{
    public const string ServiceName = "BrimDeckArm64Sensors";
    public const string PipeName = "BrimDeckArm64.Sensors";
}
