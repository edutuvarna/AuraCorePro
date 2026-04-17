# Handoff — Phase 5.5 + Phase 5.6 (2026-04-17)

**For next session — read this first.** Phase 5.4 just closed; branch is ready for Phase 5.5 QA feature wave.

## Current state

- **Branch:** `phase-5-polish`
- **HEAD:** `16c1ce1` (Phase 5.4 ceremonial close)
- **Tests:** 2032 passing + 6 skipped + 0 failed across 8 assemblies
- **Baseline for Phase 5.5:** 2032

### Bootstrap (first commands to run)

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git checkout phase-5-polish
git log --oneline -8                      # confirm HEAD 16c1ce1
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!"
# Expected: 8 Passed! lines, no Failed!
```

### Read-before-starting

- `docs/superpowers/specs/2026-04-16-phase5-polish-umbrella-design.md` (§5.5 scope authoritative)
- `C:/Users/Admin/.claude/projects/C--/memory/project_ui_rebuild_phase_5_4_debt_polish_complete.md` (previous-wave context)
- `C:/Users/Admin/.claude/projects/C--/memory/project_feature_ideas_qa_2026_04_16.md` (the 9 QA feature ideas)

---

## Phase 5.5 scope — 9 QA feature ideas

Per umbrella §5.5. Items 6-8 were blocked on Phase 5.2 privilege IPC — **now unblocked** since Phase 5.2 landed (commit `0045573`).

| # | Item | Status | Est. size | Notes |
|---|------|--------|-----------|-------|
| 1 | JunkCleaner + DiskCleanup consolidation decision | Needs user input mid-impl | M | Start with a consolidation design doc; user gates whether to actually merge OR keep separate with clearer demarcation |
| 2 | Bloatware default Windows preset (one-click quick action) | Ready | S | Add a "Remove Windows bloat" preset button to Dashboard Quick Actions; preset is a curated list of bloat packages |
| 3 | Disk Health → Dashboard layout change (remove from Apps & Tools) | Ready | S | Sidebar category move + Dashboard embedded widget. Pure UI/sidebar work |
| 4 | System Health clarity (subtitle + intro card OR repurpose) | Needs design call | M | User may want to fold into Dashboard entirely — ask at brainstorm time |
| 5 | Space Analyzer file-path drill-down (tree expansion) | Ready | M | Existing Space Analyzer shows aggregate sizes only; add per-path tree drill |
| 6 | Driver Updater write capability | **Unblocked by 5.2** | M | Uses `IShellCommandService.RunPrivilegedAsync` with a new `driver-update` action id. Needs Windows privileged-service flesh-out (Phase 5.2 stub returns HelperMissing on Windows) OR in-process elevation via UAC prompt — **architectural decision at brainstorm** |
| 7 | Defender Manager write capability | **Unblocked by 5.2** | M | Same pattern as #6 but for Defender policies. Action id candidates: `defender-policy` / `defender-exclusion` |
| 8 | Service Manager write capability | **Unblocked by 5.2** | M | Same pattern. Action id: `service-control`. Includes start/stop/restart/set-startup-type |
| 9 | Symlink Manager UX polish | Ready | S | Existing module; polish pass (clearer labels, better error messages, maybe drag-drop) |

**Scope clusters (suggested for brainstorm):**
- **Small quick wins (2, 3, 9):** can be batched as a single "UX polish" sub-wave (5.5.1)
- **Privileged writes (6, 7, 8):** architectural sub-wave (5.5.2) — needs Windows IPC decision up front: flesh out `AuraCore.PrivilegedService` Named Pipe server OR use UAC-elevated in-process calls. The umbrella spec originally punted Windows Named Pipe to "Phase 5.5 alongside items 6-8" so this is the natural home
- **Design-heavy (1, 4):** need user input mid-session (5.5.3)
- **Medium (5):** standalone

Suggested order: 5.5.2 privileged-writes first (gates #6-8 which are the biggest user-ask), then 5.5.1 quick wins, then 5.5.3 design calls, then #5.

**Expected test growth:** ~2032 → 2100+ depending on how rich #6-8 become.

---

## Phase 5.6 — Phase 5 ceremonial close

Mechanical:
1. Empty ceremonial commit: `milestone: Phase 5 Polish + Cross-Cutting Infra Wave 3 CLOSED`
2. Update MEMORY.md — demote Phase 5.5 entry to history, insert Phase 5 close marker
3. Write `project_ui_rebuild_phase_5_close.md` memory file
4. Decision point for user: merge `phase-5-polish` → `main`? Or keep open? If merge: follow `superpowers:finishing-a-development-branch` skill

---

## Carry-forward debt (from Phase 5.2–5.4 — do NOT re-litigate in 5.5)

All documented in commits; listed here so the next session can ignore these during brainstorms:

### Phase 5.2 Linux

- **GrubManager 3 deferred sudo hits** (lines 199, 320, 373 in `GrubManagerModule.cs`, `TODO(phase-5.2.1)` markers). Need new validator sub-actions: `backup-etc-grub` + `grub-mkconfig`. Edit Task 16 `src/Service/AuraCore.PrivHelper.Linux/ActionWhitelist.cs` + Task 18 `pro.auracore.privhelper.policy`

### Phase 5.2 macOS

- **TimeMachineManager 2 deferred verbs** (`TODO(phase-5.2.2)` markers at lines 152, 165, 182): `thinlocalsnapshots` belongs to `purgeable` action id (not `time-machine`); `tmutil delete` needs path-strict validator
- **PurgeableSpaceManager `run-periodic` dead-code branch** (`TODO(phase-5.2.2)` comment, `fbf4b74`) — either remove dead branch OR add new `run-periodic` validator sub-action
- **SwapOptimizer migration** — mini-spec §3.8 flagged optional; explicitly deferred from 5.2.2. Would need new `swap` action id with `sysctl vm.*` whitelist
- **Daemon-side ObjC block-handler wiring** (Task 26 follow-up): `xpc_connection_set_event_handler` needs a C/Swift shim `.dylib` — without it, the daemon accepts connections but never replies on real macOS. Client-side (Task 29) sidesteps via `xpc_connection_send_message_with_reply_sync`
- **SMAppService `registerAndReturnError:` NSError out-param robustness** (Task 30 follow-up): may need a Swift shim for reliable error marshalling. Current bridge returns `NotSupported` on any exception (safe fallback)
- **TEAM_ID signing-time substitution** (placeholder `"AURACORE_TEAM_ID_PLACEHOLDER"` in `SecurityConfig.cs`) — ship-prep
- **macOS notarization + lipo universal binary pipeline** — ship-prep

### Phase 5.3 narrow-mode

- **AIFeaturesView icon-only compression at 80 DIP** (`TODO(phase-5.3.2.3)`): current state uses compact text ("Recs"); full icon-glyph substitution needs per-nav-item icon assets OR DataTemplate swap
- **6 pilot-view render tests skipped** (`TODO(phase-5.3.3)`): SystemHealth/BloatwareRemoval/RamOptimizer views require `App.Services` DI root which isn't bootstrapped in headless test harness. Fix: either bootstrap minimal DI in `AvaloniaTestApplication` OR make those views tolerate null `App.Services`

### Phase 5.4 debt polish

- **`ChatSection.axaml.cs` wire-up of `ReloadAsync`** — `IAuraCoreLLM.ReloadAsync` + `IsReloading` contract now exists; UI still has a comment saying "IAuraCoreLLM has no Reload method today" and advises app restart. Replace with actual `_llm.ReloadAsync(newConfig)` call + IsReloading-bound spinner
- **Nginx `auracore-api.bak` stale file in `sites-enabled/`** at origin `165.227.170.3` — causes the non-fatal "conflicting server name" warnings. SSH + `mv /etc/nginx/sites-enabled/auracore-api.bak /etc/nginx/sites-available/archive-20260417-api.bak` to retire it
- **API conf duplicate `add_header Strict-Transport-Security`** (3 locations in `auracore-api` conf cause double-header on responses) — consolidate to single directive. Pre-existing; not a 5.4 regression

### Cross-wave (always-deferred)

- **Pixel-regression testing** — parent spec §6 carry-forward; infrastructure gap
- **AuraCore.Desktop WinUI build error** (dotnet SDK 10.0.201 missing `MrtCore.PriGen.targets`/`AppxPackage` dll) — pre-existing since Phase 5.1 baseline; does not block test execution
- **Real VM validation on Ubuntu 24.04 + Fedora 40 + macOS 14 Sonoma** — parent spec §8.5 carry-forward; manual QA
- **Windows Named Pipe server flesh-out** — was gated to 5.5 (items 6-8). **This is the biggest architectural decision for 5.5 start**

---

## Active behavior preferences

- `feedback_subagent_driven_default.md` — default to subagent-driven execution, don't ask
- `feedback_skip_spec_review.md` — no spec-review user-gate
- `feedback_supervisor_mode.md` — main-session inline verification post-subagent
- `feedback_gpu_cpu_limit.md` — 80-85% max GPU/CPU
- `feedback_notify_before_gpu.md` — ask before GPU-intensive work
- `feedback_visual_companion_always_yes.md` — skip consent prompt if used
- `feedback_afk_default_recommended.md` — **DORMANT** (user not AFK)

---

## Suggested first moves for next session

1. `cd` + `git checkout` + `dotnet test` bootstrap (see top of file)
2. `mcp__ccd_session__mark_chapter` with title `"Phase 5.5 start"` to mark the chapter transition
3. Read this handoff + `project_feature_ideas_qa_2026_04_16.md` (memory)
4. Invoke `superpowers:brainstorming` for Phase 5.5 — locked-in questions to tee up:
   - Q1: Scope — all 9 items in one wave OR split into 2 sub-phases (5.5 privileged-writes + 5.5b polish)?
   - Q2: Windows IPC strategy for items 6-8 — flesh out `AuraCore.PrivilegedService` Named Pipe server OR UAC-elevated in-process calls? (The bigger architectural call.)
   - Q3: Item 1 (JunkCleaner + DiskCleanup consolidation) — design-first pass, or defer until you have more module-usage data?
   - Q4: Item 4 (System Health clarity) — repurpose / rewrite / keep-as-is + clarity additions only?
5. Per answers, decompose into sub-waves + spec + plan + subagent-driven execution

Phase 5.6 can run directly after 5.5 (or at a convenient break) — it's ceremonial.

---

**You're set. Next session should hit the ground sprinting.** 🚀
