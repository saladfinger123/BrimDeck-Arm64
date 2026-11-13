namespace BrimDeck.Core;

// The check a running copy applies before it keeps a manifest. Identity and architecture are required together:
// a file that is only named arm64, or a hash from the upstream repository, is not an update for this fork.
public static class UpdateAdmission
{
    public static void Ensure(UpdateManifest manifest, string clientArchitecture, UpdateMode mode)
    {
        if (!ArchitectureCapabilities.IsSupportedRuntime(clientArchitecture))
            throw new InvalidDataException("This copy's architecture cannot take an update.");
        if (!string.Equals(manifest.Product, ProductIdentity.ProductId, StringComparison.Ordinal))
            throw new InvalidDataException("Update manifest is not the BrimDeck-Arm64 fork.");
        if (!string.Equals(manifest.Repository, ProductIdentity.Repository, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Repository, ProductIdentity.UpstreamRepository, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update manifest is not published by " + ProductIdentity.Repository + ".");
        var package = manifest.PackageFor(mode, clientArchitecture) ?? throw new InvalidDataException("No " + clientArchitecture + " package for this copy.");
        if (!string.Equals(package.Architecture, clientArchitecture, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package architecture does not match this copy.");
        if (package.File.IndexOf(clientArchitecture, StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidDataException("Package file name does not state its architecture.");
    }
}
