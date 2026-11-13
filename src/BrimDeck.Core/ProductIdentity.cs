namespace BrimDeck.Core;

// Identity of this fork. It is a separate product from MoFeng2223/BrimDeck: its own installer id, data folder,
// update repository and sensor service, so an ARM64 install cannot replace or update an upstream x64 copy.
public static class ProductIdentity
{
    public const string ProductId = "BrimDeck-Arm64";
    public const string Repository = "saladfinger123/BrimDeck-Arm64";
    public const string RepositoryUrl = "https://github.com/" + Repository;
    public const string UpstreamRepository = "MoFeng2223/BrimDeck";
    public const string UpstreamRepositoryUrl = "https://github.com/" + UpstreamRepository;
    // Braces included, as the uninstall key stores them. The upstream id is 9D9AD6ED-CF62-4527-A587-6DBE94A74B9E.
    public const string InstallerAppId = "{C4E8A1B7-6D24-4F93-8A15-2B7E9D0C3F61}";
    public const string InstallerAppIdValue = "C4E8A1B7-6D24-4F93-8A15-2B7E9D0C3F61";
    public const string UpstreamInstallerAppId = "{9D9AD6ED-CF62-4527-A587-6DBE94A74B9E}";
    public const string AppName = "BrimDeck Arm64";
    public const string DataFolderName = "BrimDeck-Arm64";
    public const string UpstreamDataFolderName = "BrimDeck";
    public const string MutexNamePrefix = "Local\\BrimDeckArm64.Instance.";
    public const string SettingsEventPrefix = "Local\\BrimDeckArm64.Settings.";
    public const string AppUserModelId = "BrimDeck.Arm64.Desktop";
    public const string RunValue = "BrimDeck Arm64";
    public const string ServiceName = "BrimDeckArm64Sensors";
    public const string UpstreamServiceName = "BrimDeckSensors";
    public const string SensorPipeName = "BrimDeckArm64.Sensors";
    public const string SensorFolderName = "BrimDeck Arm64 Sensors";

    public static string DataDirectory(string localApplicationData) => Path.Combine(localApplicationData, DataFolderName);

    // Installing and upgrading never delete the data folder. Uninstall deletes it only when the user asks.
    public static bool UninstallRemovesUserData(bool userAsked, bool upgrade) => userAsked && !upgrade;
}
