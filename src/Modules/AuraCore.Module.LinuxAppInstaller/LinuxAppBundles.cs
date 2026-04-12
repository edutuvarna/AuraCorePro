using AuraCore.Module.LinuxAppInstaller.Models;

namespace AuraCore.Module.LinuxAppInstaller;

public static class LinuxAppBundles
{
    public static readonly IReadOnlyList<LinuxAppBundle> AllBundles = new List<LinuxAppBundle>
    {
        // ========== 1. Essential Apps (12) ==========
        new()
        {
            Name = "Essential Apps",
            Description = "Core applications everyone needs",
            Icon = "E774",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "firefox", Name = "Firefox", Description = "Popular open-source browser", Source = LinuxPackageSource.Apt, PackageName = "firefox" },
                new() { Id = "chromium", Name = "Chromium", Description = "Open-source Chrome base", Source = LinuxPackageSource.Apt, PackageName = "chromium-browser" },
                new() { Id = "vlc", Name = "VLC Media Player", Description = "Plays any video/audio format", Source = LinuxPackageSource.Apt, PackageName = "vlc" },
                new() { Id = "libreoffice", Name = "LibreOffice", Description = "Full office suite", Source = LinuxPackageSource.Apt, PackageName = "libreoffice" },
                new() { Id = "gimp", Name = "GIMP", Description = "Image editor (Photoshop alternative)", Source = LinuxPackageSource.Apt, PackageName = "gimp" },
                new() { Id = "thunderbird", Name = "Thunderbird", Description = "Email client", Source = LinuxPackageSource.Apt, PackageName = "thunderbird" },
                new() { Id = "file-roller", Name = "File Roller", Description = "Archive manager (zip/tar/7z)", Source = LinuxPackageSource.Apt, PackageName = "file-roller" },
                new() { Id = "filezilla", Name = "FileZilla", Description = "FTP client", Source = LinuxPackageSource.Apt, PackageName = "filezilla" },
                new() { Id = "transmission", Name = "Transmission", Description = "BitTorrent client", Source = LinuxPackageSource.Apt, PackageName = "transmission-gtk" },
                new() { Id = "bleachbit", Name = "BleachBit", Description = "System cleaner", Source = LinuxPackageSource.Apt, PackageName = "bleachbit" },
                new() { Id = "p7zip", Name = "7-Zip (p7zip)", Description = "High-ratio archiver", Source = LinuxPackageSource.Apt, PackageName = "p7zip-full" },
                new() { Id = "gnome-tweaks", Name = "GNOME Tweaks", Description = "GNOME customization", Source = LinuxPackageSource.Apt, PackageName = "gnome-tweaks" },
            }
        },

        // ========== 2. Developer Tools (28) ==========
        new()
        {
            Name = "Developer Tools",
            Description = "Programming languages, IDEs, and dev utilities",
            Icon = "E943",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "vscode", Name = "VS Code", Description = "Code editor", Source = LinuxPackageSource.Snap, PackageName = "code --classic" },
                new() { Id = "git", Name = "Git", Description = "Version control", Source = LinuxPackageSource.Apt, PackageName = "git" },
                new() { Id = "nodejs", Name = "Node.js", Description = "JavaScript runtime", Source = LinuxPackageSource.Apt, PackageName = "nodejs" },
                new() { Id = "npm", Name = "npm", Description = "Node package manager", Source = LinuxPackageSource.Apt, PackageName = "npm" },
                new() { Id = "python3", Name = "Python 3", Description = "Python language", Source = LinuxPackageSource.Apt, PackageName = "python3" },
                new() { Id = "python3-pip", Name = "pip", Description = "Python package manager", Source = LinuxPackageSource.Apt, PackageName = "python3-pip" },
                new() { Id = "docker", Name = "Docker", Description = "Container platform", Source = LinuxPackageSource.Apt, PackageName = "docker.io" },
                new() { Id = "docker-compose", Name = "Docker Compose", Description = "Multi-container orchestration", Source = LinuxPackageSource.Apt, PackageName = "docker-compose" },
                new() { Id = "build-essential", Name = "Build Essential", Description = "GCC, make, build tools", Source = LinuxPackageSource.Apt, PackageName = "build-essential" },
                new() { Id = "cmake", Name = "CMake", Description = "Cross-platform build system", Source = LinuxPackageSource.Apt, PackageName = "cmake" },
                new() { Id = "gcc", Name = "GCC", Description = "C compiler", Source = LinuxPackageSource.Apt, PackageName = "gcc" },
                new() { Id = "gpp", Name = "G++", Description = "C++ compiler", Source = LinuxPackageSource.Apt, PackageName = "g++" },
                new() { Id = "golang", Name = "Go", Description = "Go language", Source = LinuxPackageSource.Apt, PackageName = "golang-go" },
                new() { Id = "rustc", Name = "Rust", Description = "Rust language", Source = LinuxPackageSource.Apt, PackageName = "rustc" },
                new() { Id = "openjdk17", Name = "OpenJDK 17", Description = "Java Development Kit", Source = LinuxPackageSource.Apt, PackageName = "openjdk-17-jdk" },
                new() { Id = "maven", Name = "Maven", Description = "Java build tool", Source = LinuxPackageSource.Apt, PackageName = "maven" },
                new() { Id = "gradle", Name = "Gradle", Description = "Build automation", Source = LinuxPackageSource.Apt, PackageName = "gradle" },
                new() { Id = "dbeaver", Name = "DBeaver", Description = "Universal database tool", Source = LinuxPackageSource.Snap, PackageName = "dbeaver-ce" },
                new() { Id = "wireshark", Name = "Wireshark", Description = "Network protocol analyzer", Source = LinuxPackageSource.Apt, PackageName = "wireshark" },
                new() { Id = "curl", Name = "curl", Description = "HTTP client", Source = LinuxPackageSource.Apt, PackageName = "curl" },
                new() { Id = "wget", Name = "wget", Description = "File downloader", Source = LinuxPackageSource.Apt, PackageName = "wget" },
                new() { Id = "jq", Name = "jq", Description = "JSON processor", Source = LinuxPackageSource.Apt, PackageName = "jq" },
                new() { Id = "tmux", Name = "tmux", Description = "Terminal multiplexer", Source = LinuxPackageSource.Apt, PackageName = "tmux" },
                new() { Id = "meld", Name = "Meld", Description = "Diff and merge tool", Source = LinuxPackageSource.Apt, PackageName = "meld" },
                new() { Id = "postman", Name = "Postman", Description = "API testing tool", Source = LinuxPackageSource.Snap, PackageName = "postman" },
                new() { Id = "insomnia", Name = "Insomnia", Description = "REST client", Source = LinuxPackageSource.Snap, PackageName = "insomnia" },
                new() { Id = "sublime-text", Name = "Sublime Text", Description = "Fast text editor", Source = LinuxPackageSource.Snap, PackageName = "sublime-text --classic" },
                new() { Id = "intellij", Name = "IntelliJ IDEA Community", Description = "Java IDE", Source = LinuxPackageSource.Snap, PackageName = "intellij-idea-community --classic" },
            }
        },

        // ========== 3. Productivity & Office (18) ==========
        new()
        {
            Name = "Productivity & Office",
            Description = "Notes, docs, collaboration, and office tools",
            Icon = "E77F",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "libreoffice-full", Name = "LibreOffice (Full)", Description = "Complete LibreOffice suite", Source = LinuxPackageSource.Apt, PackageName = "libreoffice" },
                new() { Id = "onlyoffice", Name = "OnlyOffice Desktop", Description = "MS Office compatible", Source = LinuxPackageSource.Snap, PackageName = "onlyoffice-desktopeditors" },
                new() { Id = "obsidian", Name = "Obsidian", Description = "Knowledge base", Source = LinuxPackageSource.Flatpak, PackageName = "md.obsidian.Obsidian" },
                new() { Id = "zoom", Name = "Zoom", Description = "Video conferencing", Source = LinuxPackageSource.Snap, PackageName = "zoom-client" },
                new() { Id = "slack", Name = "Slack", Description = "Team messaging", Source = LinuxPackageSource.Snap, PackageName = "slack --classic" },
                new() { Id = "teams", Name = "Microsoft Teams", Description = "Team collaboration", Source = LinuxPackageSource.Snap, PackageName = "teams-for-linux" },
                new() { Id = "notion", Name = "Notion", Description = "All-in-one workspace", Source = LinuxPackageSource.Snap, PackageName = "notion-snap" },
                new() { Id = "joplin", Name = "Joplin", Description = "Note-taking app", Source = LinuxPackageSource.Snap, PackageName = "joplin-desktop" },
                new() { Id = "simplenote", Name = "Simplenote", Description = "Simple note-taking", Source = LinuxPackageSource.Snap, PackageName = "simplenote" },
                new() { Id = "standard-notes", Name = "Standard Notes", Description = "Encrypted notes", Source = LinuxPackageSource.Snap, PackageName = "standard-notes" },
                new() { Id = "evince", Name = "Evince", Description = "PDF viewer", Source = LinuxPackageSource.Apt, PackageName = "evince" },
                new() { Id = "keepassxc", Name = "KeePassXC", Description = "Password manager", Source = LinuxPackageSource.Apt, PackageName = "keepassxc" },
                new() { Id = "calibre", Name = "Calibre", Description = "E-book manager", Source = LinuxPackageSource.Apt, PackageName = "calibre" },
                new() { Id = "atril", Name = "Atril", Description = "MATE PDF viewer", Source = LinuxPackageSource.Apt, PackageName = "atril" },
                new() { Id = "nextcloud-client", Name = "Nextcloud Client", Description = "Cloud file sync", Source = LinuxPackageSource.Apt, PackageName = "nextcloud-desktop" },
                new() { Id = "syncthing", Name = "Syncthing", Description = "P2P file sync", Source = LinuxPackageSource.Apt, PackageName = "syncthing" },
                new() { Id = "scribus", Name = "Scribus", Description = "Desktop publishing", Source = LinuxPackageSource.Apt, PackageName = "scribus" },
                new() { Id = "pinta", Name = "Pinta", Description = "Simple image editor", Source = LinuxPackageSource.Apt, PackageName = "pinta" },
            }
        },

        // ========== 4. Media & Creative (16) ==========
        new()
        {
            Name = "Media & Creative",
            Description = "Image, video, audio editing and design tools",
            Icon = "E8AD",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "gimp-full", Name = "GIMP", Description = "Image editor", Source = LinuxPackageSource.Apt, PackageName = "gimp" },
                new() { Id = "inkscape", Name = "Inkscape", Description = "Vector graphics editor", Source = LinuxPackageSource.Apt, PackageName = "inkscape" },
                new() { Id = "blender", Name = "Blender", Description = "3D creation suite", Source = LinuxPackageSource.Snap, PackageName = "blender --classic" },
                new() { Id = "audacity", Name = "Audacity", Description = "Audio editor", Source = LinuxPackageSource.Apt, PackageName = "audacity" },
                new() { Id = "obs-studio", Name = "OBS Studio", Description = "Screen recording/streaming", Source = LinuxPackageSource.Apt, PackageName = "obs-studio" },
                new() { Id = "kdenlive", Name = "Kdenlive", Description = "Video editor", Source = LinuxPackageSource.Apt, PackageName = "kdenlive" },
                new() { Id = "shotcut", Name = "Shotcut", Description = "Cross-platform video editor", Source = LinuxPackageSource.Snap, PackageName = "shotcut --classic" },
                new() { Id = "krita", Name = "Krita", Description = "Digital painting", Source = LinuxPackageSource.Apt, PackageName = "krita" },
                new() { Id = "darktable", Name = "Darktable", Description = "RAW photo workflow", Source = LinuxPackageSource.Apt, PackageName = "darktable" },
                new() { Id = "handbrake", Name = "HandBrake", Description = "Video transcoder", Source = LinuxPackageSource.Apt, PackageName = "handbrake" },
                new() { Id = "mypaint", Name = "MyPaint", Description = "Digital painting for artists", Source = LinuxPackageSource.Apt, PackageName = "mypaint" },
                new() { Id = "ardour", Name = "Ardour", Description = "Digital audio workstation", Source = LinuxPackageSource.Apt, PackageName = "ardour" },
                new() { Id = "openshot", Name = "OpenShot", Description = "Simple video editor", Source = LinuxPackageSource.Apt, PackageName = "openshot-qt" },
                new() { Id = "rawtherapee", Name = "RawTherapee", Description = "RAW image processor", Source = LinuxPackageSource.Apt, PackageName = "rawtherapee" },
                new() { Id = "ffmpeg", Name = "FFmpeg", Description = "Audio/video converter", Source = LinuxPackageSource.Apt, PackageName = "ffmpeg" },
                new() { Id = "imagemagick", Name = "ImageMagick", Description = "Image manipulation CLI", Source = LinuxPackageSource.Apt, PackageName = "imagemagick" },
            }
        },

        // ========== 5. Gaming (12) ==========
        new()
        {
            Name = "Gaming",
            Description = "Game launchers, tools, and compatibility layers",
            Icon = "E7FC",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "steam", Name = "Steam", Description = "Game platform", Source = LinuxPackageSource.Apt, PackageName = "steam" },
                new() { Id = "lutris", Name = "Lutris", Description = "Game manager", Source = LinuxPackageSource.Apt, PackageName = "lutris" },
                new() { Id = "heroic", Name = "Heroic Games Launcher", Description = "Epic/GOG client", Source = LinuxPackageSource.Flatpak, PackageName = "com.heroicgameslauncher.hgl" },
                new() { Id = "wine", Name = "Wine", Description = "Windows compatibility layer", Source = LinuxPackageSource.Apt, PackageName = "wine" },
                new() { Id = "mangohud", Name = "MangoHud", Description = "FPS/system monitor overlay", Source = LinuxPackageSource.Apt, PackageName = "mangohud" },
                new() { Id = "gamemode", Name = "GameMode", Description = "Performance optimizer", Source = LinuxPackageSource.Apt, PackageName = "gamemode" },
                new() { Id = "protonup-qt", Name = "ProtonUp-Qt", Description = "Proton version manager", Source = LinuxPackageSource.Flatpak, PackageName = "net.davidotek.pupgui2" },
                new() { Id = "discord", Name = "Discord", Description = "Gaming voice/chat", Source = LinuxPackageSource.Snap, PackageName = "discord" },
                new() { Id = "minecraft", Name = "Minecraft Launcher", Description = "Minecraft Java", Source = LinuxPackageSource.Snap, PackageName = "mc-installer" },
                new() { Id = "supertuxkart", Name = "SuperTuxKart", Description = "Free kart racing", Source = LinuxPackageSource.Apt, PackageName = "supertuxkart" },
                new() { Id = "openttd", Name = "OpenTTD", Description = "Transport tycoon clone", Source = LinuxPackageSource.Apt, PackageName = "openttd" },
                new() { Id = "dosbox", Name = "DOSBox", Description = "DOS emulator", Source = LinuxPackageSource.Apt, PackageName = "dosbox" },
            }
        },

        // ========== 6. Security & Privacy (11) ==========
        new()
        {
            Name = "Security & Privacy",
            Description = "VPN, password managers, encryption, antivirus",
            Icon = "E72E",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "keepassxc-sec", Name = "KeePassXC", Description = "Password manager", Source = LinuxPackageSource.Apt, PackageName = "keepassxc" },
                new() { Id = "bitwarden", Name = "Bitwarden", Description = "Cloud password manager", Source = LinuxPackageSource.Snap, PackageName = "bitwarden" },
                new() { Id = "clamav", Name = "ClamAV", Description = "Antivirus engine", Source = LinuxPackageSource.Apt, PackageName = "clamav" },
                new() { Id = "clamtk", Name = "ClamTK", Description = "ClamAV GUI", Source = LinuxPackageSource.Apt, PackageName = "clamtk" },
                new() { Id = "rkhunter", Name = "rkhunter", Description = "Rootkit detector", Source = LinuxPackageSource.Apt, PackageName = "rkhunter" },
                new() { Id = "chkrootkit", Name = "chkrootkit", Description = "Rootkit scanner", Source = LinuxPackageSource.Apt, PackageName = "chkrootkit" },
                new() { Id = "tor-browser", Name = "Tor Browser", Description = "Anonymous browsing", Source = LinuxPackageSource.Flatpak, PackageName = "com.github.micahflee.torbrowser-launcher" },
                new() { Id = "wireguard", Name = "WireGuard", Description = "Modern VPN", Source = LinuxPackageSource.Apt, PackageName = "wireguard" },
                new() { Id = "openvpn", Name = "OpenVPN", Description = "OpenVPN client", Source = LinuxPackageSource.Apt, PackageName = "openvpn" },
                new() { Id = "gnupg", Name = "GnuPG", Description = "GPG encryption", Source = LinuxPackageSource.Apt, PackageName = "gnupg" },
                new() { Id = "fail2ban", Name = "Fail2ban", Description = "Intrusion prevention", Source = LinuxPackageSource.Apt, PackageName = "fail2ban" },
            }
        },

        // ========== 7. System Utilities (16) ==========
        new()
        {
            Name = "System Utilities",
            Description = "System monitoring and maintenance tools",
            Icon = "E950",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "htop", Name = "htop", Description = "Interactive process viewer", Source = LinuxPackageSource.Apt, PackageName = "htop" },
                new() { Id = "btop", Name = "btop", Description = "Modern resource monitor", Source = LinuxPackageSource.Apt, PackageName = "btop" },
                new() { Id = "ncdu", Name = "ncdu", Description = "Disk usage analyzer", Source = LinuxPackageSource.Apt, PackageName = "ncdu" },
                new() { Id = "timeshift", Name = "Timeshift", Description = "System restore tool", Source = LinuxPackageSource.Apt, PackageName = "timeshift" },
                new() { Id = "stacer", Name = "Stacer", Description = "System optimizer GUI", Source = LinuxPackageSource.Apt, PackageName = "stacer" },
                new() { Id = "gparted", Name = "GParted", Description = "Partition editor", Source = LinuxPackageSource.Apt, PackageName = "gparted" },
                new() { Id = "baobab", Name = "Baobab", Description = "Disk usage analyzer", Source = LinuxPackageSource.Apt, PackageName = "baobab" },
                new() { Id = "neofetch", Name = "neofetch", Description = "System info display", Source = LinuxPackageSource.Apt, PackageName = "neofetch" },
                new() { Id = "inxi", Name = "inxi", Description = "Full system info", Source = LinuxPackageSource.Apt, PackageName = "inxi" },
                new() { Id = "hwinfo", Name = "hwinfo", Description = "Hardware detector", Source = LinuxPackageSource.Apt, PackageName = "hwinfo" },
                new() { Id = "tree", Name = "tree", Description = "Directory tree viewer", Source = LinuxPackageSource.Apt, PackageName = "tree" },
                new() { Id = "rsync", Name = "rsync", Description = "Fast file sync", Source = LinuxPackageSource.Apt, PackageName = "rsync" },
                new() { Id = "screen", Name = "GNU screen", Description = "Terminal multiplexer", Source = LinuxPackageSource.Apt, PackageName = "screen" },
                new() { Id = "iotop", Name = "iotop", Description = "I/O monitoring", Source = LinuxPackageSource.Apt, PackageName = "iotop" },
                new() { Id = "nmon", Name = "nmon", Description = "Performance monitor", Source = LinuxPackageSource.Apt, PackageName = "nmon" },
                new() { Id = "lm-sensors", Name = "lm-sensors", Description = "Temperature/fan sensors", Source = LinuxPackageSource.Apt, PackageName = "lm-sensors" },
            }
        },

        // ========== 8. Communication (9) ==========
        new()
        {
            Name = "Communication",
            Description = "Messaging, email, and video chat apps",
            Icon = "E8BD",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "discord-comm", Name = "Discord", Description = "Chat and voice", Source = LinuxPackageSource.Snap, PackageName = "discord" },
                new() { Id = "telegram", Name = "Telegram", Description = "Instant messenger", Source = LinuxPackageSource.Snap, PackageName = "telegram-desktop" },
                new() { Id = "signal", Name = "Signal", Description = "Encrypted messenger", Source = LinuxPackageSource.Snap, PackageName = "signal-desktop" },
                new() { Id = "thunderbird-comm", Name = "Thunderbird", Description = "Email client", Source = LinuxPackageSource.Apt, PackageName = "thunderbird" },
                new() { Id = "element", Name = "Element", Description = "Matrix client", Source = LinuxPackageSource.Snap, PackageName = "element-desktop" },
                new() { Id = "skype", Name = "Skype", Description = "Microsoft video calls", Source = LinuxPackageSource.Snap, PackageName = "skype --classic" },
                new() { Id = "wire", Name = "Wire", Description = "Secure collaboration", Source = LinuxPackageSource.Snap, PackageName = "wire" },
                new() { Id = "pidgin", Name = "Pidgin", Description = "Multi-protocol IM", Source = LinuxPackageSource.Apt, PackageName = "pidgin" },
                new() { Id = "hexchat", Name = "HexChat", Description = "IRC client", Source = LinuxPackageSource.Apt, PackageName = "hexchat" },
            }
        },

        // ========== 9. Web Browsers (9) ==========
        new()
        {
            Name = "Web Browsers",
            Description = "Alternative web browsers",
            Icon = "E774",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "firefox-br", Name = "Firefox", Description = "Mozilla browser", Source = LinuxPackageSource.Apt, PackageName = "firefox" },
                new() { Id = "chromium-br", Name = "Chromium", Description = "Chrome's open-source base", Source = LinuxPackageSource.Apt, PackageName = "chromium-browser" },
                new() { Id = "brave", Name = "Brave", Description = "Privacy-focused browser", Source = LinuxPackageSource.Snap, PackageName = "brave" },
                new() { Id = "opera", Name = "Opera", Description = "Built-in VPN browser", Source = LinuxPackageSource.Snap, PackageName = "opera" },
                new() { Id = "vivaldi", Name = "Vivaldi", Description = "Customizable browser", Source = LinuxPackageSource.Snap, PackageName = "vivaldi" },
                new() { Id = "tor-browser-br", Name = "Tor Browser", Description = "Anonymous browsing", Source = LinuxPackageSource.Flatpak, PackageName = "com.github.micahflee.torbrowser-launcher" },
                new() { Id = "librewolf", Name = "LibreWolf", Description = "Hardened Firefox", Source = LinuxPackageSource.Flatpak, PackageName = "io.gitlab.librewolf-community" },
                new() { Id = "falkon", Name = "Falkon", Description = "KDE QtWebEngine browser", Source = LinuxPackageSource.Apt, PackageName = "falkon" },
                new() { Id = "epiphany", Name = "GNOME Web (Epiphany)", Description = "GNOME WebKit browser", Source = LinuxPackageSource.Apt, PackageName = "epiphany-browser" },
            }
        },

        // ========== 10. Terminal & Shell (10) ==========
        new()
        {
            Name = "Terminal & Shell",
            Description = "Terminals, shells, and command-line tools",
            Icon = "E756",
            Apps = new List<LinuxBundleApp>
            {
                new() { Id = "zsh", Name = "Zsh", Description = "Modern shell", Source = LinuxPackageSource.Apt, PackageName = "zsh" },
                new() { Id = "fish", Name = "Fish Shell", Description = "User-friendly shell", Source = LinuxPackageSource.Apt, PackageName = "fish" },
                new() { Id = "alacritty", Name = "Alacritty", Description = "GPU-accelerated terminal", Source = LinuxPackageSource.Apt, PackageName = "alacritty" },
                new() { Id = "kitty", Name = "Kitty", Description = "Fast GPU terminal", Source = LinuxPackageSource.Apt, PackageName = "kitty" },
                new() { Id = "terminator", Name = "Terminator", Description = "Tiled terminal", Source = LinuxPackageSource.Apt, PackageName = "terminator" },
                new() { Id = "tmux-term", Name = "tmux", Description = "Terminal multiplexer", Source = LinuxPackageSource.Apt, PackageName = "tmux" },
                new() { Id = "starship", Name = "Starship", Description = "Cross-shell prompt", Source = LinuxPackageSource.Snap, PackageName = "starship" },
                new() { Id = "tilix", Name = "Tilix", Description = "Tiled GTK terminal", Source = LinuxPackageSource.Apt, PackageName = "tilix" },
                new() { Id = "bash-completion", Name = "bash-completion", Description = "Tab completion for bash", Source = LinuxPackageSource.Apt, PackageName = "bash-completion" },
                new() { Id = "ripgrep", Name = "ripgrep", Description = "Fast grep alternative", Source = LinuxPackageSource.Apt, PackageName = "ripgrep" },
            }
        },
    };

    public static LinuxBundleApp? FindById(string id) =>
        AllBundles.SelectMany(b => b.Apps).FirstOrDefault(a => a.Id == id);
}
