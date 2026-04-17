# Cleaner Consolidation Decision — JunkCleaner + DiskCleanup

**Date:** 2026-04-17  
**Status:** Keep separate, with clearer demarcation.

## Context

User surfaced during Phase 4.2 QA that two cleaner modules feel overlapping:
"iki ayrı cleaner boş yer kaplıyor." (Two separate cleaners are wasting space.)

## Overlap analysis

| Scan target                       | JunkCleaner | DiskCleanup Pro |
|-----------------------------------|-------------|-----------------|
| User temp                         | Yes         | Yes             |
| Browser caches (Chrome/Edge/FF)   | Yes         | —               |
| OS trash / Recycle Bin            | Yes         | —               |
| Prefetch                          | —           | (legacy/unused) |
| Windows Update cache              | —           | Yes             |
| Delivery Optimization             | —           | Yes             |
| DirectX shader cache              | —           | Yes             |
| Error reports / memory dumps      | —           | Yes             |
| Empty user folders                | —           | Yes             |
| Duplicate files (100KB-500MB)     | —           | Yes             |
| Cross-platform (Linux/macOS)      | Yes         | —               |

## Recommendation

**Keep the two modules separate. Clarify purpose via subtitles + sidebar ordering.**

Merging would either:

- Collapse to the lowest common denominator (drop the Windows-only deep scanners), or
- Pollute JunkCleaner's cross-platform scope with Windows-specific cache paths.

Neither serves users well. The right move is:

- **JunkCleaner** subtitle: "Cross-platform quick cleanup — temp + browser caches + trash"
- **DiskCleanup Pro** subtitle: "Windows deep clean — system caches + duplicates + empty folders"
- Sidebar ordering: JunkCleaner first (lighter / broader platform coverage), DiskCleanup second (heavier / Windows-only).

## Out of scope for this phase

No engine code changes follow from this decision. Phase 5.5.3 only updates subtitles and localization keys.
