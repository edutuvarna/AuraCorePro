using System.Text;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Generates autounattend.xml and PostInstall.ps1 for Windows ISO customization.
/// Port from WinUI3 IsoBuilderService — platform-independent generation logic.
/// </summary>
public static class IsoBuilderService
{
    public static string GenerateUnattendXml(IsoBuilderConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");

        // windowsPE
        sb.AppendLine("  <settings pass=\"windowsPE\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-International-Core-WinPE\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine($"      <SetupUILanguage><UILanguage>{cfg.Language}</UILanguage></SetupUILanguage>");
        sb.AppendLine($"      <InputLocale>{cfg.Language}</InputLocale><SystemLocale>{cfg.Language}</SystemLocale><UILanguage>{cfg.Language}</UILanguage><UserLocale>{cfg.Language}</UserLocale>");
        sb.AppendLine("    </component>");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        if (cfg.SkipEula) sb.AppendLine("      <UserData><AcceptEula>true</AcceptEula><FullName>User</FullName><Organization>AuraCore</Organization></UserData>");

        if (cfg.BypassTpm || cfg.BypassSecureBoot || cfg.BypassRam || cfg.BypassStorage || cfg.BypassCpu)
        {
            sb.AppendLine("      <RunSynchronous>"); var o = 1;
            if (cfg.BypassTpm) sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            if (cfg.BypassSecureBoot) sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            if (cfg.BypassRam) sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            if (cfg.BypassStorage) sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassStorageCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            if (cfg.BypassCpu) sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            sb.AppendLine("      </RunSynchronous>");
        }
        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");

        // specialize
        sb.AppendLine("  <settings pass=\"specialize\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine($"      <ComputerName>{(string.IsNullOrEmpty(cfg.ComputerName) ? "*" : cfg.ComputerName)}</ComputerName>");
        sb.AppendLine($"      <TimeZone>{cfg.Timezone}</TimeZone>");
        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");

        // oobeSystem
        sb.AppendLine("  <settings pass=\"oobeSystem\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");

        if (cfg.SkipOobe)
        {
            sb.AppendLine("      <OOBE>");
            sb.AppendLine("        <HideEULAPage>true</HideEULAPage><HideLocalAccountScreen>true</HideLocalAccountScreen>");
            sb.AppendLine("        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen><HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
            sb.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE><ProtectYourPC>3</ProtectYourPC>");
            sb.AppendLine("        <SkipMachineOOBE>true</SkipMachineOOBE><SkipUserOOBE>true</SkipUserOOBE>");
            sb.AppendLine("      </OOBE>");
        }

        if (cfg.UseLocalAccount && !string.IsNullOrEmpty(cfg.Username))
        {
            sb.AppendLine("      <UserAccounts><LocalAccounts><LocalAccount wcm:action=\"add\">");
            sb.AppendLine($"        <n>{cfg.Username}</n><DisplayName>{cfg.Username}</DisplayName><Group>Administrators</Group>");
            sb.AppendLine($"        <Password><Value>{cfg.Password}</Value><PlainText>true</PlainText></Password>");
            sb.AppendLine("      </LocalAccount></LocalAccounts></UserAccounts>");
            if (cfg.AutoLogin)
                sb.AppendLine($"      <AutoLogon><Enabled>true</Enabled><Username>{cfg.Username}</Username><LogonCount>1</LogonCount></AutoLogon>");
        }

        sb.AppendLine("      <FirstLogonCommands>");
        var cmdOrder = 1;

        if (cfg.DisableTelemetry)
        {
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>sc config DiagTrack start=disabled</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        if (cfg.DisableCortana)
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");

        if (cfg.DisableOneDrive)
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OneDrive\" /v DisableFileSyncNGSC /t REG_DWORD /d 1 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");

        foreach (var pkg in cfg.BloatwareToRemove)
        {
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>powershell.exe -NoProfile -Command \"Get-AppxPackage -AllUsers *{pkg}* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        if (cfg.ExePaths.Count > 0)
        {
            foreach (var exe in cfg.ExePaths)
            {
                var fn = System.IO.Path.GetFileName(exe);
                var ext = System.IO.Path.GetExtension(exe).ToLowerInvariant();
                var silentArgs = cfg.SilentInstall ? (ext == ".msi" ? "/quiet /norestart" : "/S /silent /quiet /VERYSILENT") : "";
                if (ext == ".msi")
                    sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\AuraCoreInstallers\\{fn} (msiexec /i %%d:\\AuraCoreInstallers\\{fn} {silentArgs})\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
                else
                    sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\AuraCoreInstallers\\{fn} (%%d:\\AuraCoreInstallers\\{fn} {silentArgs})\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            }
        }

        sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\PostInstall.ps1 (powershell.exe -ExecutionPolicy Bypass -File %%d:\\PostInstall.ps1)\"</CommandLine><RequiresUserInput>false</RequiresUserInput><Description>AuraCore PostInstall</Description></SynchronousCommand>");

        sb.AppendLine("      </FirstLogonCommands>");
        sb.AppendLine("      <RegisteredOwner>User</RegisteredOwner>");
        sb.AppendLine("    </component>");
        sb.AppendLine("    <component name=\"Microsoft-Windows-International-Core\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine($"      <InputLocale>{cfg.Language}</InputLocale><SystemLocale>{cfg.Language}</SystemLocale><UILanguage>{cfg.Language}</UILanguage><UserLocale>{cfg.Language}</UserLocale>");
        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");
        sb.AppendLine("</unattend>");
        return sb.ToString();
    }

    public static string GeneratePostInstallScript(IsoBuilderConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AuraCore Pro - Post-Install Script");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
        sb.AppendLine("Write-Host '=== AuraCore Pro Post-Install ===' -ForegroundColor Cyan");
        sb.AppendLine();

        if (cfg.PresetDefault)
        {
            sb.AppendLine("# [DEFAULT]");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'ShowTaskViewButton' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarMn' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-338387Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }
        if (cfg.PresetGaming)
        {
            sb.AppendLine("# [GAMING]");
            sb.AppendLine("New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\GameBar' -Name 'AutoGameModeEnabled' -Value 1 -Type DWord -Force | Out-Null");
            sb.AppendLine("$p='HKCU:\\System\\GameConfigStore'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'GameDVR_Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2>$null");
            sb.AppendLine();
        }
        if (cfg.PresetPrivacy)
        {
            sb.AppendLine("# [PRIVACY]");
            sb.AppendLine("$p='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }
        if (cfg.PresetDev)
        {
            sb.AppendLine("# [DEV]");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'HideFileExt' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Hidden' -Value 1 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name 'LongPathsEnabled' -Value 1 -Type DWord -Force");
            sb.AppendLine();
        }
        if (cfg.PresetOffice)
        {
            sb.AppendLine("# [OFFICE]");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'AppsUseLightTheme' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'SystemUsesLightTheme' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }

        foreach (var a in cfg.PostInstallActions)
        {
            switch (a)
            {
                case "dark_mode": sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'AppsUseLightTheme' -Value 0 -Type DWord -Force"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'SystemUsesLightTheme' -Value 0 -Type DWord -Force"); break;
                case "file_extensions": sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'HideFileExt' -Value 0 -Type DWord -Force"); break;
                case "hidden_files": sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Hidden' -Value 1 -Type DWord -Force"); break;
                case "disable_web_search": sb.AppendLine("$p='HKCU:\\Software\\Policies\\Microsoft\\Windows\\Explorer'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}; Set-ItemProperty -Path $p -Name 'DisableSearchBoxSuggestions' -Value 1 -Type DWord -Force"); break;
                case "classic_context_menu": sb.AppendLine("reg add 'HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32' /f /ve 2>$null"); break;
                case "disable_widgets": sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarDa' -Value 0 -Type DWord -Force"); break;
                case "disable_copilot": sb.AppendLine("$p='HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}; Set-ItemProperty -Path $p -Name 'TurnOffWindowsCopilot' -Value 1 -Type DWord -Force"); break;
                case "taskbar_left": sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarAl' -Value 0 -Type DWord -Force"); break;
                case "install_winget": sb.AppendLine("try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -EA SilentlyContinue } catch {}"); break;
            }
            sb.AppendLine();
        }

        if (cfg.WingetApps.Count > 0)
        {
            sb.AppendLine("# [WINGET APPS]");
            foreach (var app in cfg.WingetApps)
                sb.AppendLine($"try {{ winget install {app} --accept-source-agreements --accept-package-agreements -h 2>$null }} catch {{}}");
            sb.AppendLine();
        }

        sb.AppendLine("Stop-Process -Name explorer -Force -EA SilentlyContinue; Start-Sleep 2; Start-Process explorer");
        sb.AppendLine("Write-Host '=== All done! Powered by AuraCore Pro ===' -ForegroundColor Green");
        return sb.ToString();
    }
}

public sealed class IsoBuilderConfig
{
    public string IsoPath { get; set; } = "";
    public bool UseLocalAccount { get; set; } = true;
    public string Username { get; set; } = "Admin";
    public string Password { get; set; } = "";
    public bool AutoLogin { get; set; } = true;
    public bool SkipOobe { get; set; } = true;
    public bool SkipEula { get; set; } = true;
    public bool DisableTelemetry { get; set; } = true;
    public bool DisableCortana { get; set; } = true;
    public bool DisableOneDrive { get; set; } = true;
    public string ComputerName { get; set; } = "";
    public string Language { get; set; } = "en-US";
    public string Timezone { get; set; } = "UTC";
    public bool BypassTpm { get; set; }
    public bool BypassSecureBoot { get; set; }
    public bool BypassRam { get; set; }
    public bool BypassStorage { get; set; }
    public bool BypassCpu { get; set; }
    public bool WifiEnabled { get; set; }
    public string WifiSsid { get; set; } = "";
    public string WifiPassword { get; set; } = "";
    public string WifiSecurity { get; set; } = "WPA2PSK";
    public List<string> BloatwareToRemove { get; set; } = new();
    public List<string> PostInstallActions { get; set; } = new();
    public List<string> ExePaths { get; set; } = new();
    public bool SilentInstall { get; set; } = true;
    public bool IncludeDrivers { get; set; } = true;
    public List<string> WingetApps { get; set; } = new();
    public bool PresetDefault { get; set; } = true;
    public bool PresetGaming { get; set; }
    public bool PresetPrivacy { get; set; }
    public bool PresetDev { get; set; }
    public bool PresetOffice { get; set; }
}
