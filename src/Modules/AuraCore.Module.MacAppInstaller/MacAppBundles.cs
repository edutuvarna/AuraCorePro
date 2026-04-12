using AuraCore.Module.MacAppInstaller.Models;

namespace AuraCore.Module.MacAppInstaller;

public static class MacAppBundles
{
    public static readonly IReadOnlyList<MacAppBundle> AllBundles = new List<MacAppBundle>
    {
        // ========== 1. Essential Apps (12) ==========
        new()
        {
            Name = "Essential Apps",
            Description = "Core applications every Mac user needs",
            Icon = "E774",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "firefox",          Name = "Firefox",          Description = "Popular open-source browser",     Source = MacPackageSource.BrewCask,    PackageName = "firefox" },
                new() { Id = "google-chrome",    Name = "Google Chrome",    Description = "Google's browser",                Source = MacPackageSource.BrewCask,    PackageName = "google-chrome" },
                new() { Id = "brave-browser",    Name = "Brave",            Description = "Privacy-focused browser",         Source = MacPackageSource.BrewCask,    PackageName = "brave-browser" },
                new() { Id = "vlc",              Name = "VLC",              Description = "Plays any video/audio format",    Source = MacPackageSource.BrewCask,    PackageName = "vlc" },
                new() { Id = "libreoffice",      Name = "LibreOffice",      Description = "Free office suite",               Source = MacPackageSource.BrewCask,    PackageName = "libreoffice" },
                new() { Id = "gimp",             Name = "GIMP",             Description = "Image editor (Photoshop alt)",    Source = MacPackageSource.BrewCask,    PackageName = "gimp" },
                new() { Id = "thunderbird",      Name = "Thunderbird",      Description = "Email client",                    Source = MacPackageSource.BrewCask,    PackageName = "thunderbird" },
                new() { Id = "the-unarchiver",   Name = "The Unarchiver",   Description = "Archive extractor",               Source = MacPackageSource.BrewCask,    PackageName = "the-unarchiver" },
                new() { Id = "transmission",     Name = "Transmission",     Description = "BitTorrent client",               Source = MacPackageSource.BrewCask,    PackageName = "transmission" },
                new() { Id = "appcleaner",       Name = "AppCleaner",       Description = "Uninstaller with leftover scan",  Source = MacPackageSource.BrewCask,    PackageName = "appcleaner" },
                new() { Id = "keka",             Name = "Keka",             Description = "File archiver (7z/zip/tar)",      Source = MacPackageSource.BrewCask,    PackageName = "keka" },
                new() { Id = "rectangle",        Name = "Rectangle",        Description = "Window snapping tool",            Source = MacPackageSource.BrewCask,    PackageName = "rectangle" },
            }
        },

        // ========== 2. Developer Tools (28) ==========
        new()
        {
            Name = "Developer Tools",
            Description = "Languages, IDEs, and development utilities",
            Icon = "E943",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "vscode",             Name = "VS Code",              Description = "Code editor by Microsoft",  Source = MacPackageSource.BrewCask,    PackageName = "visual-studio-code" },
                new() { Id = "iterm2",             Name = "iTerm2",               Description = "Advanced Terminal replacement", Source = MacPackageSource.BrewCask, PackageName = "iterm2" },
                new() { Id = "git",                Name = "Git",                  Description = "Version control",           Source = MacPackageSource.BrewFormula, PackageName = "git" },
                new() { Id = "node",               Name = "Node.js",              Description = "JavaScript runtime",        Source = MacPackageSource.BrewFormula, PackageName = "node" },
                new() { Id = "python",             Name = "Python 3",             Description = "Python language",           Source = MacPackageSource.BrewFormula, PackageName = "python@3.12" },
                new() { Id = "go",                 Name = "Go",                   Description = "Go language",               Source = MacPackageSource.BrewFormula, PackageName = "go" },
                new() { Id = "rust",               Name = "Rust",                 Description = "Rust language",             Source = MacPackageSource.BrewFormula, PackageName = "rust" },
                new() { Id = "openjdk",            Name = "OpenJDK",              Description = "Java Development Kit",      Source = MacPackageSource.BrewFormula, PackageName = "openjdk" },
                new() { Id = "maven",              Name = "Maven",                Description = "Java build tool",           Source = MacPackageSource.BrewFormula, PackageName = "maven" },
                new() { Id = "gradle",             Name = "Gradle",               Description = "Build automation",          Source = MacPackageSource.BrewFormula, PackageName = "gradle" },
                new() { Id = "cmake",              Name = "CMake",                Description = "Cross-platform build",      Source = MacPackageSource.BrewFormula, PackageName = "cmake" },
                new() { Id = "docker",             Name = "Docker Desktop",       Description = "Container platform",        Source = MacPackageSource.BrewCask,    PackageName = "docker" },
                new() { Id = "dbeaver",            Name = "DBeaver",              Description = "Universal database tool",   Source = MacPackageSource.BrewCask,    PackageName = "dbeaver-community" },
                new() { Id = "postman",            Name = "Postman",              Description = "API testing tool",          Source = MacPackageSource.BrewCask,    PackageName = "postman" },
                new() { Id = "insomnia",           Name = "Insomnia",             Description = "REST client",               Source = MacPackageSource.BrewCask,    PackageName = "insomnia" },
                new() { Id = "sublime-text",       Name = "Sublime Text",         Description = "Fast text editor",          Source = MacPackageSource.BrewCask,    PackageName = "sublime-text" },
                new() { Id = "intellij",           Name = "IntelliJ IDEA CE",     Description = "Java IDE (community)",      Source = MacPackageSource.BrewCask,    PackageName = "intellij-idea-ce" },
                new() { Id = "jetbrains-toolbox",  Name = "JetBrains Toolbox",    Description = "JetBrains IDE manager",     Source = MacPackageSource.BrewCask,    PackageName = "jetbrains-toolbox" },
                new() { Id = "sourcetree",         Name = "Sourcetree",           Description = "Git GUI client",            Source = MacPackageSource.BrewCask,    PackageName = "sourcetree" },
                new() { Id = "github",             Name = "GitHub Desktop",       Description = "GitHub GUI client",         Source = MacPackageSource.BrewCask,    PackageName = "github" },
                new() { Id = "wireshark",          Name = "Wireshark",            Description = "Network protocol analyzer", Source = MacPackageSource.BrewCask,    PackageName = "wireshark" },
                new() { Id = "curl",               Name = "curl",                 Description = "HTTP client",               Source = MacPackageSource.BrewFormula, PackageName = "curl" },
                new() { Id = "wget",               Name = "wget",                 Description = "File downloader",           Source = MacPackageSource.BrewFormula, PackageName = "wget" },
                new() { Id = "jq",                 Name = "jq",                   Description = "JSON processor",            Source = MacPackageSource.BrewFormula, PackageName = "jq" },
                new() { Id = "tmux",               Name = "tmux",                 Description = "Terminal multiplexer",      Source = MacPackageSource.BrewFormula, PackageName = "tmux" },
                new() { Id = "httpie",             Name = "HTTPie",               Description = "Human-friendly HTTP CLI",   Source = MacPackageSource.BrewFormula, PackageName = "httpie" },
                new() { Id = "fork",               Name = "Fork",                 Description = "Fast Git client",           Source = MacPackageSource.BrewCask,    PackageName = "fork" },
                new() { Id = "tableplus",          Name = "TablePlus",            Description = "Modern database GUI",       Source = MacPackageSource.BrewCask,    PackageName = "tableplus" },
            }
        },

        // ========== 3. Productivity & Office (18) ==========
        new()
        {
            Name = "Productivity & Office",
            Description = "Notes, docs, collaboration, and office tools",
            Icon = "E77F",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "obsidian",          Name = "Obsidian",         Description = "Knowledge base with links",    Source = MacPackageSource.BrewCask, PackageName = "obsidian" },
                new() { Id = "notion",            Name = "Notion",           Description = "All-in-one workspace",         Source = MacPackageSource.BrewCask, PackageName = "notion" },
                new() { Id = "ms-office",         Name = "Microsoft Office", Description = "Word, Excel, PowerPoint",      Source = MacPackageSource.BrewCask, PackageName = "microsoft-office" },
                new() { Id = "libreoffice-full",  Name = "LibreOffice",      Description = "Free office suite",            Source = MacPackageSource.BrewCask, PackageName = "libreoffice" },
                new() { Id = "onlyoffice",        Name = "OnlyOffice",       Description = "MS Office compatible",         Source = MacPackageSource.BrewCask, PackageName = "onlyoffice" },
                new() { Id = "zoom",              Name = "Zoom",             Description = "Video conferencing",           Source = MacPackageSource.BrewCask, PackageName = "zoom" },
                new() { Id = "slack",             Name = "Slack",            Description = "Team messaging",               Source = MacPackageSource.BrewCask, PackageName = "slack" },
                new() { Id = "microsoft-teams",   Name = "Microsoft Teams",  Description = "Team collaboration",           Source = MacPackageSource.BrewCask, PackageName = "microsoft-teams" },
                new() { Id = "joplin",            Name = "Joplin",           Description = "Note-taking app",              Source = MacPackageSource.BrewCask, PackageName = "joplin" },
                new() { Id = "simplenote",        Name = "Simplenote",       Description = "Simple note-taking",           Source = MacPackageSource.BrewCask, PackageName = "simplenote" },
                new() { Id = "standard-notes",    Name = "Standard Notes",   Description = "Encrypted notes",              Source = MacPackageSource.BrewCask, PackageName = "standard-notes" },
                new() { Id = "todoist",           Name = "Todoist",          Description = "Task manager",                 Source = MacPackageSource.BrewCask, PackageName = "todoist" },
                new() { Id = "1password",         Name = "1Password",        Description = "Password manager",             Source = MacPackageSource.BrewCask, PackageName = "1password" },
                new() { Id = "bitwarden",         Name = "Bitwarden",        Description = "Open-source password manager", Source = MacPackageSource.BrewCask, PackageName = "bitwarden" },
                new() { Id = "calibre",           Name = "Calibre",          Description = "E-book manager",               Source = MacPackageSource.BrewCask, PackageName = "calibre" },
                new() { Id = "adobe-reader",      Name = "Adobe Acrobat Reader", Description = "PDF reader",               Source = MacPackageSource.BrewCask, PackageName = "adobe-acrobat-reader" },
                new() { Id = "scrivener",         Name = "Scrivener",        Description = "Long-form writing tool",       Source = MacPackageSource.BrewCask, PackageName = "scrivener" },
                new() { Id = "keepingyouawake",   Name = "KeepingYouAwake",  Description = "Prevent Mac from sleeping",    Source = MacPackageSource.BrewCask, PackageName = "keepingyouawake" },
            }
        },

        // ========== 4. Media & Creative (16) ==========
        new()
        {
            Name = "Media & Creative",
            Description = "Image, video, audio editing and design tools",
            Icon = "E8AD",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "gimp-full",      Name = "GIMP",           Description = "Image editor",              Source = MacPackageSource.BrewCask,    PackageName = "gimp" },
                new() { Id = "inkscape",       Name = "Inkscape",       Description = "Vector graphics editor",    Source = MacPackageSource.BrewCask,    PackageName = "inkscape" },
                new() { Id = "blender",        Name = "Blender",        Description = "3D creation suite",         Source = MacPackageSource.BrewCask,    PackageName = "blender" },
                new() { Id = "audacity",       Name = "Audacity",       Description = "Audio editor",              Source = MacPackageSource.BrewCask,    PackageName = "audacity" },
                new() { Id = "obs",            Name = "OBS Studio",     Description = "Screen recording/streaming", Source = MacPackageSource.BrewCask,   PackageName = "obs" },
                new() { Id = "handbrake",      Name = "HandBrake",      Description = "Video transcoder",          Source = MacPackageSource.BrewCask,    PackageName = "handbrake" },
                new() { Id = "davinci-resolve",Name = "DaVinci Resolve",Description = "Pro video editor",          Source = MacPackageSource.BrewCask,    PackageName = "davinci-resolve" },
                new() { Id = "figma",          Name = "Figma",          Description = "Collaborative design",      Source = MacPackageSource.BrewCask,    PackageName = "figma" },
                new() { Id = "krita",          Name = "Krita",          Description = "Digital painting",          Source = MacPackageSource.BrewCask,    PackageName = "krita" },
                new() { Id = "darktable",      Name = "Darktable",      Description = "RAW photo workflow",        Source = MacPackageSource.BrewCask,    PackageName = "darktable" },
                new() { Id = "imageoptim",     Name = "ImageOptim",     Description = "Image compression",         Source = MacPackageSource.BrewCask,    PackageName = "imageoptim" },
                new() { Id = "kap",            Name = "Kap",            Description = "Screen recorder to GIF",    Source = MacPackageSource.BrewCask,    PackageName = "kap" },
                new() { Id = "shotcut",        Name = "Shotcut",        Description = "Cross-platform video editor", Source = MacPackageSource.BrewCask,  PackageName = "shotcut" },
                new() { Id = "ffmpeg",         Name = "FFmpeg",         Description = "Audio/video converter",     Source = MacPackageSource.BrewFormula, PackageName = "ffmpeg" },
                new() { Id = "imagemagick",    Name = "ImageMagick",    Description = "Image manipulation CLI",    Source = MacPackageSource.BrewFormula, PackageName = "imagemagick" },
                new() { Id = "vlc-media",      Name = "VLC",            Description = "Media player",              Source = MacPackageSource.BrewCask,    PackageName = "vlc" },
            }
        },

        // ========== 5. Gaming (10) ==========
        new()
        {
            Name = "Gaming",
            Description = "Game launchers, emulators, and streaming tools",
            Icon = "E7FC",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "steam",         Name = "Steam",           Description = "Valve game platform",       Source = MacPackageSource.BrewCask, PackageName = "steam" },
                new() { Id = "epic-games",    Name = "Epic Games",      Description = "Epic Games Store",          Source = MacPackageSource.BrewCask, PackageName = "epic-games" },
                new() { Id = "gog-galaxy",    Name = "GOG Galaxy",      Description = "GOG store client",          Source = MacPackageSource.BrewCask, PackageName = "gog-galaxy" },
                new() { Id = "battle-net",    Name = "Battle.net",      Description = "Blizzard launcher",         Source = MacPackageSource.BrewCask, PackageName = "battle-net" },
                new() { Id = "discord-game",  Name = "Discord",         Description = "Gaming voice/chat",         Source = MacPackageSource.BrewCask, PackageName = "discord" },
                new() { Id = "minecraft",     Name = "Minecraft",       Description = "Minecraft launcher",        Source = MacPackageSource.BrewCask, PackageName = "minecraft" },
                new() { Id = "openemu",       Name = "OpenEmu",         Description = "Retro game emulator",       Source = MacPackageSource.BrewCask, PackageName = "openemu" },
                new() { Id = "retroarch",     Name = "RetroArch",       Description = "Multi-system emulator",     Source = MacPackageSource.BrewCask, PackageName = "retroarch" },
                new() { Id = "parsec",        Name = "Parsec",          Description = "Game streaming",            Source = MacPackageSource.BrewCask, PackageName = "parsec" },
                new() { Id = "moonlight",     Name = "Moonlight",       Description = "NVIDIA game streaming",     Source = MacPackageSource.BrewCask, PackageName = "moonlight" },
            }
        },

        // ========== 6. Security & Privacy (11) ==========
        new()
        {
            Name = "Security & Privacy",
            Description = "VPN, password managers, encryption, antivirus",
            Icon = "E72E",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "1password-sec",  Name = "1Password",       Description = "Password manager",           Source = MacPackageSource.BrewCask,    PackageName = "1password" },
                new() { Id = "bitwarden-sec",  Name = "Bitwarden",       Description = "Open-source password mgr",   Source = MacPackageSource.BrewCask,    PackageName = "bitwarden" },
                new() { Id = "keepassxc",      Name = "KeePassXC",       Description = "Offline password manager",   Source = MacPackageSource.BrewCask,    PackageName = "keepassxc" },
                new() { Id = "nordvpn",        Name = "NordVPN",         Description = "VPN service",                Source = MacPackageSource.BrewCask,    PackageName = "nordvpn" },
                new() { Id = "protonvpn",      Name = "ProtonVPN",       Description = "Privacy-focused VPN",        Source = MacPackageSource.BrewCask,    PackageName = "protonvpn" },
                new() { Id = "tor-browser",    Name = "Tor Browser",     Description = "Anonymous browsing",         Source = MacPackageSource.BrewCask,    PackageName = "tor-browser" },
                new() { Id = "wireguard",      Name = "WireGuard Tools", Description = "Modern VPN tools",           Source = MacPackageSource.BrewFormula, PackageName = "wireguard-tools" },
                new() { Id = "openvpn",        Name = "OpenVPN",         Description = "OpenVPN client",             Source = MacPackageSource.BrewFormula, PackageName = "openvpn" },
                new() { Id = "gnupg",          Name = "GnuPG",           Description = "GPG encryption",             Source = MacPackageSource.BrewFormula, PackageName = "gnupg" },
                new() { Id = "little-snitch",  Name = "Little Snitch",   Description = "Outbound firewall",          Source = MacPackageSource.BrewCask,    PackageName = "little-snitch" },
                new() { Id = "malwarebytes",   Name = "Malwarebytes",    Description = "Malware scanner",            Source = MacPackageSource.BrewCask,    PackageName = "malwarebytes" },
            }
        },

        // ========== 7. System Utilities (16) ==========
        new()
        {
            Name = "System Utilities",
            Description = "System monitoring and maintenance tools",
            Icon = "E950",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "htop",             Name = "htop",              Description = "Interactive process viewer",  Source = MacPackageSource.BrewFormula, PackageName = "htop" },
                new() { Id = "btop",             Name = "btop",              Description = "Modern resource monitor",     Source = MacPackageSource.BrewFormula, PackageName = "btop" },
                new() { Id = "ncdu",             Name = "ncdu",              Description = "Disk usage analyzer",         Source = MacPackageSource.BrewFormula, PackageName = "ncdu" },
                new() { Id = "onyx",             Name = "OnyX",              Description = "System maintenance",          Source = MacPackageSource.BrewCask,    PackageName = "onyx" },
                new() { Id = "cleanmymac",       Name = "CleanMyMac",        Description = "System cleaner (trial)",      Source = MacPackageSource.BrewCask,    PackageName = "cleanmymac" },
                new() { Id = "appcleaner-sys",   Name = "AppCleaner",        Description = "App uninstaller",             Source = MacPackageSource.BrewCask,    PackageName = "appcleaner" },
                new() { Id = "grandperspective",Name = "GrandPerspective",  Description = "Visual disk usage",           Source = MacPackageSource.BrewCask,    PackageName = "grandperspective" },
                new() { Id = "disk-inv-x",       Name = "Disk Inventory X",  Description = "Disk usage visualizer",       Source = MacPackageSource.BrewCask,    PackageName = "disk-inventory-x" },
                new() { Id = "raycast",          Name = "Raycast",           Description = "Spotlight replacement",       Source = MacPackageSource.BrewCask,    PackageName = "raycast" },
                new() { Id = "alfred",           Name = "Alfred",            Description = "Productivity launcher",       Source = MacPackageSource.BrewCask,    PackageName = "alfred" },
                new() { Id = "bartender",        Name = "Bartender",         Description = "Menu bar organizer",          Source = MacPackageSource.BrewCask,    PackageName = "bartender" },
                new() { Id = "istat-menus",      Name = "iStat Menus",       Description = "System stats in menu bar",    Source = MacPackageSource.BrewCask,    PackageName = "istat-menus" },
                new() { Id = "coconutbattery",   Name = "coconutBattery",    Description = "Battery health monitor",      Source = MacPackageSource.BrewCask,    PackageName = "coconutbattery" },
                new() { Id = "stats",            Name = "Stats",             Description = "Free menu bar system stats",  Source = MacPackageSource.BrewCask,    PackageName = "stats" },
                new() { Id = "macs-fan-control",Name = "Macs Fan Control",  Description = "Fan speed control",           Source = MacPackageSource.BrewCask,    PackageName = "macs-fan-control" },
                new() { Id = "tree",             Name = "tree",              Description = "Directory tree viewer",       Source = MacPackageSource.BrewFormula, PackageName = "tree" },
            }
        },

        // ========== 8. Communication (9) ==========
        new()
        {
            Name = "Communication",
            Description = "Messaging, email, and video chat apps",
            Icon = "E8BD",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "discord-comm",  Name = "Discord",       Description = "Chat and voice",         Source = MacPackageSource.BrewCask, PackageName = "discord" },
                new() { Id = "telegram",      Name = "Telegram",      Description = "Instant messenger",      Source = MacPackageSource.BrewCask, PackageName = "telegram" },
                new() { Id = "signal",        Name = "Signal",        Description = "Encrypted messenger",    Source = MacPackageSource.BrewCask, PackageName = "signal" },
                new() { Id = "thunderbird-c", Name = "Thunderbird",   Description = "Email client",           Source = MacPackageSource.BrewCask, PackageName = "thunderbird" },
                new() { Id = "element",       Name = "Element",       Description = "Matrix client",          Source = MacPackageSource.BrewCask, PackageName = "element" },
                new() { Id = "skype",         Name = "Skype",         Description = "Microsoft video calls",  Source = MacPackageSource.BrewCask, PackageName = "skype" },
                new() { Id = "whatsapp",      Name = "WhatsApp",      Description = "WhatsApp desktop",       Source = MacPackageSource.BrewCask, PackageName = "whatsapp" },
                new() { Id = "airmail",       Name = "Airmail",       Description = "Email client for Mac",   Source = MacPackageSource.BrewCask, PackageName = "airmail" },
                new() { Id = "spark",         Name = "Spark",         Description = "Email by Readdle",       Source = MacPackageSource.BrewCask, PackageName = "spark" },
            }
        },

        // ========== 9. Web Browsers (9) ==========
        new()
        {
            Name = "Web Browsers",
            Description = "Alternative web browsers",
            Icon = "E774",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "firefox-br",     Name = "Firefox",       Description = "Mozilla browser",         Source = MacPackageSource.BrewCask, PackageName = "firefox" },
                new() { Id = "chrome-br",      Name = "Google Chrome", Description = "Google's browser",        Source = MacPackageSource.BrewCask, PackageName = "google-chrome" },
                new() { Id = "brave-br",       Name = "Brave",         Description = "Privacy-focused browser", Source = MacPackageSource.BrewCask, PackageName = "brave-browser" },
                new() { Id = "opera",          Name = "Opera",         Description = "Built-in VPN browser",    Source = MacPackageSource.BrewCask, PackageName = "opera" },
                new() { Id = "vivaldi",        Name = "Vivaldi",       Description = "Customizable browser",    Source = MacPackageSource.BrewCask, PackageName = "vivaldi" },
                new() { Id = "tor-br",         Name = "Tor Browser",   Description = "Anonymous browsing",      Source = MacPackageSource.BrewCask, PackageName = "tor-browser" },
                new() { Id = "edge",           Name = "Microsoft Edge",Description = "Microsoft's browser",     Source = MacPackageSource.BrewCask, PackageName = "microsoft-edge" },
                new() { Id = "arc",            Name = "Arc",           Description = "The new browser",         Source = MacPackageSource.BrewCask, PackageName = "arc" },
                new() { Id = "orion",          Name = "Orion",         Description = "WebKit browser by Kagi",  Source = MacPackageSource.BrewCask, PackageName = "orion" },
            }
        },

        // ========== 10. macOS Native Utilities (12) ==========
        new()
        {
            Name = "macOS Native Utilities",
            Description = "Mac-specific power-user tools",
            Icon = "E7C0",
            Apps = new List<MacBundleApp>
            {
                new() { Id = "rectangle-mac",     Name = "Rectangle",          Description = "Window snapping tool",            Source = MacPackageSource.BrewCask, PackageName = "rectangle" },
                new() { Id = "magnet",            Name = "Magnet",             Description = "Window manager",                  Source = MacPackageSource.BrewCask, PackageName = "magnet" },
                new() { Id = "bettertouchtool",   Name = "BetterTouchTool",    Description = "Input customization",             Source = MacPackageSource.BrewCask, PackageName = "bettertouchtool" },
                new() { Id = "karabiner",         Name = "Karabiner-Elements", Description = "Keyboard customization",          Source = MacPackageSource.BrewCask, PackageName = "karabiner-elements" },
                new() { Id = "keka-mac",          Name = "Keka",               Description = "Archive utility",                 Source = MacPackageSource.BrewCask, PackageName = "keka" },
                new() { Id = "unarchiver-mac",    Name = "The Unarchiver",     Description = "Archive extractor",               Source = MacPackageSource.BrewCask, PackageName = "the-unarchiver" },
                new() { Id = "appcleaner-mac",    Name = "AppCleaner",         Description = "App uninstaller",                 Source = MacPackageSource.BrewCask, PackageName = "appcleaner" },
                new() { Id = "onyx-mac",          Name = "OnyX",               Description = "System maintenance",              Source = MacPackageSource.BrewCask, PackageName = "onyx" },
                new() { Id = "clipy",             Name = "Clipy",              Description = "Clipboard manager",               Source = MacPackageSource.BrewCask, PackageName = "clipy" },
                new() { Id = "hiddenbar",         Name = "Hidden Bar",         Description = "Menu bar hider",                  Source = MacPackageSource.BrewCask, PackageName = "hiddenbar" },
                new() { Id = "maccy",             Name = "Maccy",              Description = "Clipboard manager",               Source = MacPackageSource.BrewCask, PackageName = "maccy" },
                new() { Id = "mos",               Name = "Mos",                Description = "Smooth scrolling for mice",       Source = MacPackageSource.BrewCask, PackageName = "mos" },
            }
        },
    };

    public static MacBundleApp? FindById(string id) =>
        AllBundles.SelectMany(b => b.Apps).FirstOrDefault(a => a.Id == id);
}
