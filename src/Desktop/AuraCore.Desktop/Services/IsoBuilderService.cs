using System.Text;
using AuraCore.Desktop.Pages;

namespace AuraCore.Desktop.Services;

public static class IsoBuilderService
{
    public static string GenerateUnattendXml(IsoBuilderConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");

        // ── windowsPE ──
        sb.AppendLine("  <settings pass=\"windowsPE\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-International-Core-WinPE\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine($"      <SetupUILanguage><UILanguage>{cfg.Language}</UILanguage></SetupUILanguage>");
        sb.AppendLine($"      <InputLocale>{cfg.Language}</InputLocale><SystemLocale>{cfg.Language}</SystemLocale><UILanguage>{cfg.Language}</UILanguage><UserLocale>{cfg.Language}</UserLocale>");
        sb.AppendLine("    </component>");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        if (cfg.SkipEula) sb.AppendLine("      <UserData><AcceptEula>true</AcceptEula><FullName>User</FullName><Organization>AuraCore</Organization></UserData>");

        // Win11 bypass
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

        // ── specialize ──
        sb.AppendLine("  <settings pass=\"specialize\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine($"      <ComputerName>{(string.IsNullOrEmpty(cfg.ComputerName) ? "*" : cfg.ComputerName)}</ComputerName>");
        sb.AppendLine($"      <TimeZone>{cfg.Timezone}</TimeZone>");
        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");

        // ── oobeSystem ──
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
            sb.AppendLine($"        <Name>{cfg.Username}</Name><DisplayName>{cfg.Username}</DisplayName><Group>Administrators</Group>");
            sb.AppendLine($"        <Password><Value>{cfg.Password}</Value><PlainText>true</PlainText></Password>");
            sb.AppendLine("      </LocalAccount></LocalAccounts></UserAccounts>");
            if (cfg.AutoLogin)
                sb.AppendLine($"      <AutoLogon><Enabled>true</Enabled><Username>{cfg.Username}</Username><LogonCount>1</LogonCount></AutoLogon>");
        }

        // ── FirstLogonCommands ──
        sb.AppendLine("      <FirstLogonCommands>");
        var cmdOrder = 1;

        // Telemetry
        if (cfg.DisableTelemetry)
        {
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>sc config DiagTrack start=disabled</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>sc config dmwappushservice start=disabled</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        // Cortana
        if (cfg.DisableCortana)
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");

        // WiFi Auto-Connect
        if (cfg.WifiEnabled && !string.IsNullOrEmpty(cfg.WifiSsid))
        {
            // Write WiFi profile XML and import via netsh
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"echo ^&lt;WLANProfile xmlns=^&quot;http://www.microsoft.com/networking/WLAN/profile/v1^&quot;^&gt;^&lt;name^&gt;{cfg.WifiSsid}^&lt;/name^&gt;^&lt;SSIDConfig^&gt;^&lt;SSID^&gt;^&lt;name^&gt;{cfg.WifiSsid}^&lt;/name^&gt;^&lt;/SSID^&gt;^&lt;/SSIDConfig^&gt;^&lt;connectionType^&gt;ESS^&lt;/connectionType^&gt;^&lt;connectionMode^&gt;auto^&lt;/connectionMode^&gt;^&lt;MSM^&gt;^&lt;security^&gt;^&lt;authEncryption^&gt;^&lt;authentication^&gt;{cfg.WifiSecurity}^&lt;/authentication^&gt;^&lt;encryption^&gt;AES^&lt;/encryption^&gt;^&lt;useOneX^&gt;false^&lt;/useOneX^&gt;^&lt;/authEncryption^&gt;^&lt;sharedKey^&gt;^&lt;keyType^&gt;passPhrase^&lt;/keyType^&gt;^&lt;protected^&gt;false^&lt;/protected^&gt;^&lt;keyMaterial^&gt;{cfg.WifiPassword}^&lt;/keyMaterial^&gt;^&lt;/sharedKey^&gt;^&lt;/security^&gt;^&lt;/MSM^&gt;^&lt;/WLANProfile^&gt; > %TEMP%\\wifi.xml ^&amp;^&amp; netsh wlan add profile filename=%TEMP%\\wifi.xml ^&amp;^&amp; netsh wlan connect name={cfg.WifiSsid}\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        // OneDrive
        if (cfg.DisableOneDrive)
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OneDrive\" /v DisableFileSyncNGSC /t REG_DWORD /d 1 /f</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");

        // Bloatware removal
        foreach (var pkg in cfg.BloatwareToRemove)
        {
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>powershell.exe -NoProfile -Command \"Get-AppxPackage -AllUsers *{pkg}* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>powershell.exe -NoProfile -Command \"Get-AppxProvisionedPackage -Online | Where-Object {{$_.PackageName -like '*{pkg}*'}} | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        // Driver install from USB (auto-detect install drive)
        if (cfg.IncludeDrivers && cfg.Drivers.Count > 0)
        {
            sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\AuraCoreDrivers\\*.inf (for /r %%d:\\AuraCoreDrivers %%f in (*.inf) do pnputil /add-driver %%f /install)\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
        }

        // Auto-run PostInstall.ps1 from USB root
        sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\PostInstall.ps1 (powershell.exe -ExecutionPolicy Bypass -File %%d:\\PostInstall.ps1)\"</CommandLine><RequiresUserInput>false</RequiresUserInput><Description>AuraCore PostInstall</Description></SynchronousCommand>");

        // Auto-run EXE installers from USB
        if (cfg.ExePaths.Count > 0)
        {
            foreach (var exe in cfg.ExePaths)
            {
                var fn = System.IO.Path.GetFileName(exe);
                var ext = System.IO.Path.GetExtension(exe).ToLower();
                var silentArgs = cfg.SilentInstall ? (ext == ".msi" ? "/quiet /norestart" : "/S /silent /quiet /VERYSILENT /SUPPRESSMSGBOXES") : "";

                if (ext == ".msi")
                    sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\AuraCoreInstallers\\{fn} (msiexec /i %%d:\\AuraCoreInstallers\\{fn} {silentArgs})\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
                else
                    sb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{cmdOrder++}</Order><CommandLine>cmd /c \"for %%d in (C D E F G H) do if exist %%d:\\AuraCoreInstallers\\{fn} (%%d:\\AuraCoreInstallers\\{fn} {silentArgs})\"</CommandLine><RequiresUserInput>false</RequiresUserInput></SynchronousCommand>");
            }
        }

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
        sb.AppendLine("# ═══════════════════════════════════════════════════");
        sb.AppendLine("# AuraCore Pro — Post-Install Script");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("# ═══════════════════════════════════════════════════");
        sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
        sb.AppendLine("Write-Host '=== AuraCore Pro Post-Install ===' -ForegroundColor Cyan");
        sb.AppendLine();

        // Default Preset
        if (cfg.PresetDefault)
        {
            sb.AppendLine("# [DEFAULT PRESET]");
            sb.AppendLine("Write-Host '[+] Default (Balanced)...' -ForegroundColor Green");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'ShowTaskViewButton' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarMn' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'RotatingLockScreenOverlayEnabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-338387Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-338388Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SoftLandingEnabled' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }

        if (cfg.PresetGaming)
        {
            sb.AppendLine("# [GAMING]");
            sb.AppendLine("Write-Host '[+] Gaming preset...' -ForegroundColor Green");
            sb.AppendLine("New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\GameBar' -Name 'AutoGameModeEnabled' -Value 1 -Type DWord -Force | Out-Null");
            sb.AppendLine("$p='HKCU:\\System\\GameConfigStore'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'GameDVR_Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'GameDVR_FSEBehavior' -Value 2 -Type DWord -Force");
            sb.AppendLine("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2>$null");
            sb.AppendLine();
        }

        if (cfg.PresetPrivacy)
        {
            sb.AppendLine("# [PRIVACY]");
            sb.AppendLine("Write-Host '[+] Privacy preset...' -ForegroundColor Green");
            sb.AppendLine("$p='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path $p -Name 'PublishUserActivities' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SilentInstalledAppsEnabled' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }

        if (cfg.PresetDev)
        {
            sb.AppendLine("# [DEVELOPER]");
            sb.AppendLine("Write-Host '[+] Developer preset...' -ForegroundColor Green");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'HideFileExt' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Hidden' -Value 1 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name 'LongPathsEnabled' -Value 1 -Type DWord -Force");
            sb.AppendLine("try { winget install Git.Git --accept-source-agreements --accept-package-agreements -h 2>$null } catch {}");
            sb.AppendLine();
        }

        if (cfg.PresetOffice)
        {
            sb.AppendLine("# [OFFICE]");
            sb.AppendLine("Write-Host '[+] Office preset...' -ForegroundColor Green");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'AppsUseLightTheme' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'SystemUsesLightTheme' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarDa' -Value 0 -Type DWord -Force");
            sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'ShowTaskViewButton' -Value 0 -Type DWord -Force");
            sb.AppendLine();
        }

        // Post-install actions
        foreach (var a in cfg.PostInstallActions)
        {
            switch (a)
            {
                case "dark_mode": sb.AppendLine("Write-Host '[+] Dark Mode' -ForegroundColor Green"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'AppsUseLightTheme' -Value 0 -Type DWord -Force"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'SystemUsesLightTheme' -Value 0 -Type DWord -Force"); break;
                case "file_extensions": sb.AppendLine("Write-Host '[+] File extensions' -ForegroundColor Green"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'HideFileExt' -Value 0 -Type DWord -Force"); break;
                case "hidden_files": sb.AppendLine("Write-Host '[+] Hidden files' -ForegroundColor Green"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Hidden' -Value 1 -Type DWord -Force"); break;
                case "disable_web_search": sb.AppendLine("Write-Host '[+] Disable web search' -ForegroundColor Green"); sb.AppendLine("$p='HKCU:\\Software\\Policies\\Microsoft\\Windows\\Explorer'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}; Set-ItemProperty -Path $p -Name 'DisableSearchBoxSuggestions' -Value 1 -Type DWord -Force"); break;
                case "classic_context_menu": sb.AppendLine("Write-Host '[+] Classic context menu' -ForegroundColor Green"); sb.AppendLine("reg add 'HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32' /f /ve 2>$null"); break;
                case "disable_widgets": sb.AppendLine("Write-Host '[+] Disable Widgets' -ForegroundColor Green"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarDa' -Value 0 -Type DWord -Force"); break;
                case "disable_copilot": sb.AppendLine("Write-Host '[+] Disable Copilot' -ForegroundColor Green"); sb.AppendLine("$p='HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot'; if(!(Test-Path $p)){New-Item $p -Force|Out-Null}; Set-ItemProperty -Path $p -Name 'TurnOffWindowsCopilot' -Value 1 -Type DWord -Force"); break;
                case "taskbar_left": sb.AppendLine("Write-Host '[+] Taskbar left' -ForegroundColor Green"); sb.AppendLine("Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarAl' -Value 0 -Type DWord -Force"); break;
                case "install_winget": sb.AppendLine("Write-Host '[+] Winget' -ForegroundColor Green"); sb.AppendLine("try { $h=Get-Command winget -EA SilentlyContinue; if(!$h) { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -EA SilentlyContinue } } catch {}"); break;
            }
            sb.AppendLine();
        }

        // Winget apps
        if (cfg.WingetApps.Count > 0)
        {
            sb.AppendLine("# [WINGET APPS]");
            sb.AppendLine("Write-Host '[+] Installing apps via Winget...' -ForegroundColor Cyan");
            foreach (var app in cfg.WingetApps)
            {
                sb.AppendLine($"Write-Host '    {app}...' -ForegroundColor Gray");
                sb.AppendLine($"try {{ winget install {app} --accept-source-agreements --accept-package-agreements -h 2>$null }} catch {{}}");
            }
            sb.AppendLine();
        }

        // WiFi
        if (cfg.WifiEnabled && !string.IsNullOrEmpty(cfg.WifiSsid))
        {
            sb.AppendLine("# [WIFI AUTO-CONNECT]");
            sb.AppendLine("Write-Host '[+] Configuring WiFi...' -ForegroundColor Green");
            sb.AppendLine("$wifiXml = @'");
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">");
            sb.AppendLine($"  <name>{cfg.WifiSsid}</name>");
            sb.AppendLine($"  <SSIDConfig><SSID><name>{cfg.WifiSsid}</name></SSID></SSIDConfig>");
            sb.AppendLine("  <connectionType>ESS</connectionType>");
            sb.AppendLine("  <connectionMode>auto</connectionMode>");
            if (cfg.WifiSecurity == "open")
            {
                sb.AppendLine("  <MSM><security><authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption></security></MSM>");
            }
            else
            {
                sb.AppendLine($"  <MSM><security><authEncryption><authentication>{cfg.WifiSecurity}</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption>");
                sb.AppendLine($"    <sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{cfg.WifiPassword}</keyMaterial></sharedKey>");
                sb.AppendLine("  </security></MSM>");
            }
            sb.AppendLine("</WLANProfile>");
            sb.AppendLine("'@");
            sb.AppendLine("$wifiPath = Join-Path $env:TEMP 'AuraCoreWifi.xml'");
            sb.AppendLine("$wifiXml | Out-File -FilePath $wifiPath -Encoding UTF8");
            sb.AppendLine("netsh wlan add profile filename=$wifiPath 2>$null");
            sb.AppendLine($"netsh wlan connect name='{cfg.WifiSsid}' 2>$null");
            sb.AppendLine("Remove-Item $wifiPath -Force -EA SilentlyContinue");
            sb.AppendLine($"Write-Host '    Connected to {cfg.WifiSsid}' -ForegroundColor Gray");
            sb.AppendLine();
        }

        // Restart Explorer
        sb.AppendLine("# Restart Explorer");
        sb.AppendLine("Stop-Process -Name explorer -Force -EA SilentlyContinue; Start-Sleep 2; Start-Process explorer");
        sb.AppendLine("Write-Host '' -ForegroundColor Green");
        sb.AppendLine("Write-Host '=== All done! Powered by AuraCore Pro ===' -ForegroundColor Green");
        return sb.ToString();
    }
}
