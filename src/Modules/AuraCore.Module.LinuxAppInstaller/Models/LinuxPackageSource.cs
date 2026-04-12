namespace AuraCore.Module.LinuxAppInstaller.Models;

public enum LinuxPackageSource
{
    Apt,       // apt install
    Snap,      // snap install
    Flatpak,   // flatpak install flathub
    Dnf,       // dnf install
}
