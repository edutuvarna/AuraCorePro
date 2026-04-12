namespace AuraCore.Module.MacAppInstaller.Models;

public enum MacPackageSource
{
    BrewFormula,  // brew install <name>
    BrewCask,     // brew install --cask <name>
}
