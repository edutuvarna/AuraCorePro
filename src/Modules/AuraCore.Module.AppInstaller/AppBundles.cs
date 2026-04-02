using AuraCore.Module.AppInstaller.Models;

namespace AuraCore.Module.AppInstaller;

public static class AppBundles
{
    public static List<AppBundle> GetAll() => new()
    {
        new AppBundle
        {
            Name = "Essential Apps",
            Description = "Must-have apps for every Windows PC",
            Icon = "E74C",
            Apps = new()
            {
                new() { WinGetId = "Google.Chrome", Name = "Google Chrome", Description = "Fast, widely used web browser" },
                new() { WinGetId = "Mozilla.Firefox", Name = "Firefox", Description = "Privacy-focused web browser" },
                new() { WinGetId = "Brave.Brave", Name = "Brave Browser", Description = "Ad-blocking privacy browser" },
                new() { WinGetId = "7zip.7zip", Name = "7-Zip", Description = "Free file archiver — opens ZIP, RAR, 7z" },
                new() { WinGetId = "RARLab.WinRAR", Name = "WinRAR", Description = "Popular archive manager" },
                new() { WinGetId = "Notepad++.Notepad++", Name = "Notepad++", Description = "Advanced text editor" },
                new() { WinGetId = "VideoLAN.VLC", Name = "VLC Media Player", Description = "Plays any video/audio format" },
                new() { WinGetId = "Adobe.Acrobat.Reader.64-bit", Name = "Adobe Reader", Description = "PDF viewer" },
                new() { WinGetId = "SumatraPDF.SumatraPDF", Name = "SumatraPDF", Description = "Lightweight PDF reader" },
                new() { WinGetId = "IrfanSkalworker.IrfanView", Name = "IrfanView", Description = "Fast image viewer" },
                new() { WinGetId = "Telegram.TelegramDesktop", Name = "Telegram", Description = "Fast messaging app" },
            }
        },
        new AppBundle
        {
            Name = "Developer Tools",
            Description = "IDEs, runtimes, and dev utilities",
            Icon = "E943",
            Apps = new()
            {
                new() { WinGetId = "Microsoft.VisualStudioCode", Name = "VS Code", Description = "Lightweight code editor" },
                new() { WinGetId = "Git.Git", Name = "Git", Description = "Version control system" },
                new() { WinGetId = "Microsoft.WindowsTerminal", Name = "Windows Terminal", Description = "Modern terminal app" },
                new() { WinGetId = "Python.Python.3.12", Name = "Python 3.12", Description = "Programming language" },
                new() { WinGetId = "OpenJS.NodeJS.LTS", Name = "Node.js LTS", Description = "JavaScript runtime" },
                new() { WinGetId = "Docker.DockerDesktop", Name = "Docker Desktop", Description = "Container platform" },
                new() { WinGetId = "JetBrains.IntelliJIDEA.Community", Name = "IntelliJ IDEA CE", Description = "Java IDE" },
                new() { WinGetId = "JetBrains.PyCharm.Community", Name = "PyCharm CE", Description = "Python IDE" },
                new() { WinGetId = "JetBrains.WebStorm", Name = "WebStorm", Description = "JavaScript/TypeScript IDE" },
                new() { WinGetId = "JetBrains.Rider", Name = "Rider", Description = ".NET/C# IDE" },
                new() { WinGetId = "Postman.Postman", Name = "Postman", Description = "API testing tool" },
                new() { WinGetId = "Insomnia.Insomnia", Name = "Insomnia", Description = "REST/GraphQL API client" },
                new() { WinGetId = "GoLang.Go", Name = "Go", Description = "Go programming language" },
                new() { WinGetId = "Rustlang.Rustup", Name = "Rust", Description = "Rust programming language" },
                new() { WinGetId = "CoreyButler.NVMforWindows", Name = "NVM for Windows", Description = "Node version manager" },
                new() { WinGetId = "Microsoft.DotNet.SDK.8", Name = ".NET SDK 8", Description = "C#/.NET development kit" },
                new() { WinGetId = "Oracle.JavaRuntimeEnvironment", Name = "Java Runtime", Description = "Java JRE" },
                new() { WinGetId = "GitHub.GitHubDesktop", Name = "GitHub Desktop", Description = "Git GUI for GitHub" },
                new() { WinGetId = "Atlassian.Sourcetree", Name = "Sourcetree", Description = "Git GUI for Bitbucket" },
                new() { WinGetId = "WinSCP.WinSCP", Name = "WinSCP", Description = "SFTP/FTP client" },
                new() { WinGetId = "PuTTY.PuTTY", Name = "PuTTY", Description = "SSH terminal client" },
                new() { WinGetId = "Canonical.Ubuntu.2204", Name = "Ubuntu (WSL)", Description = "Linux on Windows via WSL" },
                new() { WinGetId = "DBBrowserForSQLite.DBBrowserForSQLite", Name = "DB Browser SQLite", Description = "SQLite database viewer" },
                new() { WinGetId = "dbeaver.dbeaver", Name = "DBeaver", Description = "Universal database manager" },
                new() { WinGetId = "RedHat.Podman", Name = "Podman", Description = "Docker alternative" },
                new() { WinGetId = "Figma.Figma", Name = "Figma", Description = "UI design tool" },
            }
        },
        new AppBundle
        {
            Name = "Productivity & Office",
            Description = "Office suites, notes, and workflow tools",
            Icon = "E8A1",
            Apps = new()
            {
                new() { WinGetId = "Notion.Notion", Name = "Notion", Description = "All-in-one workspace" },
                new() { WinGetId = "Obsidian.Obsidian", Name = "Obsidian", Description = "Knowledge base with Markdown" },
                new() { WinGetId = "Zoom.Zoom", Name = "Zoom", Description = "Video conferencing" },
                new() { WinGetId = "SlackTechnologies.Slack", Name = "Slack", Description = "Team messaging" },
                new() { WinGetId = "Microsoft.PowerToys", Name = "PowerToys", Description = "Windows power user utilities" },
                new() { WinGetId = "voidtools.Everything", Name = "Everything", Description = "Instant file search" },
                new() { WinGetId = "TheDocumentFoundation.LibreOffice", Name = "LibreOffice", Description = "Free office suite (Word/Excel/PPT alternative)" },
                new() { WinGetId = "Apache.OpenOffice", Name = "OpenOffice", Description = "Open-source office suite" },
                new() { WinGetId = "Trello.Trello", Name = "Trello", Description = "Kanban project management" },
                new() { WinGetId = "Todoist.Todoist", Name = "Todoist", Description = "Task management app" },
                new() { WinGetId = "Grammarly.Grammarly", Name = "Grammarly", Description = "Writing assistant" },
                new() { WinGetId = "Evernote.Evernote", Name = "Evernote", Description = "Note-taking app" },
                new() { WinGetId = "StandardNotes.StandardNotes", Name = "Standard Notes", Description = "Encrypted note-taking" },
                new() { WinGetId = "AnyDeskSoftwareGmbH.AnyDesk", Name = "AnyDesk", Description = "Remote desktop access" },
                new() { WinGetId = "TeamViewer.TeamViewer", Name = "TeamViewer", Description = "Remote support & access" },
                new() { WinGetId = "Google.Drive", Name = "Google Drive", Description = "Cloud storage client" },
                new() { WinGetId = "Dropbox.Dropbox", Name = "Dropbox", Description = "Cloud file sync" },
            }
        },
        new AppBundle
        {
            Name = "Media & Creative",
            Description = "Photo, video, audio, and design tools",
            Icon = "E8B9",
            Apps = new()
            {
                new() { WinGetId = "GIMP.GIMP", Name = "GIMP", Description = "Free image editor (Photoshop alternative)" },
                new() { WinGetId = "KDE.Kdenlive", Name = "Kdenlive", Description = "Free video editor" },
                new() { WinGetId = "Audacity.Audacity", Name = "Audacity", Description = "Free audio editor" },
                new() { WinGetId = "OBSProject.OBSStudio", Name = "OBS Studio", Description = "Screen recording & streaming" },
                new() { WinGetId = "BlenderFoundation.Blender", Name = "Blender", Description = "3D modeling & animation" },
                new() { WinGetId = "HandBrake.HandBrake", Name = "HandBrake", Description = "Video converter" },
                new() { WinGetId = "Inkscape.Inkscape", Name = "Inkscape", Description = "Vector graphics editor (Illustrator alternative)" },
                new() { WinGetId = "darktable.darktable", Name = "darktable", Description = "Photo workflow & RAW editor" },
                new() { WinGetId = "Krita.Krita", Name = "Krita", Description = "Digital painting app" },
                new() { WinGetId = "ShareX.ShareX", Name = "ShareX", Description = "Screenshot & screen recorder" },
                new() { WinGetId = "Greenshot.Greenshot", Name = "Greenshot", Description = "Simple screenshot tool" },
                new() { WinGetId = "FFmpeg.FFmpeg", Name = "FFmpeg", Description = "Command-line audio/video toolkit" },
                new() { WinGetId = "MusicBee.MusicBee", Name = "MusicBee", Description = "Music player & organizer" },
                new() { WinGetId = "Spotify.Spotify", Name = "Spotify", Description = "Music streaming" },
                new() { WinGetId = "DaVinciResolve.DaVinciResolve", Name = "DaVinci Resolve", Description = "Professional video editor (free)" },
            }
        },
        new AppBundle
        {
            Name = "Gaming",
            Description = "Game launchers, chat, and gaming tools",
            Icon = "E7FC",
            Apps = new()
            {
                new() { WinGetId = "Valve.Steam", Name = "Steam", Description = "Game store and launcher" },
                new() { WinGetId = "EpicGames.EpicGamesLauncher", Name = "Epic Games", Description = "Game store" },
                new() { WinGetId = "Discord.Discord", Name = "Discord", Description = "Voice chat for gamers" },
                new() { WinGetId = "Nvidia.GeForceExperience", Name = "GeForce Experience", Description = "Nvidia GPU drivers & optimization" },
                new() { WinGetId = "GOG.Galaxy", Name = "GOG Galaxy", Description = "DRM-free game launcher" },
                new() { WinGetId = "PrismLauncher.PrismLauncher", Name = "Prism Launcher", Description = "Minecraft launcher" },
                new() { WinGetId = "ElectronicArts.EADesktop", Name = "EA App", Description = "Electronic Arts game launcher" },
                new() { WinGetId = "Ubisoft.Connect", Name = "Ubisoft Connect", Description = "Ubisoft game launcher" },
                new() { WinGetId = "Playnite.Playnite", Name = "Playnite", Description = "Unified game library manager" },
                new() { WinGetId = "MSI.Afterburner", Name = "MSI Afterburner", Description = "GPU overclock & monitoring" },
            }
        },
        new AppBundle
        {
            Name = "Security & Privacy",
            Description = "Antivirus, VPN, and privacy tools",
            Icon = "E72E",
            Apps = new()
            {
                new() { WinGetId = "Bitwarden.Bitwarden", Name = "Bitwarden", Description = "Free password manager" },
                new() { WinGetId = "KeePassXCTeam.KeePassXC", Name = "KeePassXC", Description = "Offline password manager" },
                new() { WinGetId = "Malwarebytes.Malwarebytes", Name = "Malwarebytes", Description = "Malware scanner" },
                new() { WinGetId = "ProtonTechnologies.ProtonVPN", Name = "ProtonVPN", Description = "Free VPN service" },
                new() { WinGetId = "WireGuard.WireGuard", Name = "WireGuard", Description = "Fast VPN protocol" },
                new() { WinGetId = "Cryptomator.Cryptomator", Name = "Cryptomator", Description = "Cloud storage encryption" },
                new() { WinGetId = "VeraCrypt.VeraCrypt", Name = "VeraCrypt", Description = "Disk encryption" },
                new() { WinGetId = "IDRIX.VeraCrypt", Name = "VeraCrypt (alt)", Description = "Full disk encryption" },
                new() { WinGetId = "Mullvad.MullvadVPN", Name = "Mullvad VPN", Description = "No-log VPN service" },
                new() { WinGetId = "GlassWire.GlassWire", Name = "GlassWire", Description = "Network monitor & firewall" },
            }
        },
        new AppBundle
        {
            Name = "System Utilities",
            Description = "Disk tools, monitors, tweakers, and maintenance",
            Icon = "E912",
            Apps = new()
            {
                new() { WinGetId = "CrystalDewWorld.CrystalDiskInfo", Name = "CrystalDiskInfo", Description = "HDD/SSD health monitor" },
                new() { WinGetId = "CrystalDewWorld.CrystalDiskMark", Name = "CrystalDiskMark", Description = "Disk speed benchmark" },
                new() { WinGetId = "CPUID.CPU-Z", Name = "CPU-Z", Description = "CPU & hardware info" },
                new() { WinGetId = "CPUID.HWMonitor", Name = "HWMonitor", Description = "Hardware temperature monitor" },
                new() { WinGetId = "REALiX.HWiNFO", Name = "HWiNFO", Description = "Detailed hardware analysis" },
                new() { WinGetId = "WinDirStat.WinDirStat", Name = "WinDirStat", Description = "Disk space usage visualizer" },
                new() { WinGetId = "TreeSizeFree.TreeSizeFree", Name = "TreeSize Free", Description = "Folder size analyzer" },
                new() { WinGetId = "Rufus.Rufus", Name = "Rufus", Description = "Bootable USB creator" },
                new() { WinGetId = "Ventoy.Ventoy", Name = "Ventoy", Description = "Multi-boot USB tool" },
                new() { WinGetId = "TechPowerUp.GPU-Z", Name = "GPU-Z", Description = "GPU info & monitoring" },
                new() { WinGetId = "SysInternals.ProcessExplorer", Name = "Process Explorer", Description = "Advanced task manager (Sysinternals)" },
                new() { WinGetId = "SysInternals.Autoruns", Name = "Autoruns", Description = "Startup program manager (Sysinternals)" },
                new() { WinGetId = "Piriform.CCleaner", Name = "CCleaner", Description = "System cleaner" },
                new() { WinGetId = "Balena.Etcher", Name = "balenaEtcher", Description = "Flash OS images to USB/SD" },
                new() { WinGetId = "GeekUninstaller.GeekUninstaller", Name = "Geek Uninstaller", Description = "Clean app uninstaller" },
            }
        },
        new AppBundle
        {
            Name = "Communication",
            Description = "Messaging, email, and social apps",
            Icon = "E8BD",
            Apps = new()
            {
                new() { WinGetId = "Discord.Discord", Name = "Discord", Description = "Voice & text chat" },
                new() { WinGetId = "Telegram.TelegramDesktop", Name = "Telegram", Description = "Fast messaging app" },
                new() { WinGetId = "WhatsApp.WhatsApp", Name = "WhatsApp", Description = "Messaging (Meta)" },
                new() { WinGetId = "Signal.Signal", Name = "Signal", Description = "Encrypted messaging" },
                new() { WinGetId = "Mozilla.Thunderbird", Name = "Thunderbird", Description = "Free email client" },
                new() { WinGetId = "Element.Element", Name = "Element", Description = "Matrix chat client" },
                new() { WinGetId = "Zoom.Zoom", Name = "Zoom", Description = "Video conferencing" },
                new() { WinGetId = "Microsoft.Skype", Name = "Skype", Description = "Video calling" },
            }
        },
        new AppBundle
        {
            Name = "Web Browsers",
            Description = "All major browsers in one place",
            Icon = "E774",
            Apps = new()
            {
                new() { WinGetId = "Google.Chrome", Name = "Google Chrome", Description = "Most popular browser" },
                new() { WinGetId = "Mozilla.Firefox", Name = "Firefox", Description = "Privacy-focused browser" },
                new() { WinGetId = "Brave.Brave", Name = "Brave", Description = "Ad-blocking privacy browser" },
                new() { WinGetId = "OperaSoftware.OperaGX", Name = "Opera GX", Description = "Gaming browser" },
                new() { WinGetId = "Vivaldi.Vivaldi", Name = "Vivaldi", Description = "Highly customizable browser" },
                new() { WinGetId = "ArcBrowser.Arc", Name = "Arc", Description = "Modern tab management browser" },
                new() { WinGetId = "AbleBits.TorBrowser", Name = "Tor Browser", Description = "Anonymous browsing" },
                new() { WinGetId = "LibreWolf.LibreWolf", Name = "LibreWolf", Description = "Privacy-hardened Firefox fork" },
            }
        },
        new AppBundle
        {
            Name = "Education & Science",
            Description = "Learning, math, research, and academic tools",
            Icon = "E82D",
            Apps = new()
            {
                new() { WinGetId = "Anki.Anki", Name = "Anki", Description = "Spaced repetition flashcards" },
                new() { WinGetId = "Zotero.Zotero", Name = "Zotero", Description = "Research reference manager" },
                new() { WinGetId = "GeoGebra.GeoGebra", Name = "GeoGebra", Description = "Math visualization tool" },
                new() { WinGetId = "GNU.Octave", Name = "GNU Octave", Description = "MATLAB alternative" },
                new() { WinGetId = "RProject.R", Name = "R Language", Description = "Statistical computing" },
                new() { WinGetId = "RStudio.RStudio.OpenSource", Name = "RStudio", Description = "R IDE" },
                new() { WinGetId = "Stellarium.Stellarium", Name = "Stellarium", Description = "Astronomy planetarium" },
            }
        },
    };
}
