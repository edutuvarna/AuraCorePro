# Phase 3: AI Features Consolidation + Sidebar Cleanup — Design Spec

**Date:** 2026-04-15
**Status:** Pending review
**Scope:** Replace the 4-accordion AI sidebar sub-items with a unified AIFeaturesView page (Insights + Recommendations + Smart Schedule + Chat). Bundle Phase 2.5 carry-overs (tier locking + missing modules + visual verify) as this phase touches the same files. Covers Vision Doc §10 Phase 3.

---

## 1. Purpose

Phase 2 shipped the restructured sidebar and redesigned Dashboard. The "AI Features" category currently holds 4 accordion sub-items (Insights, Recommendations, Schedule, Chat). This fragmentation dilutes the CORTEX brand and creates navigation overhead. Phase 3 collapses the 4 views into a single unified AIFeaturesView with a dashboard-style overview and hybrid drill-in, making CORTEX feel cohesive and approachable. Model management (R2 download infrastructure) is introduced so AI Chat actually works end-to-end when enabled.

Tier locking (regressed in Phase 2) and missing modules in the sidebar (system-health, admin-panel, platform-specific) are bundled into this phase because they touch the same `SidebarViewModel` / `SidebarNavItem` files — avoiding a separate Phase 2.5 sprint.

**Phase 3 does NOT:**
- Add new AI capabilities beyond what already exists (Insights, Recommendations, Schedule, Chat logic all preserved)
- Rewrite any existing AI ViewModel (refresh `.axaml`s only; preserve AI logic)
- Build Settings > Models page (Phase 4+)
- Implement model delete, checksum validation, pause/resume, or auto-update (Phase 4+)
- Ship light theme (Vision §12 out of scope)
- Migrate module pages away from V1 theme bridge (Phase 4)

---

## 2. Inputs (what Phase 3 starts from)

- **Vision Document** — `docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md`
- **Phase 2 spec + completion memory** — `docs/superpowers/specs/2026-04-14-phase2-sidebar-dashboard-design.md` + memory file `project_ui_rebuild_phase_2_complete.md`
- **Phase 1 primitives** — GlassCard, HeroCTA, InsightCard, QuickActionTile, SidebarNavItem (will be extended), SidebarSectionDivider, StatusChip, AuraToggle, AccentBadge, UserChip, AppLogoBadge, Gauge
- **Theme V2 tokens** — `Themes/AuraCoreThemeV2.axaml` (authoritative)
- **Existing 4 AI views** — AIInsightsView/RecommendationsView/SchedulerView/AIChatView + their ViewModels and tests (kept, refactored, renamed)
- **Cloudflare R2 infrastructure** (set up 2026-04-15):
  - Custom domain: `https://models.auracore.pro` (active, SSL)
  - 8 GGUF models uploaded in `auracore-models` bucket
  - Cache Rule (1 year Edge/Browser TTL)
  - HSTS + Min TLS 1.2 + Bot Fight Mode enabled
  - Note: Files > 512 MB return `cf-cache-status: DYNAMIC` on Free plan — expected, no cost penalty (R2 egress free)

### Carry-overs from Phase 2 bundled here (Path A)

- **Tier locking regression** — Old `Nav_Click` applied `ApplyTierLocking` (dimmed + lock-iconed locked modules). New `SidebarNavItem` doesn't support a locked state. Phase 3 adds `IsLocked` DP and wires up `SidebarViewModel` tier check.
- **Missing modules in sidebar** — `system-health`, `admin-panel`, Linux-only (`systemd-manager`, `package-cleaner`, `swap-optimizer`, `cron-manager`), macOS-only (`defaults-optimizer`, `launchagent-manager`, `brew-manager`, `timemachine-manager`). Phase 3 adds these to appropriate categories with platform filtering.
- **Visual verify `bccd8ed`** — Quick Actions `HorizontalAlignment="Stretch"` fix not yet verified. First implementation step: launch app, confirm 2×2 grid evenly stretched.

---

## 3. Design Decisions (Brainstorming Outcomes)

### 3.1 Chat included as [Experimental] (Q1)

Chat stays in scope with permanent warning banner. Existing AIChatView code reused (refactored .axaml, same ViewModel). Aligns with Vision Doc §10 but acknowledges memory note "AI strategy pivot: deprioritize chatbot" — chat is opt-in and prominently warned, not removed.

### 3.2 Layout: Dashboard-Style 2×2 grid overview (Q2)

Hero + 4 cards (Insights / Recommendations / Schedule / Chat). Each card: status chip + title + preview text + master toggle. Matches Phase 2 Dashboard visual language (gauge row + hero + cards).

### 3.3 Drill-in: Hybrid (Q3)

**Overview mode:** 2×2 grid (landing).
**Detail mode:** Grid compresses to left sidebar (~120 px wide) with 5 items (Overview + 4 features); detail pane on right. "Overview" item returns to 2×2 grid.

### 3.4 Existing views: refresh .axaml, preserve ViewModels (Q4)

Each section's `.axaml` rewritten using Phase 1 primitives + V2 tokens. **ViewModel files untouched structurally** (logic preserved — no AI regression). Renamed to match new file layout (see §4.1).

### 3.5 Coupled toggle semantics (Q5)

Single master toggle per feature card:
- **ON:** feature runs in background + visible in UI
- **OFF:** feature paused (background timers stopped) + hidden from dashboard/status bar

State persisted via `IAppSettings`. Chat default OFF (opt-in); Insights/Recommendations/Schedule default ON. Learned pattern state preserved across toggle cycles (data not purged).

### 3.6 CORTEX Hero: Status-Aware (Q6)

Kicker `✦ CORTEX` + right chip (`● Active · Learning day N`) + title `AI Features` + tagline. Dynamic chip reflects aggregated feature state.

### 3.7 Model Management: split scope (Q7)

**In Phase 3:**
- R2 download service (simple GET with progress, no resume)
- Chat opt-in 2-step flow (experimental ack → model select/download)
- Chat header model switcher + "Download more" button
- ModelManagerDialog (OptIn + Manage modes)
- Model storage: `%LOCALAPPDATA%/AuraCorePro/models/*.gguf`
- No bundled default model; user must explicitly select
- Phi-3 Mini Q4KM carries `RECOMMENDED` badge (no pre-selection)

**Deferred to Phase 4+:**
- Settings > Models management page
- Model delete, checksum validation
- Download pause/resume, bandwidth throttle
- Auto-update checking

### 3.8 R2 Access Model: Custom Domain

`https://models.auracore.pro/*` public read (no auth required). Simple `HttpClient.GetAsync()`. Reconsider signed URLs via Cloudflare Workers in Phase 4+ if premium tiers emerge.

### 3.9 Tier Locking + Missing Modules bundled (Path A)

Phase 3 touches `SidebarViewModel` for the accordion cleanup; adding `IsLocked` DP and missing-module enumerations at the same time is minimal extra work. Avoids parallel Phase 2.5 sprint.

---

## 4. Architecture

### 4.1 File Layout

```
src/UI/AuraCore.UI.Avalonia/
├── Views/Pages/
│   ├── AIFeaturesView.axaml[.cs]                ← NEW (unified container)
│   └── AI/                                        ← NEW directory
│       ├── InsightsSection.axaml[.cs]            ← from AIInsightsView + refresh
│       ├── RecommendationsSection.axaml[.cs]     ← from RecommendationsView + refresh
│       ├── ScheduleSection.axaml[.cs]            ← from SchedulerView + refresh
│       └── ChatSection.axaml[.cs]                ← from AIChatView + refresh
├── Views/Dialogs/
│   ├── ChatOptInDialog.axaml[.cs]               ← NEW (2-step modal)
│   └── ModelManagerDialog.axaml[.cs]            ← NEW (OptIn + Manage modes)
├── Views/Controls/
│   └── SidebarNavItem.axaml[.cs]                ← MODIFIED (add IsLocked DP)
├── ViewModels/
│   ├── AIFeaturesViewModel.cs                   ← NEW
│   ├── InsightsSectionViewModel.cs              ← RENAMED from AIInsightsViewModel
│   ├── RecommendationsSectionViewModel.cs       ← RENAMED from RecommendationsViewModel
│   ├── ScheduleSectionViewModel.cs              ← RENAMED from SchedulerViewModel
│   ├── ChatSectionViewModel.cs                  ← RENAMED from AIChatViewModel
│   ├── ChatOptInDialogViewModel.cs              ← NEW
│   └── ModelManagerDialogViewModel.cs           ← NEW
├── Services/AI/
│   ├── IModelCatalog.cs + ModelCatalog.cs       ← NEW (8 models metadata)
│   ├── IModelDownloadService.cs + impl          ← NEW (R2 HTTP GET + progress)
│   ├── IInstalledModelStore.cs + impl           ← NEW (disk enumeration)
│   ├── ICortexAmbientService.cs + impl          ← NEW (state aggregation)
│   └── [existing AI adapter layers untouched]
└── LocalizationService.cs                        ← MODIFIED (new keys per §9)
```

**Old files deleted:**
- `Views/Pages/AIInsightsView.axaml[.cs]` (content moved to InsightsSection)
- `Views/Pages/RecommendationsView.axaml[.cs]`
- `Views/Pages/SchedulerView.axaml[.cs]`
- `Views/Pages/AIChatView.axaml[.cs]`
- `Views/Dialogs/SmartOptimizePlaceholderDialog.axaml[.cs]` (replaced by direct navigation to AIFeaturesView)

**Git archaeology note (see Risk R8):** Before deleting, run `git log --all -- "*AIInsights*"` etc. to preserve file history via `git mv` semantics where applicable.

### 4.2 AIFeaturesView — two view modes

ViewModel contract:

```csharp
public enum AIFeaturesViewMode { Overview, Detail }

public class AIFeaturesViewModel : ViewModelBase {
    public AIFeaturesViewMode Mode { get; set; }
    public string ActiveSection { get; set; }
        // "overview" | "insights" | "recommendations" | "schedule" | "chat"
    public bool IsOverview => Mode == AIFeaturesViewMode.Overview;
    public bool IsDetail => Mode == AIFeaturesViewMode.Detail;

    public AIFeatureCardVM InsightsCard { get; }
    public AIFeatureCardVM RecommendationsCard { get; }
    public AIFeatureCardVM ScheduleCard { get; }
    public AIFeatureCardVM ChatCard { get; }

    public string HeroStatusText { get; }
        // "Active · Learning day 3" / "Paused" / "Ready to start"

    public object? ActiveSectionView { get; }
        // UserControl instance (cached per section for state preservation)

    public ICommand NavigateToSection { get; }    // (string section) => void
    public ICommand NavigateToOverview { get; }
}
```

XAML structure (simplified):

```xml
<UserControl x:Class="AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView">
  <DockPanel>
    <!-- CORTEX Hero (status-aware) -->
    <Border DockPanel.Dock="Top" Classes="cortex-hero">
      <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto">
        <TextBlock Grid.Row="0" Classes="kicker"
                   Text="✦ CORTEX" />
        <controls:StatusChip Grid.Row="0" Grid.Column="1"
                             Text="{Binding HeroStatusText}" />
        <TextBlock Grid.Row="1" Grid.ColumnSpan="2" Classes="title"
                   Text="{loc aiFeatures.hero.title}" />
        <TextBlock Grid.Row="2" Grid.ColumnSpan="2" Classes="tagline"
                   Text="{loc aiFeatures.hero.tagline}" />
      </Grid>
    </Border>

    <!-- Content: Overview or Detail -->
    <Panel>
      <!-- Overview mode: 2×2 grid -->
      <UniformGrid Rows="2" Columns="2"
                   IsVisible="{Binding IsOverview}">
        <ai:AIFeatureCard DataContext="{Binding InsightsCard}" />
        <ai:AIFeatureCard DataContext="{Binding RecommendationsCard}" />
        <ai:AIFeatureCard DataContext="{Binding ScheduleCard}" />
        <ai:AIFeatureCard DataContext="{Binding ChatCard}" />
      </UniformGrid>

      <!-- Detail mode: sidebar + content -->
      <Grid ColumnDefinitions="120,*"
            IsVisible="{Binding IsDetail}">
        <StackPanel Grid.Column="0" Classes="section-nav">
          <ai:SectionNavItem Key="overview"
                             Command="{Binding NavigateToOverview}" />
          <ai:SectionNavItem Key="insights" ... />
          <ai:SectionNavItem Key="recommendations" ... />
          <ai:SectionNavItem Key="schedule" ... />
          <ai:SectionNavItem Key="chat" ... />
        </StackPanel>
        <ContentControl Grid.Column="1"
                        Content="{Binding ActiveSectionView}" />
      </Grid>
    </Panel>
  </DockPanel>
</UserControl>
```

`ActiveSectionView` is computed by the ViewModel to return the appropriate UserControl instance based on `ActiveSection`. UserControl instances are **cached per section** in a `Dictionary<string, UserControl>` to preserve ViewModel state on back-and-forth navigation (important for chat message history, scroll position, etc.).

### 4.3 CORTEX Hero (status-aware)

Inline in AIFeaturesView (not a reusable primitive in Phase 3 — lift to primitive in Phase 5 if reused).

Visual:

```
┌──────────────────────────────────────────────────────┐
│ ✦ CORTEX               [● ACTIVE · LEARNING DAY 3]   │
│                                                        │
│ AI Features                                           │
│ Intelligent monitoring, predictions, and automation   │
└──────────────────────────────────────────────────────┘
```

Status chip logic (driven by `ICortexAmbientService`):
- 1+ features ON: `● Active · Learning day N` (N = days since `AIFirstEnabledAt`, minimum 1)
- All features OFF and `AIFirstEnabledAt` set: `○ Paused`
- First run (`AIFirstEnabledAt` is null): `○ Ready to start`

Background: linear gradient `AccentPurple → AccentTeal` (subtle, 15%→8% opacity), radial glow top-right (`GlowPurple`).

### 4.4 AIFeatureCard — Overview mode card

Composition: `GlassCard` containing status row + title + preview body + master toggle.

Visual:

```
┌────────────────────────────────┐
│ ✦ INSIGHTS       [● · 3 active]│ ← accent kicker + status pill
│                         [ON ●○]│ ← AuraToggle (top-right)
│                                │
│ Cortex Insights                │ ← title (TextSubheading)
│ 3 active · 1 warning           │ ← preview summary (TextBody)
│                                │
│ ⚠ Abnormal spike detected      │ ← top highlight row (optional)
└────────────────────────────────┘
```

`AIFeatureCardVM` contract:

```csharp
public class AIFeatureCardVM : ViewModelBase {
    public string Key { get; }                 // "insights" | "recommendations" | "schedule" | "chat"
    public string AccentColor { get; }         // resource key to apply
    public StreamGeometry Icon { get; }
    public string Title { get; }
    public string PreviewSummary { get; }      // "3 active · 1 warning"
    public string? HighlightText { get; }      // top insight, optional
    public string? HighlightIcon { get; }      // "⚠" or "✓"
    public bool IsEnabled { get; set; }        // drives toggle; bound to IAppSettings.*Enabled
    public bool IsChatExperimental { get; }    // true only for Chat card (shows warning)
    public ICommand NavigateToDetail { get; }  // card body click
}
```

**Paused state:** When `IsEnabled = false`, card shows `Paused` in preview area, highlight row hidden, toggle visible as OFF. Card body click still navigates to detail (user lands on paused section, sees Enable button).

**Click targets:**
- Toggle switch: flips `IsEnabled` only (no navigation)
- Card body (anywhere else): `NavigateToDetail` command fires → `AIFeaturesViewModel.NavigateToSection(Key)`

### 4.5 Section refactor pattern

All 4 sections share a structural template:

```xml
<UserControl>
  <DockPanel>
    <!-- Header: title + status chip + master toggle -->
    <Border DockPanel.Dock="Top" Classes="section-header">
      <Grid ColumnDefinitions="*,Auto,Auto">
        <TextBlock Grid.Column="0" Classes="section-title"
                   Text="{Binding Title}" />
        <controls:StatusChip Grid.Column="1"
                             Text="{Binding StatusText}" />
        <controls:AuraToggle Grid.Column="2"
                             IsChecked="{Binding IsEnabled}" />
      </Grid>
    </Border>

    <!-- Permanent warning banner (ChatSection only) -->
    <Border DockPanel.Dock="Top" Classes="warning-banner"
            IsVisible="{Binding ShowWarning}">
      <TextBlock Text="{loc aiFeatures.chat.warningBanner}" />
    </Border>

    <!-- Paused overlay (shown when IsEnabled=false) -->
    <Panel IsVisible="{Binding !IsEnabled}">
      <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
        <TextBlock Text="{loc aiFeatures.section.paused.message}" />
        <Button Content="{loc aiFeatures.section.paused.enableButton}"
                Command="{Binding EnableCommand}" Classes="primary" />
      </StackPanel>
    </Panel>

    <!-- Active content -->
    <ScrollViewer IsVisible="{Binding IsEnabled}">
      <StackPanel>
        <!-- Section-specific content using Phase 1 primitives -->
      </StackPanel>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

### 4.6 Section content outlines

**InsightsSection:**
- Anomalies list → `InsightCard` (one per anomaly, amber accent for warnings)
- Patterns learned → `InsightCard` (teal accent, "Pattern Learned" title)
- Predictions → `InsightCard` (purple accent, "Prediction" title)
- **Recent Activity** (moved from Dashboard in Phase 2, now inside this section) → `GlassCard` with scrollable activity log

**RecommendationsSection:**
- Top: batch apply `HeroCTA` (if 2+ pending recommendations)
- List: each recommendation as `GlassCard` with apply/dismiss buttons
- Applied this week: collapsible `GlassCard` at bottom

**ScheduleSection:**
- Top: "Next run" summary `GlassCard` with countdown
- Timeline: upcoming scheduled runs → `InsightCard` list
- Patterns learned section → `GlassCard` with toggle per pattern (enable/disable using learned schedule)

**ChatSection:**
- Permanent warning banner at top (AccentAmber background)
- Header row below banner: `StatusChip` showing active model + dropdown trigger ("⚙ {Model Name} ▾")
- Message list scrollviewer (existing AIChatView bindings preserved)
- Input row at bottom (existing pattern preserved)
- **Model switcher dropdown** on chip click: lists installed models + "Download more..." opens `ModelManagerDialog` in Manage mode

### 4.7 Responsive behavior

- **≥1000 px:** Overview = 2×2 grid; Detail = 120 px sidebar + flex right pane
- **<1000 px (900–999):** Overview = 1 column × 4 cards stacked; Detail = 80 px icon-only sidebar + tighter right pane

### 4.8 Color/Icon mapping per feature

| Feature | Accent | Icon | Note |
|-|-|-|-|
| Insights | `AccentPurple` #B088FF | `IconSparklesFilled` | Filled variant — add to Icons.axaml |
| Recommendations | `AccentTeal` #00D4AA | `IconLightbulb` | New — add to Icons.axaml |
| Smart Schedule | `AccentAmber` #F59E0B | `IconCalendarClock` | New — add |
| Chat [Experimental] | `AccentPink` #EC4899 | `IconMessageSquare` | New — add; permanent ⚠ badge overlay |

**New icons needed (add to `Themes/Icons.axaml`):** `IconSparklesFilled`, `IconLightbulb`, `IconCalendarClock`, `IconMessageSquare`, `IconDownload`, `IconWarningTriangleFilled`, `IconLock` (for tier locking).

All Lucide SVG path strings; follow existing 30-icon pattern from Phase 1.

### 4.9 DI registrations

New registrations in composition root (`App.axaml.cs` or `Program.cs` — follow existing bootstrapping):

```csharp
// Services (singletons unless noted)
services.AddSingleton<IModelCatalog, ModelCatalog>();
services.AddSingleton<IInstalledModelStore, InstalledModelStore>();
services.AddSingleton<ICortexAmbientService, CortexAmbientService>();
services.AddSingleton<ITierService, TierService>();
services.AddTransient<IModelDownloadService, ModelDownloadService>();

// HttpClient factory for downloads (sets User-Agent globally)
services.AddHttpClient("ModelDownload", client => {
    client.Timeout = TimeSpan.FromMinutes(
        config.GetValue<int>("AICortex:DownloadTimeoutMinutes", 30));
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        config.GetValue<string>("AICortex:DownloadUserAgent")
        ?? "AuraCorePro/1.0 (+https://auracore.pro)");
});

// ViewModels (transient)
services.AddTransient<AIFeaturesViewModel>();
services.AddTransient<InsightsSectionViewModel>();           // renamed from AIInsightsViewModel
services.AddTransient<RecommendationsSectionViewModel>();    // renamed from RecommendationsViewModel
services.AddTransient<ScheduleSectionViewModel>();           // renamed from SchedulerViewModel
services.AddTransient<ChatSectionViewModel>();               // renamed from AIChatViewModel
services.AddTransient<ChatOptInDialogViewModel>();
services.AddTransient<ModelManagerDialogViewModel>();

// Views (transient — Avalonia instantiates via DI where applicable)
services.AddTransient<AIFeaturesView>();
services.AddTransient<ChatOptInDialog>();
services.AddTransient<ModelManagerDialog>();
services.AddTransient<InsightsSection>();
services.AddTransient<RecommendationsSection>();
services.AddTransient<ScheduleSection>();
services.AddTransient<ChatSection>();
```

**Removed registrations:**
- Old standalone view classes (`AIInsightsView`, `RecommendationsView`, `SchedulerView`, `AIChatView`)
- Old ViewModel names (replaced by renamed versions)
- `SmartOptimizePlaceholderDialog`

**Verify during implementation:**
- All missing-module Views (`system-health`, `admin-panel`, 4 Linux, 4 macOS) are DI-registered. Phase 2 already registered them; confirm with `grep "AddTransient<SystemHealth"` etc.

---

## 5. State Model

### 5.1 IAppSettings additions

```csharp
public interface IAppSettings {
    // existing fields preserved...

    // New for Phase 3
    bool InsightsEnabled { get; set; }          // default true
    bool RecommendationsEnabled { get; set; }   // default true
    bool ScheduleEnabled { get; set; }          // default true
    bool ChatEnabled { get; set; }              // default false (opt-in)
    bool ChatOptInAcknowledged { get; set; }    // default false — Step 1 of opt-in
    string? ActiveChatModelId { get; set; }     // null = no model selected
    DateTime? AIFirstEnabledAt { get; set; }    // tracks "learning day N"; set on first ON transition of any feature
}
```

Persistence mechanism: follow whatever `IAppSettings` already uses (JSON file in `%APPDATA%/AuraCorePro/`). No migration needed — missing keys default to the values above.

### 5.2 CortexAmbientService — state aggregation

```csharp
public interface ICortexAmbientService : INotifyPropertyChanged {
    bool AnyFeatureEnabled { get; }
    int EnabledFeatureCount { get; }
    int TotalFeatureCount { get; }  // always 4
    int LearningDay { get; }        // days since AIFirstEnabledAt; 0 if null
    string AggregatedStatusText { get; }
        // "Active · Learning day 3" / "Paused" / "Ready to start"
    CortexActiveness Activeness { get; }
        // Active | Paused | Ready
}

public enum CortexActiveness { Active, Paused, Ready }
```

Implementation watches `IAppSettings` for toggle changes (via its `INotifyPropertyChanged` or explicit subscription), recomputes aggregated state, fires PropertyChanged for subscribers.

**On first ON transition of any feature:** sets `IAppSettings.AIFirstEnabledAt = DateTime.UtcNow` (only if currently null — preserves initial learning-day anchor).

**Subscribers:**
- `AIFeaturesViewModel.HeroStatusText` (binding)
- `DashboardViewModel` — controls Cortex Insights card visibility, Cortex AI chip, header subtitle
- `StatusBarViewModel` — controls status bar message

### 5.3 Ripple effects — master table

| UI Element | Location | Behavior by state |
|-|-|-|
| Dashboard `Cortex Insights` card | `DashboardView` | `InsightsEnabled = true`: rendered normally. `false`: card replaced by subtle "AI Insights paused — Enable in AI Features" placeholder with link |
| Dashboard "Cortex AI · ON" chip (header) | `DashboardView` | Any feature ON: green dot + "ON". All OFF: grey dot + "OFF". Click always navigates to AIFeaturesView |
| Dashboard header subtitle ("Cortex is monitoring...") | `DashboardView` | `InsightsEnabled = true`: shown. `false`: hidden |
| Smart Optimize Hero CTA | `DashboardView` | Primary button enabled iff `RecommendationsEnabled`. If disabled: CTA text becomes "Enable Recommendations to use Smart Optimize", click opens AIFeaturesView > Recommendations section |
| Status bar `✦ Cortex · Learning day N` | `StatusBarView` | `AnyFeatureEnabled`: "Active · Learning day N". All OFF with `AIFirstEnabledAt` set: "Paused". First run (`AIFirstEnabledAt` null): "Ready" |
| Sidebar "✦ AI Features [CORTEX]" link | `MainWindow` | Always visible with CORTEX badge, no state-based change in Phase 3 |

---

## 6. Model Management (Phase 3 minimum)

### 6.1 ModelCatalog — 8 models, hardcoded

```csharp
public record ModelDescriptor(
    string Id,
    string DisplayName,
    string Filename,
    long SizeBytes,
    long EstimatedRamBytes,
    ModelTier Tier,
    SpeedClass Speed,
    bool IsRecommended,
    string DescriptionKey);  // localization key

public enum ModelTier { Lite, Standard, Advanced, Heavy }
public enum SpeedClass { Fast, Medium, Slow }

public interface IModelCatalog {
    IReadOnlyList<ModelDescriptor> All { get; }
    ModelDescriptor? FindById(string id);
    ModelDescriptor? FindByFilename(string filename);
}
```

Catalog entries (note: size/RAM values are rounded; exact bytes from real files):

| Id | Display | Filename | Size | RAM | Tier | Speed | Recommended |
|-|-|-|-|-|-|-|-|
| tinyllama | TinyLlama | auracore-tinyllama.gguf | 2.1 GB | 2 GB | Lite | Fast | No |
| phi3-mini-q4km | Phi-3 Mini Q4KM | auracore-phi3-mini-q4km.gguf | 2.3 GB | 3 GB | Lite | Fast | **Yes** |
| phi2 | Phi-2 | auracore-phi2.gguf | 5.3 GB | 6 GB | Standard | Medium | No |
| phi3-mini | Phi-3 Mini | auracore-phi3-mini.gguf | 7.3 GB | 8 GB | Standard | Medium | No |
| mistral-7b | Mistral 7B | auracore-mistral-7b.gguf | 14 GB | 16 GB | Advanced | Slow | No |
| llama31-8b | Llama 3.1 8B | auracore-llama31-8b.gguf | 15 GB | 18 GB | Advanced | Slow | No |
| phi3-medium | Phi-3 Medium | auracore-phi3-medium.gguf | 26.6 GB | 32 GB | Heavy | Slow | No |
| qwen25-32b | Qwen 2.5 32B | auracore-qwen25-32b.gguf | 62.5 GB | 70 GB | Heavy | Slow | No |

Descriptions localized via `modelManager.model.{id}.description` keys (see §9).

### 6.2 Download service

```csharp
public interface IModelDownloadService {
    Task<FileInfo> DownloadAsync(
        ModelDescriptor model,
        IProgress<DownloadProgress> progress,
        CancellationToken ct);
}

public record DownloadProgress(
    long BytesReceived,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan? EstimatedTimeRemaining);
```

Implementation notes:
- `HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)` → stream
- URL: `{ModelCatalogBaseUrl}/{model.Filename}` (base from appsettings; defaults to `https://models.auracore.pro`)
- Buffer: 256 KB (configurable via `DownloadBufferKb`)
- Write target: `{ModelInstallDirectory}/{filename}.download` (temp name)
- On success: **verify downloaded size matches `model.SizeBytes`** (±1 MB tolerance for metadata rounding) — if mismatch, delete file, throw `ModelSizeMismatchException`
- On success + size verified: rename `.download` → `.gguf` (atomic)
- On cancel/error: delete partial `.download` file
- Timeout: 30 min (configurable `DownloadTimeoutMinutes`)
- **User-Agent header:** `AuraCorePro/1.0 (+https://auracore.pro)` (set globally on HttpClient — bypasses Cloudflare Bot Fight Mode; see Risk R1)

Size verification is a cheap integrity check (no cryptographic checksum needed). Phase 4 will add SHA256 verification for stronger guarantees.

### 6.3 Installed model store

```csharp
public interface IInstalledModelStore {
    IReadOnlyList<InstalledModel> Enumerate();
    bool IsInstalled(string modelId);
    FileInfo? GetFile(string modelId);
    // Phase 4+: Task DeleteAsync(string modelId);
}

public record InstalledModel(
    string ModelId,
    FileInfo File,
    long SizeBytes,
    DateTime DownloadedAt);
```

Disk enumeration: scan `ModelInstallDirectory` for `auracore-*.gguf` files. For each file, lookup catalog via `ModelCatalog.FindByFilename(name)` to resolve ModelId. Files that don't match a catalog entry are ignored (orphan — could be stale test data; don't surface but don't crash).

### 6.4 Storage paths (cross-platform)

- **Windows:** `%LOCALAPPDATA%/AuraCorePro/models/`
- **macOS:** `~/Library/Application Support/AuraCorePro/models/`
- **Linux:** `~/.local/share/AuraCorePro/models/`

Use `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` for cross-platform resolution.

### 6.5 Config (appsettings.json additions)

```json
"AICortex": {
  "ModelCatalogBaseUrl": "https://models.auracore.pro",
  "ModelInstallDirectory": null,
  "DownloadTimeoutMinutes": 30,
  "DownloadBufferKb": 256,
  "DownloadUserAgent": "AuraCorePro/1.0 (+https://auracore.pro)"
}
```

`ModelInstallDirectory: null` → use platform default per §6.4. Non-null value overrides (for testing or custom installs).

### 6.6 Infrastructure reference (set up 2026-04-15)

- Cloudflare R2 bucket: `auracore-models` (ENAM region, `d4bd9fd5051b4077aa885a006c4db219`)
- Custom domain: `models.auracore.pro` — SSL Active, HSTS enabled
- Cache Rule ("Cache AuraCore Models"): Hostname equals `models.auracore.pro`, Edge TTL 1 year, Browser TTL 1 year
- HSTS: Cloudflare (6 months, includeSubDomains) + origin Nginx (1 year, includeSubDomains, preload directive — not submitted to hstspreload.org)
- Min TLS: 1.2 (TLS 1.3 also enabled)
- Bot Fight Mode: enabled (app sends custom User-Agent to pass)
- Continuous Script Monitoring: enabled (not relevant for R2, bonus for other subdomains)

**Known behavior:** Files > 512 MB return `cf-cache-status: DYNAMIC` on Cloudflare Free plan. This is expected. R2 egress is free regardless of cache status; downloads go through Cloudflare's global network with TLS/HTTP2/HTTP3. No cost or significant performance penalty.

**Model update protocol (documented for future reference):** If a model file needs updating, use a versioned filename (e.g., `auracore-phi3-mini-q4km-v2.gguf`) rather than overwriting. Cache rule TTL is 1 year; overwriting the same filename would serve stale content until purge. Versioned filenames require catalog update (another `ModelDescriptor` entry).

---

## 7. Dialogs

### 7.1 ChatOptInDialog — 2-step modal

**Step 1: Experimental acknowledgment**

- Title: `⚠ CORTEX Chat — Experimental`
- Body: full warning text (localized key `chatOptIn.step1.body`)
- Buttons: `[I understand, continue]` / `[Cancel]`

On Continue: `IAppSettings.ChatOptInAcknowledged = true`, dialog advances to Step 2 (without closing).
On Cancel: close dialog, `ChatEnabled` stays false, `ChatOptInAcknowledged` stays false.

**Step 2: Model selection**

Rather than duplicate UI, Step 2 mounts `ModelManagerDialog` in `OptIn` mode inside the ChatOptInDialog container. The outer dialog adds a "Step 2 of 2" indicator.

When ModelManagerDialog reports "Model downloaded + selected":
- `IAppSettings.ActiveChatModelId = selectedModelId`
- `IAppSettings.ChatEnabled = true`
- Close ChatOptInDialog

If ModelManagerDialog cancels:
- `ChatOptInAcknowledged` stays true (warning was seen)
- `ChatEnabled` stays false
- Close ChatOptInDialog

**Resuming from ack state:** When user later toggles Chat ON and `ChatOptInAcknowledged == true && ActiveChatModelId == null`, skip Step 1 and open Step 2 directly.

### 7.2 ModelManagerDialog — two modes

```csharp
public enum ModelManagerDialogMode { OptIn, Manage }

public class ModelManagerDialogViewModel : ViewModelBase {
    public ModelManagerDialogMode Mode { get; }
    public IReadOnlyList<ModelListItemVM> Models { get; }
    public ModelListItemVM? SelectedModel { get; set; }
    public bool CanDownload { get; }
    public DownloadProgress? ActiveDownload { get; }
    public string? ErrorMessage { get; }
    public ICommand DownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand CancelDialogCommand { get; }
}

public class ModelListItemVM {
    public ModelDescriptor Model { get; }
    public bool IsInstalled { get; }
    public bool IsSelectable { get; }       // false if RAM insufficient
    public string? DisabledReason { get; }  // "Needs 32 GB RAM"
}
```

**OptIn mode:**
- Title: `Choose your AI model`
- Subtitle: `Please select a model to download...`
- Primary button: `Download & use`
- On complete: dialog returns selected `ModelDescriptor` to caller

**Manage mode:**
- Title: `Manage AI Models`
- Subtitle: `Download additional models or switch active model`
- Installed models show ✓ badge
- Primary button: `Download` (only enabled if selection is not-yet-installed)
- On complete: new model downloaded; dialog closes; **does not change active model** (that happens via chat header dropdown)

**Shared layout (both modes):**
- Tiered sections (Lite / Standard / Advanced / Heavy) with tier header showing RAM requirement
- Columns: Model name + description | Size | RAM estimate | Speed badge
- Phi-3 Mini Q4KM shows `RECOMMENDED` badge (not pre-selected)
- Footer: `Disk free: {X} · Your RAM: {Y}`
- **RAM-aware disable:** Catalog entries requiring more RAM than `Environment.WorkingSet` / total physical RAM are visually dimmed with tooltip "Needs {tier.ramRequirement}"

**Download UI (when button clicked):**
- Progress bar (0–100%)
- Status line: `Downloading {ModelName} · 42% · 8.3 MB/s · ETA 3m 21s`
- Cancel button
- On completion: dialog's primary state transitions (either close for OptIn, or return to selection for Manage)

**Error states (inline above action buttons):**
- Network error → text + Retry button
- Timeout → text + Retry / Choose smaller model
- Disk full → text (with required space) + Choose different model
- Bot Fight Mode rejection (HTTP 403 with Cloudflare challenge body) → text + diagnostic info + "Report issue" link (support contact)

---

## 8. Main Sidebar Changes

### 8.1 Accordion cleanup

**Before (Phase 2):**

```
✦ AI Features (accordion, expanded)
  • AI Insights
  • AI Recommendations
  • Smart Scheduler
  • AI Chat [Experimental]
```

**After (Phase 3):**

```
✦ AI Features [CORTEX]      ← single clickable link, purple accent
```

`SidebarViewModel.BuildCategories()` adjustments:
- AI Features category contains exactly one module: `{ Key = "ai-features", View = nameof(AIFeaturesView) }`
- Rendering logic in XAML: if category has exactly one module, render it as a direct link (no accordion chevron/expand behavior)

Old localization keys `nav.ai.insights`, `nav.ai.recommendations`, `nav.ai.scheduler`, `nav.ai.chat` **kept** in LocalizationService (reserved for potential deep-link URL routing in Phase 4+); **removed** from sidebar data. Stale localization keys are cheap; orphan navigation targets are not.

### 8.2 Tier locking re-introduction (Path A)

**SidebarNavItem gets a new dependency property:**

```csharp
public static readonly StyledProperty<bool> IsLockedProperty =
    AvaloniaProperty.Register<SidebarNavItem, bool>(nameof(IsLocked));

public bool IsLocked {
    get => GetValue(IsLockedProperty);
    set => SetValue(IsLockedProperty, value);
}
```

Visual states (via XAML selectors/pseudoclasses):
- `IsLocked = true`: opacity 0.5, appended `IconLock` at right, cursor `NotAllowed`, tooltip bound to `{loc tier.lockedTooltip}`
- Hover on locked item: no highlight animation
- Click on locked item: routes to `ShowTierUpgradeDialog(moduleKey)` instead of navigation

**SidebarViewModel wire-up:**

```csharp
// In BuildCategories() per-module loop:
foreach (var module in category.Modules) {
    var navItem = new SidebarNavItemViewModel {
        Key = module.Key,
        LabelKey = module.LabelKey,
        Icon = module.Icon,
        // NEW:
        IsLocked = _tierService.IsModuleLocked(module.Key, _userSession.Tier),
    };
    ...
}
```

**TierService contract** (recreate based on pre-Phase-2 `ApplyTierLocking` logic — see Risk R8):

```csharp
public interface ITierService {
    bool IsModuleLocked(string moduleKey, UserTier userTier);
    UserTier GetRequiredTier(string moduleKey);
}

public enum UserTier { Free, Pro, Enterprise, Admin }
```

Logic:
- Admin tier: everything unlocked
- Enterprise tier: Same as Pro tier with enterprise exclusive admin control panel (if added in the future)
- Pro tier: everything except tier-restricted premium modules (if any)
- Free tier: basic modules only

Module → required tier mapping: recover from git history. If no mapping existed (tier system was only UI-stubbed), define a minimal conservative mapping: all current modules = Free tier, `admin-panel` = Admin tier, placeholder for future premium features.

**Locked item click handling:**

```csharp
public void OnSidebarItemClick(SidebarModule module) {
    if (_tierService.IsModuleLocked(module.Key, _userSession.Tier)) {
        _dialogService.ShowTierUpgradeDialog(module.Key);
        return;
    }
    _navigationService.Navigate(module.View);
}
```

`ShowTierUpgradeDialog` in Phase 3 = simple placeholder dialog showing:
- Title: `{loc tier.upgrade.dialog.title}` — "Feature locked"
- Body: `{loc tier.upgrade.dialog.body}` with `{tier}` placeholder filled by required tier
- Button: `Close`

Full upgrade UX (payment, tier change, admin contact) deferred to Phase 5 (Settings + Onboarding cohesion).

### 8.3 Missing modules added to sidebar

New entries in `SidebarViewModel.BuildCategories()`:

| Module | Category | Platform Filter | Required Tier |
|-|-|-|-|
| `system-health` | Apps & Tools | all platforms | Free |
| `admin-panel` | Advanced (below divider) | all platforms | **Admin** |
| `systemd-manager` | Optimize | Linux only | Free |
| `package-cleaner` | Clean & Debloat | Linux only | Free |
| `swap-optimizer` | Optimize | Linux only | Free |
| `cron-manager` | Advanced | Linux only | Free |
| `defaults-optimizer` | Apps & Tools | macOS only | Free |
| `launchagent-manager` | Advanced | macOS only | Free |
| `brew-manager` | Apps & Tools | macOS only | Free |
| `timemachine-manager` | Security | macOS only | Free |

All corresponding View classes already exist and compile (verified during Phase 2 — they just weren't reachable). Verify DI registration during implementation; add any missing registrations.

**Platform filter implementation:** `RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` / `OSPlatform.OSX` etc. in the SidebarViewModel category builder. Module is simply excluded from the category's Modules list if platform doesn't match.

---

## 9. Localization

All new user-facing strings go through `LocalizationService`. Full key list below (both EN and TR must be populated).

### 9.1 Navigation
- `nav.aiFeatures.title` — "AI Features" / "Yapay Zekâ"
- `nav.aiFeatures.badge` — "CORTEX" / "CORTEX" (not translated)

### 9.2 AI Features page
- `aiFeatures.hero.kicker` — "CORTEX" / "CORTEX"
- `aiFeatures.hero.title` — "AI Features" / "Yapay Zekâ"
- `aiFeatures.hero.tagline` — "Intelligent monitoring, predictions, and automation" / "Akıllı izleme, tahminler ve otomasyon"
- `aiFeatures.hero.status.active` — "Active · Learning day {0}" / "Aktif · Öğrenme günü {0}"
- `aiFeatures.hero.status.paused` — "Paused" / "Duraklatıldı"
- `aiFeatures.hero.status.ready` — "Ready to start" / "Başlamaya hazır"
- `aiFeatures.overview.navItem` — "Overview" / "Genel Bakış"
- `aiFeatures.card.insights.title` — "Cortex Insights" / "Cortex İçgörüler"
- `aiFeatures.card.insights.previewSingular` — "{0} active · {1} warning" / "{0} aktif · {1} uyarı"
- `aiFeatures.card.insights.previewPlural` — "{0} active · {1} warnings" / "{0} aktif · {1} uyarı" (Turkish: same form)
- `aiFeatures.card.recommendations.title` — "Recommendations" / "Öneriler"
- `aiFeatures.card.recommendations.preview` — "{0} pending · {1} applied this week" / "{0} bekliyor · bu hafta {1} uygulandı"
- `aiFeatures.card.schedule.title` — "Smart Schedule" / "Akıllı Zamanlama"
- `aiFeatures.card.schedule.preview` — "Next: {0} at {1}" / "Sonraki: {0} · {1}"
- `aiFeatures.card.chat.title` — "Chat" / "Sohbet"
- `aiFeatures.card.chat.previewEnabled` — "{0} · {1} messages" / "{0} · {1} mesaj"
- `aiFeatures.card.chat.previewDisabled` — "Not enabled" / "Etkin değil"
- `aiFeatures.card.chat.experimentalBadge` — "EXPERIMENTAL" / "DENEYSEL"
- `aiFeatures.card.paused.preview` — "Paused" / "Duraklatıldı"
- `aiFeatures.section.paused.message` — "This feature is paused" / "Bu özellik duraklatıldı"
- `aiFeatures.section.paused.enableButton` — "Enable" / "Etkinleştir"
- `aiFeatures.chat.warningBanner` — "⚠ Experimental — CORTEX Chat may produce inaccurate outputs. Verify before applying any suggestion." / "⚠ Deneysel — CORTEX Sohbet hatalı yanıtlar üretebilir. Herhangi bir öneri uygulamadan önce doğrulayın."

### 9.3 Chat opt-in dialog
- `chatOptIn.step1.title` — "CORTEX Chat — Experimental"
- `chatOptIn.step1.body` — "CORTEX Chat uses a local AI model that may produce inaccurate or misleading outputs. Always verify any suggestions before applying them to your system.\n\nThis feature is under active development." / Turkish equivalent
- `chatOptIn.step1.continueButton` — "I understand, continue" / "Anladım, devam et"
- `chatOptIn.step1.cancelButton` — "Cancel" / "İptal"
- `chatOptIn.step2.title` — "Choose your AI model" / "AI modelini seç"
- `chatOptIn.step2.subtitle` — "Please select a model to download. Models are pulled from AuraCore cloud. You can switch or download more later from the Chat header." / Turkish equivalent
- `chatOptIn.stepIndicator` — "Step {0} of {1}" / "Adım {0}/{1}"

### 9.4 Model manager dialog
- `modelManager.title.optIn` — "Choose your AI model" / "AI modelini seç"
- `modelManager.title.manage` — "Manage AI Models" / "AI Modellerini Yönet"
- `modelManager.tier.lite` — "Lite" / "Hafif"
- `modelManager.tier.standard` — "Standard" / "Standart"
- `modelManager.tier.advanced` — "Advanced" / "Gelişmiş"
- `modelManager.tier.heavy` — "Heavy" / "Ağır"
- `modelManager.tier.ramRequirement` — "needs {0} GB+ RAM" / "en az {0} GB RAM gerekir"
- `modelManager.column.model` — "Model" / "Model"
- `modelManager.column.size` — "Size" / "Boyut"
- `modelManager.column.ram` — "RAM" / "RAM"
- `modelManager.column.speed` — "Speed" / "Hız"
- `modelManager.speed.fast` — "FAST" / "HIZLI"
- `modelManager.speed.medium` — "MEDIUM" / "ORTA"
- `modelManager.speed.slow` — "SLOW" / "YAVAŞ"
- `modelManager.recommended` — "RECOMMENDED" / "ÖNERİLEN"
- `modelManager.installedBadge` — "Installed" / "Yüklü"
- `modelManager.downloadButton` — "Download & use" / "İndir ve kullan"
- `modelManager.downloadManageButton` — "Download" / "İndir"
- `modelManager.cancelButton` — "Cancel" / "İptal"
- `modelManager.stats` — "Disk free: {0} · Your RAM: {1}" / "Boş disk: {0} · RAM: {1}"

### 9.5 Model descriptions (8 × 2 = 16 strings)

Key pattern: `modelManager.model.{id}.description`. Example entries:

- `modelManager.model.tinyllama.description` — "Fastest · basic quality · good for quick questions" / "En hızlısı · temel kalite · hızlı sorular için"
- `modelManager.model.phi3-mini-q4km.description` — "Best balance · fine-tuned for PC optimization" / "En dengelisi · PC optimizasyonu için özelleştirildi"
- `modelManager.model.phi2.description` — "Microsoft Phi-2 · stronger reasoning" / "Microsoft Phi-2 · daha güçlü akıl yürütme"
- `modelManager.model.phi3-mini.description` — "Full-precision Phi-3 mini · better fidelity" / "Tam hassasiyet Phi-3 mini · daha yüksek kalite"
- `modelManager.model.mistral-7b.description` — "High quality · multilingual" / "Yüksek kalite · çok dilli"
- `modelManager.model.llama31-8b.description` — "Meta · high quality · latest" / "Meta · yüksek kalite · en güncel"
- `modelManager.model.phi3-medium.description` — "Best reasoning in 14B class · workstation hardware" / "14B sınıfında en güçlü · iş istasyonu donanımı gerektirir"
- `modelManager.model.qwen25-32b.description` — "Highest quality · may not run on 32 GB systems" / "En yüksek kalite · 32 GB sistemlerde çalışmayabilir"

### 9.6 Download UI
- `modelDownload.progress` — "Downloading {0} · {1}% · {2} MB/s · ETA {3}" / "İndiriliyor {0} · %{1} · {2} MB/sn · KS {3}"
- `modelDownload.error.network` — "Couldn't reach models.auracore.pro. Check your connection and try again." / "models.auracore.pro adresine ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin."
- `modelDownload.error.timeout` — "Download took too long. Try a smaller model or check your connection." / "İndirme çok uzun sürdü. Daha küçük bir model deneyin veya bağlantınızı kontrol edin."
- `modelDownload.error.diskFull` — "Not enough disk space. You need {0} GB free." / "Yeterli disk alanı yok. {0} GB boş alan gerekiyor."
- `modelDownload.error.blocked` — "Download blocked by server. Please contact support." / "İndirme sunucu tarafından engellendi. Lütfen destek ile iletişime geçin."
- `modelDownload.retryButton` — "Retry" / "Yeniden dene"

### 9.7 Tier locking
- `tier.lockedTooltip` — "Upgrade to {0} to unlock" / "Kilidi açmak için {0} sürümüne yükseltin"
- `tier.upgrade.dialog.title` — "Feature locked" / "Özellik kilitli"
- `tier.upgrade.dialog.body` — "This feature requires {0} tier. Contact admin to upgrade." / "Bu özellik {0} sürümünü gerektirir. Yükseltme için yönetici ile iletişime geçin."

### 9.8 Missing module labels

Keys added for the 10 newly-added sidebar modules (8.3). Pattern: `nav.module.{module-key}`. Only listing for reference; follow existing convention:

- `nav.module.system-health` — "System Health" / "Sistem Sağlığı"
- `nav.module.admin-panel` — "Admin Panel" / "Yönetim Paneli"
- `nav.module.systemd-manager` — "Systemd Manager" / "Systemd Yöneticisi"
- `nav.module.package-cleaner` — "Package Cleaner" / "Paket Temizleyici"
- `nav.module.swap-optimizer` — "Swap Optimizer" / "Swap Optimizasyonu"
- `nav.module.cron-manager` — "Cron Manager" / "Cron Yöneticisi"
- `nav.module.defaults-optimizer` — "Defaults Optimizer" / "Defaults Optimizasyonu"
- `nav.module.launchagent-manager` — "Launch Agent Manager" / "Launch Agent Yöneticisi"
- `nav.module.brew-manager` — "Brew Manager" / "Brew Yöneticisi"
- `nav.module.timemachine-manager` — "Time Machine Manager" / "Time Machine Yöneticisi"

---

## 10. Error Handling

| Scenario | Handling |
|-|-|
| AIFeaturesViewModel fails to load (e.g., IAppSettings corrupt) | Safe defaults applied (Insights/Recs/Schedule = true, Chat = false); log error; overview renders normally |
| Section ViewModel throws during refresh | Section card shows "Error — {message}"; other cards unaffected; error logged |
| ChatSection ActiveModel file missing from disk | Section shows "Model file not found." with `[Re-download]` (opens ModelManagerDialog pre-selected) or `[Choose different model]` |
| ChatOptInDialog cancelled mid-download | Partial `.download` file deleted; ChatEnabled stays false; no persisted state change |
| ModelManagerDialog: user clicks model whose file already exists on disk | Skip download, switch active model directly (OptIn mode) or show "Already installed" (Manage mode) |
| SidebarNavItem clicked but module View not registered in DI | Log error; show toast "This feature is unavailable in the current build." |
| Platform-specific module appears on wrong OS (filter bug) | Filter logic in SidebarViewModel is source of truth; integration test prevents regression |
| Cloudflare Bot Fight Mode blocks download (HTTP 403 with challenge) | Detect via status + response body inspection; show `modelDownload.error.blocked` with support contact |
| Disk write permission denied in install dir | Show error with install dir path; suggest run as admin / check permissions |
| Model file corrupt (fails to load in llama.cpp) | Chat load shows "Model failed to load. [Re-download] or [Choose different model]"; Phase 4 adds checksum prevention |
| User has insufficient RAM for installed active model | ChatSection shows "This model needs {X} GB RAM. [Choose different model]" |
| AIFirstEnabledAt gets corrupted (e.g., future timestamp) | CortexAmbientService clamps LearningDay to `[0, ∞)`; if future, resets to 0 |

---

## 11. Testing Strategy

### 11.1 Unit tests — new (~40)

| Test | Verifies |
|-|-|
| `AIFeaturesViewModel_Initialize_StartsInOverviewMode` | Default mode is Overview |
| `AIFeaturesViewModel_NavigateToSection_ChangesMode` | Command transitions to Detail + sets ActiveSection |
| `AIFeaturesViewModel_NavigateToOverview_ReturnsToGrid` | "Overview" nav command returns to Overview mode |
| `AIFeaturesViewModel_HeroText_ReflectsEnabledCount` | "Active · Learning day N" / "Paused" / "Ready" |
| `AIFeaturesViewModel_TogglingFeature_UpdatesSettings` | IAppSettings flag flipped atomically |
| `AIFeaturesViewModel_ActiveSectionView_CachedAcrossNav` | Same UserControl instance returned on re-navigation |
| `CortexAmbientService_AllOff_ReportsPaused` | Aggregation = Paused when all four false |
| `CortexAmbientService_OneOn_ReportsActive` | Any ON → Active |
| `CortexAmbientService_FirstEnableStampsAIFirstEnabledAt` | Timestamp set once, not overwritten |
| `CortexAmbientService_LearningDay_CountsDaysSinceFirstEnabled` | Day math correct across midnight |
| `CortexAmbientService_LearningDay_ClampsFutureTimestamps` | Corrupt future timestamp → 0 |
| `CortexAmbientService_TogglesUpdate_Propagate` | INotifyPropertyChanged fires |
| `ModelDownloadService_Success_CreatesGgufFile` | Happy path writes .gguf after .download rename |
| `ModelDownloadService_Cancel_DeletesPartial` | Cleanup of .download file |
| `ModelDownloadService_TimeoutExceeded_Throws` | Timeout enforced; partial cleaned |
| `ModelDownloadService_NetworkError_Surfaces` | Error wrapped, not thrown raw |
| `ModelDownloadService_SetsCustomUserAgent` | UA header matches config |
| `ModelCatalog_Contains8Models` | All 8 entries present |
| `ModelCatalog_Phi3MiniQ4KM_IsRecommended` | Only Phi-3 Mini Q4KM has IsRecommended=true |
| `ModelCatalog_HeavyTier_RequiresHighRam` | Heavy tier min RAM ≥ 32 GB |
| `ModelCatalog_FindById_Works` | Lookup by id |
| `ModelCatalog_FindByFilename_Works` | Lookup by filename |
| `InstalledModelStore_EnumeratesOnlyGgufFiles` | Filters non-gguf |
| `InstalledModelStore_MapsFilenameToModelId` | Catalog lookup succeeds |
| `InstalledModelStore_OrphanFiles_Ignored` | Unknown gguf doesn't crash |
| `InstalledModelStore_MissingDir_ReturnsEmpty` | Graceful empty |
| `ChatOptInDialogViewModel_Step1Continue_AdvancesToStep2` | State machine |
| `ChatOptInDialogViewModel_Step1Cancel_KeepsChatDisabled` | No persisted state change |
| `ChatOptInDialogViewModel_Step2Complete_EnablesChat` | Final state flip |
| `ChatOptInDialogViewModel_ResumeFromAck_SkipsStep1` | Step 2 direct when acknowledged |
| `ModelManagerDialogViewModel_OptInMode_RequiresSelection` | Download disabled until selection |
| `ModelManagerDialogViewModel_ManageMode_MarksInstalled` | IsInstalled shown correctly |
| `ModelManagerDialogViewModel_InsufficientRam_DisablesHeavyTier` | RAM-aware disable |
| `TierService_IsModuleLocked_ReturnsTrueForLockedModule` | Locking logic |
| `TierService_AdminTier_UnlocksEverything` | Admin bypass |
| `SidebarViewModel_Phase3_SingleAIFeaturesLink` | Accordion removed |
| `SidebarViewModel_MissingModulesAdded_Linux` | Linux-only modules appear |
| `SidebarViewModel_MissingModulesAdded_MacOS` | macOS-only modules appear |
| `SidebarViewModel_SystemHealthInAppsTools` | Category correctness |
| `SidebarViewModel_AdminPanelInAdvanced_AdminOnly` | Tier + category |

### 11.2 View tests — new (~12)

Using Avalonia.Headless pattern (as in Phase 2 MainWindowTests/DashboardViewTests).

| Test | Verifies |
|-|-|
| `AIFeaturesView_HeroRenders` | Kicker + title + tagline + status chip visible |
| `AIFeaturesView_OverviewGrid_Shows4Cards` | UniformGrid renders all four AIFeatureCards |
| `AIFeaturesView_DetailMode_ShowsSidebar` | Mode switching updates visibility |
| `AIFeaturesView_NarrowMode_StacksCards` | Responsive layout at < 1000 px |
| `InsightsSection_BindsToViewModel` | XAML bindings resolve |
| `RecommendationsSection_BindsToViewModel` | - |
| `ScheduleSection_BindsToViewModel` | - |
| `ChatSection_ShowsWarningBanner` | Banner always visible regardless of toggle |
| `ChatSection_PausedState_ShowsEnableButton` | Paused overlay UI |
| `ChatOptInDialog_Step1Renders` | Warning text + buttons visible |
| `ModelManagerDialog_TierSections_Render` | 4 tiers shown with headers |
| `SidebarNavItem_IsLocked_DimsAndShowsIcon` | Locked visual state |

### 11.3 Integration tests — new (~8)

| Test | Verifies |
|-|-|
| `ChatOptInFlow_EndToEnd_EnablesChat` | Step1 → Step2 → mock download → ChatEnabled=true, ActiveChatModelId set |
| `ChatOptInFlow_Step2Cancel_PreservesAcknowledgment` | Ack persists across attempts |
| `DashboardRipple_InsightsOff_HidesInsightsCard` | DashboardViewModel reacts to toggle |
| `DashboardRipple_AllOff_SmartOptimizeDisabled` | CTA disabled when Recommendations off |
| `StatusBarRipple_AnyOn_ShowsLearningDay` | StatusBarViewModel updates |
| `MainSidebar_Phase3_NoAccordion` | Sidebar rebuilds with single AI Features link |
| `MainSidebar_TierLocking_DimsLockedModules` | IsLocked applied correctly for simulated user tiers |
| `MainSidebar_PlatformFilter_LinuxShowsSystemd` | Platform modules visible on correct OS |

### 11.4 Target test count

- Phase 2 baseline: 283 tests (256 + 27 from Phase 2)
- Phase 3 target: **~343 tests** (283 + ~60 new: 40 unit + 12 view + 8 integration) all green, zero regression

### 11.5 Manual visual checks (documented, not automated)

1. **Launch app → visual verify bccd8ed** (Quick Actions 2×2 evenly stretched) — **FIRST STEP before any code changes**
2. Login → dashboard renders, Cortex Insights card visible (default: Insights ON)
3. Click sidebar "✦ AI Features [CORTEX]" → AIFeaturesView overview renders with 2×2 grid
4. Click Insights card → detail mode opens with sidebar nav + Insights content
5. Click "Overview" in detail sidebar → returns to 2×2 grid
6. Toggle Insights OFF via card toggle → dashboard Cortex Insights card swaps to placeholder, status bar shows "Paused" (if only feature on)
7. Toggle Chat ON for first time → ChatOptInDialog Step 1 shown
8. Click "I understand, continue" → Step 2 ModelManagerDialog
9. Select Phi-3 Mini Q4KM → Download button enabled → click → progress bar → completion
10. Chat section active with selected model shown in header chip
11. Click model chip → dropdown shows installed model + "Download more..."
12. Resize window < 1000 px → AIFeaturesView switches to narrow mode (1-column overview)
13. Navigate to a tier-locked module → dimmed in sidebar, click shows tier upgrade dialog
14. On Linux VM: `systemd-manager` visible in Optimize category (run via `dotnet run` on Ubuntu)
15. On macOS VM: `brew-manager` visible in Apps & Tools

---

## 12. Out of Scope (Explicit)

- Settings > Models page (Phase 4+)
- Model delete, auto-update, checksum validation, pause/resume download (Phase 4+)
- Signed URLs / per-user access control for downloads (Phase 4+)
- Multi-model benchmark comparison UI (Phase 4+)
- Tier upgrade flow UX (Phase 5 — Settings/Onboarding cohesion); Phase 3 ships placeholder dialog
- Light theme (Vision §12)
- V1 theme bridge removal (Phase 4)
- Localization beyond EN/TR (Vision §12)
- Mobile layout (Vision §12)
- Deep-link URL routing (e.g., `auracore://ai-features/insights`)
- Visual regression tests (screenshot diff) — acknowledged gap from Phase 1, still deferred
- Real AI inference tests (adapter layer untouched; existing tests suffice)
- CORTEX branding redesign (logo, typography changes — Vision §4 stays authoritative)

---

## 13. Risk Registry

| # | Risk | Impact | Mitigation |
|-|-|-|-|
| R1 | Bot Fight Mode blocks app User-Agent | Model download fails | App sends custom UA `AuraCorePro/1.0 (+https://auracore.pro)`; integration test verifies; if still blocked, add Cloudflare firewall allow-rule for this UA |
| R2 | Large model (Qwen 64 GB) download timeout | Half-finished download, user frustration | 30-min timeout enforced; error with "Try smaller model" hint; partial file cleanup; Phase 4 adds resume |
| R3 | Avalonia UserControl lifecycle leak (detail pane switching) | Memory usage grows over time | Cache UserControl instances in AIFeaturesViewModel; dispose properly on ViewModel dispose; integration test monitors memory |
| R4 | Chat message history lost when toggle OFF | User frustration | Toggle only sets IsEnabled; ChatSectionViewModel.MessageList preserved in memory; existing disk persistence behavior (if any) unchanged |
| R5 | ViewModel rename (AIInsightsViewModel → InsightsSectionViewModel) breaks DI | Build failure | Rename + DI registration update in single atomic commit; validation build runs before subsequent commits |
| R6 | Phase 1 primitives insufficient for some section content (e.g., chat message input) | Need new primitives mid-implementation | Pilot section refactor (ScheduleSection — simplest) first to detect; add primitives if needed (small scope creep acceptable) |
| R7 | R2 download speed slow from Turkey (bucket in ENAM) | Poor first-time UX | Cloudflare edge routing helps; if still insufficient, Phase 4 considers bucket migrate to EEUR |
| R8 | TierService / ITierService interface lost during Phase 2 sidebar rewrite | Can't wire up IsLocked | First implementation step: git archaeology (`git log --all -- "*Tier*"`); recreate from historical code if gone; fallback: define minimal Free/Pro/Admin enum + simple lookup table |
| R9 | Missing module Views fail to compile or load on their target platform | Runtime error when navigating | Platform filter in SidebarViewModel prevents click; DI registration check during module enumeration; existing Views verified compiling cross-platform during Phase 2 |
| R10 | Chat opt-in Step 2 cancel leaves inconsistent state | User confusion | State machine explicit: `Acknowledged=true + Enabled=false` is valid intermediate state; next attempt skips Step 1 |
| R11 | Model file corruption not detected (no checksum in Phase 3) | Chat fails to load model, confusing error | Catch load error, show "Model failed to load. [Re-download] or [Choose different model]"; Phase 4 adds proper checksum validation |
| R12 | CortexAmbientService subscription leaks if DashboardViewModel / StatusBarViewModel not disposed | Event handler memory leak | Use weak event pattern or explicit Dispose(); unit tests simulate long-running sessions |
| R13 | User downloads multiple heavy models filling disk | Low disk space, Windows warnings | Phase 4 disk usage UI; Phase 3 workaround: pre-download disk check (show required space vs available) |

---

## 14. Success Criteria

Phase 3 is complete when:

- [ ] Visual verify bccd8ed Quick Actions stretch fix passes (first step before any coding)
- [ ] `AIFeaturesView.axaml[.cs]` + `AIFeaturesViewModel` exist; render hero + 2×2 grid
- [ ] 4 section files (InsightsSection / RecommendationsSection / ScheduleSection / ChatSection) moved to `Views/Pages/AI/` and refactored using Phase 1 primitives
- [ ] 4 ViewModels renamed, logic and tests preserved
- [ ] Hybrid drill-in works: grid → sidebar + detail → back to grid; state preserved across navigation
- [ ] Master toggle per section; state persists via IAppSettings
- [ ] Dashboard ripple verified: Cortex Insights card, Smart Optimize CTA, header chip, status bar all reflect toggle states
- [ ] Smart Optimize placeholder dialog removed; CTA navigates directly to AIFeaturesView > Recommendations
- [ ] `ChatOptInDialog` + `ModelManagerDialog` functional (2-step Chat opt-in)
- [ ] R2 download service works end-to-end (at least Phi-3 Mini Q4KM successfully downloaded during manual test)
- [ ] Chat header model switcher dropdown works (instant switch between installed models)
- [ ] Main sidebar: single "✦ AI Features [CORTEX]" link, no accordion sub-items for AI
- [ ] `SidebarNavItem.IsLocked` DP implemented; tier locking applied to sidebar
- [ ] Missing modules added: `system-health`, `admin-panel`, 4 Linux-only, 4 macOS-only, with correct categorization + platform filter
- [ ] `CortexAmbientService` aggregates state correctly; observed by DashboardViewModel + StatusBarViewModel + AIFeaturesViewModel
- [ ] EN + TR localization complete for all new keys (§9)
- [ ] ~343 tests passing, zero regressions from Phase 2 baseline (283)
- [ ] All manual visual checks (§11.5) pass
- [ ] Branch `phase-3-ai-features` created from `phase-2-sidebar-dashboard`, ~12-15 atomic commits each building green
- [ ] Milestone commit at end: `milestone: Phase 3 AI Features Consolidation complete`

---

## 15. Implementation Order (High-Level)

Writing-plans skill will decompose these into granular step-by-step TDD tasks. This is the sequencing intent.

**Step 0 — Pre-flight:**
1. **Visual verify `bccd8ed`** (Quick Actions stretch fix) — launch app, confirm 2×2 grid evenly stretched. If regression found, fix before proceeding.
2. Create branch `phase-3-ai-features` from `phase-2-sidebar-dashboard`.
3. Run full test suite (`dotnet test`) → 283 green baseline confirmed.

**Step 1 — Infrastructure & foundations** (no UI yet):
4. Add new icons to `Themes/Icons.axaml` (IconSparklesFilled, IconLightbulb, IconCalendarClock, IconMessageSquare, IconDownload, IconWarningTriangleFilled, IconLock).
5. Extend `IAppSettings` with 7 new properties (§5.1) + persistence.
6. Create `Services/AI/` contracts + implementations:
   - `IModelCatalog` + `ModelCatalog` (+ tests).
   - `IInstalledModelStore` + `InstalledModelStore` (+ tests).
   - `IModelDownloadService` + `ModelDownloadService` (+ tests with mocked HttpClient).
   - `ICortexAmbientService` + `CortexAmbientService` (+ tests).
7. Recreate or locate `ITierService` + `TierService` (git archaeology per R8).
8. Register all new services in DI (§4.9).
9. Run tests → all new service tests green; existing 283 still green.

**Step 2 — AIFeaturesView shell** (skeleton UI):
10. Create `AIFeaturesViewModel` with Overview/Detail mode logic, navigation commands, AIFeatureCardVMs (+ unit tests).
11. Create `AIFeaturesView.axaml` with CORTEX Hero + 2×2 grid (no section content yet — placeholder cards).
12. Route sidebar "AI Features" link to `AIFeaturesView` (temporarily broken sub-items OK).
13. View tests: hero renders, grid shows 4 cards, responsive narrow mode stacks cards.

**Step 3 — Section refactor pilot** (Schedule — simplest):
14. Create `Views/Pages/AI/ScheduleSection.axaml[.cs]` with section template (§4.5).
15. Rename `SchedulerViewModel` → `ScheduleSectionViewModel`, update DI registration in one atomic commit.
16. Wire ScheduleSection into `AIFeaturesViewModel.ActiveSectionView` dictionary.
17. Detail mode drill-in works end-to-end for Schedule section.
18. Tests: drill-in navigates, Overview back works, Schedule content renders.

**Step 4 — Remaining sections** (parallel-able):
19. InsightsSection refactor (includes Recent Activity migration from AIInsightsView body).
20. RecommendationsSection refactor.
21. ChatSection refactor (with warning banner, model chip placeholder — switcher UI comes later).
22. After each: rename ViewModel, update DI, build green, tests pass.
23. Delete old standalone view files (`AIInsightsView.axaml`, etc.).

**Step 5 — Model management layer** (prereq for chat to actually work):
24. Create `ChatOptInDialog.axaml[.cs]` + `ChatOptInDialogViewModel` (Step 1 UI).
25. Create `ModelManagerDialog.axaml[.cs]` + `ModelManagerDialogViewModel` (both OptIn + Manage modes).
26. Wire Step 2 mounting inside ChatOptInDialog.
27. Integration test: chat opt-in end-to-end with mocked download.
28. Wire Chat header model chip + dropdown → "Download more..." opens Manage mode.

**Step 6 — Ripple wire-up** (Dashboard + StatusBar):
29. DashboardViewModel subscribes to `ICortexAmbientService`, controls Cortex Insights card, header chip, subtitle, Smart Optimize CTA per §5.3.
30. Delete `SmartOptimizePlaceholderDialog.axaml[.cs]`; wire CTA to `AIFeaturesView > Recommendations`.
31. StatusBarViewModel subscribes to ambient service; controls status bar message.
32. Integration tests for ripple behaviors.

**Step 7 — Sidebar cleanup + tier locking + missing modules** (one focused commit scope):
33. Add `IsLocked` DP to `SidebarNavItem` + XAML visual states.
34. `SidebarViewModel` removes AI accordion sub-items (single "AI Features" link) + wires IsLocked via ITierService.
35. Add missing modules (10 entries per §8.3) with platform filters.
36. `ShowTierUpgradeDialog` placeholder implemented.
37. Integration tests cover sidebar changes, tier locking, platform filtering.

**Step 8 — Localization**:
38. Add all EN + TR keys from §9 to LocalizationService.
39. Smoke test in both locales (switch locale runtime, confirm UI translated).

**Step 9 — Pre-merge verification**:
40. Full test suite: ~343 green, zero regressions.
41. Manual visual checks (§11.5 all 15 steps) on Windows.
42. Cross-platform smoke: Linux VM (systemd-manager visible), macOS VM (brew-manager visible) — if VMs available, otherwise document as known gap.
43. Build Release configuration: no warnings, no missing DI registrations.
44. Milestone commit: `milestone: Phase 3 AI Features Consolidation complete`.

**Expected commit count:** 15-20 atomic commits (each builds + passes tests). Implementation plan will refine.

---

## 16. Phase Transitions

### Entering Phase 3 from Phase 2

- Git: branch off `phase-2-sidebar-dashboard` → `phase-3-ai-features`
- Phase 1 primitives + Phase 2 files remain untouched (except `SidebarViewModel`, `SidebarNavItem` which receive `IsLocked` + missing modules)
- Old `AuraCoreTheme.axaml` bridge stays (Phase 4 will remove)
- **First implementation step:** manually verify Phase 2 hotfix `bccd8ed` (Quick Actions stretch) before any new code

### Exiting Phase 3 into Phase 4

- Phase 4 = Module Pages Refactor (Vision Doc §10 Phase 4)
- Entry criteria: All §14 success criteria met + user retro approval
- Phase 4 migrates ~26 module pages off V1 theme bridge; after complete, bridge can be fully removed
- Settings > Models page (deferred from Phase 3) is a candidate for Phase 4 or a dedicated small sprint between 3 and 4

---

## 17. Decisions Log

| # | Decision | Source | Rationale |
|---|-|-|-|
| Q1 | Chat kept in scope as [Experimental] with warning banner | Brainstorm 2026-04-15 | Vision doc says include; pivot says deprecate. Resolution: keep but opt-in + warn |
| Q2 | Layout: Dashboard-style 2×2 grid overview | Brainstorm | Matches Phase 2 Dashboard language; best visual consistency |
| Q3 | Drill-in: hybrid (grid → sidebar + detail) | Brainstorm | User torn B/C; hybrid combines landing (B) with internal nav (C) |
| Q4 | Existing views: refresh .axaml, preserve ViewModels | Brainstorm | Visual consistency + zero AI logic regression |
| Q5 | Toggle semantics: coupled (single toggle controls run + visibility) | Brainstorm | Simplicity + resource sensitivity + experimental safety for Chat |
| Q5a | Chat default OFF (opt-in); others ON | Brainstorm | Chat experimental; other three provide value by default |
| Q6 | CORTEX Hero: Status-Aware | Brainstorm | Matches Vision §4 ambient CORTEX without overloading |
| Q7 | Model management: split scope (Phase 3 minimum + Phase 4 advanced) | Brainstorm | Ship usable Chat without boiling the ocean |
| Q7a | R2 access via custom domain `models.auracore.pro` | Brainstorm | Simplest secure public-read; no auth; easy migration path if premium tiers emerge |
| Q7b | No pre-selected model; Phi-3 Mini Q4KM has RECOMMENDED badge | User feedback | User choice required, guidance not coercion |
| Q7c | RAM estimate column shown for every tier | User feedback | Transparent resource disclosure |
| Path A | Tier locking + missing modules bundled into Phase 3 | Brainstorm close | Shared file scope (SidebarViewModel); avoid parallel Phase 2.5 sprint |
| Infra | R2 + Cloudflare production-hardened 2026-04-15 | Side-quest | Custom domain + cache rule + HSTS + TLS 1.2 + Bot Fight Mode; files > 512 MB get DYNAMIC (free plan limit, acceptable) |
| Storage | `%LOCALAPPDATA%/AuraCorePro/models/` cross-platform | Brainstorm | Standard OS convention, user writable, persistent |
| UA | Custom User-Agent `AuraCorePro/1.0 (+https://auracore.pro)` for downloads | Brainstorm infra | Cloudflare Bot Fight Mode bypass |
| Card order | Overview grid: Insights, Recs top row; Schedule, Chat bottom row | Implicit | Most-used first; Chat experimental to bottom-right |
| Sidebar cleanup | Accordion removed, single "AI Features" link | Vision §10 | Consolidated per vision doc |
| Smart Optimize | CTA → AIFeaturesView > Recommendations; Phase 2 placeholder dialog removed | Derived §2.5 | Phase 3 replaces Phase 2 placeholder behavior |

---

## Appendix A: Brainstorming Visual Reference

Mockups created during brainstorming at `.superpowers/brainstorm/617-1776221090/content/`:

- `01-layout-options.html` — Layout choices (Top Tabs / Dashboard Grid / Sub-Nav)
- `02-drillin-options.html` — Drill-in mechanics (Single Scroll / Replace+Back / Hybrid)
- `04-hero-options.html` — CORTEX Hero variants (Minimal / Status-Aware / Data-Rich)
- `05-decisions-summary.html` — Interim decisions summary
- `07-model-manager-v2.html` — Final ModelManagerDialog design (with RAM column, no pre-select)

These artifacts are non-normative. The spec above is authoritative. Mockups preserved in `.superpowers/brainstorm/` for implementation reference.

---

## Appendix B: Infrastructure Setup Reference

Cloudflare R2 + custom domain setup completed 2026-04-15:

1. R2 bucket `auracore-models` (ENAM region, account `d4bd9fd5051b4077aa885a006c4db219`) — 8 .gguf models uploaded
2. Custom domain `models.auracore.pro` connected — SSL Active, DNS managed by Cloudflare
3. Cache Rule "Cache AuraCore Models" — Hostname equals `models.auracore.pro`, Edge TTL 1 year (Ignore cache-control header), Browser TTL 1 year (Override origin)
4. HSTS enabled:
   - Cloudflare: max-age 6 months, includeSubDomains, preload=false
   - Origin Nginx also sends HSTS: max-age 1 year, includeSubDomains, preload directive (not submitted to hstspreload.org — safe)
5. Min TLS 1.2 enforced; TLS 1.3 enabled
6. Bot Fight Mode enabled (app bypasses via custom User-Agent)
7. Continuous Script Monitoring enabled (bonus, relevant for auracore.pro / admin.auracore.pro, no impact on R2 subdomain)

Known behavior:
- Files > 512 MB return `cf-cache-status: DYNAMIC` on Cloudflare Free plan. Expected. R2 egress is free regardless; Cloudflare still in path handling TLS/HTTP2/HTTP3.
- All 8 model files exceed this limit. Edge caching effectively disabled in practice but zero cost penalty.

Download URL format for Phase 3:

```
https://models.auracore.pro/auracore-{id}.gguf
```

Where `{id}` matches `ModelDescriptor.Filename` (full filename including `auracore-` prefix).
