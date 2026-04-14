# AI Chat "Experimental/Deneysel" Marking — Design Spec

**Date:** 2026-04-14
**Status:** Approved
**Scope:** UI-only changes to mark AI Chat as experimental

---

## Goal

Mark the AI Chat feature as "Experimental/Deneysel" across the UI with a badge in the sidebar, an updated header badge, and a persistent warning banner above the chat area. No functional changes to the chat itself.

---

## Changes

### 1. Sidebar Navigation Label

**File:** `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs` (line 277)

**Current:** `MakeNavButton("ai-chat", "\u2756", LocalizationService._("nav.aiChat"))`
**After:** Append a small amber "[Deneysel]" / "[Experimental]" tag after the nav label text.

**Implementation:** Update the `MakeNavButton` call to include the experimental suffix, OR add a special-case in `MakeNavButton` for ai-chat that appends an amber-colored run. Preferred approach: modify the localization string to include the tag, keeping code changes minimal.

**Localization keys:**
- `nav.aiChat` EN: `"AI Chat [Experimental]"` (was `"AI Chat"`)
- `nav.aiChat` TR: `"AI Sohbet [Deneysel]"` (was `"AI Sohbet"`)

### 2. Page Header Badge

**File:** `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIChatView.axaml` (lines 16-29)

**Current:** Purple badge with `"AI CHAT"` text and purple dot (`#8B5CF6`)
**After:** Amber badge with `"DENEYSEL"` / `"EXPERIMENTAL"` text and amber warning icon

**Color changes in header badge:**
- Background: `#8B5CF6` opacity 0.06 → `#F59E0B` opacity 0.08
- Border: `#8B5CF6` opacity 0.12 → `#F59E0B` opacity 0.15
- Dot: `#8B5CF6` → `#F59E0B`
- Text: `#8B5CF6` → `#F59E0B`
- Badge text: localized via new key `aiChat.experimentalBadge`

**New localization keys:**
- `aiChat.experimentalBadge` EN: `"EXPERIMENTAL"`
- `aiChat.experimentalBadge` TR: `"DENEYSEL"`

### 3. Warning Banner (New Element)

**File:** `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIChatView.axaml`

**Position:** Between the header (Row 0) and chat messages (Row 1). Grid changes from `RowDefinitions="Auto,*,Auto"` to `RowDefinitions="Auto,Auto,*,Auto"`. Chat messages move to Row 2, input to Row 3.

**Banner design:**
- Background: `#F59E0B` at 8% opacity
- Border: `#F59E0B` at 15% opacity, 1px, rounded corners (RadiusMd)
- Left icon: Warning symbol (Unicode `\u26A0` or text "!")
- Text: Localized warning message
- Padding: 10px, Margin: 0,0,0,10
- Non-dismissible (always visible)

**New localization keys:**
- `aiChat.experimentalWarning` EN: `"This feature is experimental. Responses may be inaccurate. For reliable AI-powered analysis, use the AI Insights page."`
- `aiChat.experimentalWarning` TR: `"Bu ozellik deneyseldir. Yanitlar hatali olabilir. Guvenilir AI destekli analiz icin AI Analiz sayfasini kullanin."`

### 4. Code-Behind Update

**File:** `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIChatView.axaml.cs`

- Add `WarningText` and `BadgeText` named element references
- Update `ApplyLocalization()` to set the new localized strings for the badge and warning banner

---

## What Does NOT Change

- Chat functionality (send, receive, history)
- LLM integration (IAuraCoreLLM, IAIAnalyzerEngine)
- Chat bubble colors (purple stays for user messages)
- Input area styling
- Any other page or module
- AI Insights page
- AI Recommendations page

---

## Files Modified (Summary)

| File | Change |
|------|--------|
| `LocalizationService.cs` | Update `nav.aiChat`, add `aiChat.experimentalBadge`, `aiChat.experimentalWarning` (EN+TR) |
| `AIChatView.axaml` | Change badge colors to amber, add warning banner row, update Grid rows |
| `AIChatView.axaml.cs` | Add named element refs, update `ApplyLocalization()` |
| `MainWindow.axaml.cs` | No code change needed (handled via localization string update) |

---

## Color Reference

| Element | Old Color | New Color |
|---------|-----------|-----------|
| Badge background | `#8B5CF6` @ 6% | `#F59E0B` @ 8% |
| Badge border | `#8B5CF6` @ 12% | `#F59E0B` @ 15% |
| Badge dot/icon | `#8B5CF6` | `#F59E0B` |
| Badge text | `#8B5CF6` | `#F59E0B` |
| Warning banner bg | — | `#F59E0B` @ 8% |
| Warning banner border | — | `#F59E0B` @ 15% |
| Warning text | — | `#F59E0B` |
