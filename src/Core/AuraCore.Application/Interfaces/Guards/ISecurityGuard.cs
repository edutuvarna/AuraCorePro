namespace AuraCore.Application.Interfaces.Guards;

public interface ISecurityGuard
{
    bool ValidatePath(string path);
    bool ValidateRegistryPath(string registryPath);
}
