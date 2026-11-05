# Implementation Plan: Dual Leaderboard

## Overview

Replace the single-table leaderboard with two stacked leaderboards (Active Players, All Players)
served by one `GET /leaderboard` response. The backend refactors `LeaderboardRepository` to compute
both lists from a shared per-game-net aggregation, applies a 12-month window filter to the Active
list, enforces deterministic total ordering with explicit tie-breaking, and validates `sortBy` /
`minGames` in the endpoint. The frontend renders two tables with one shared sort state and
independent per-list empty states, plus new i18n headings and empty-state strings.

Tasks are ordered so each step builds on the previous: the DTO comes first, then the repository
refactor, then the endpoint, then tests, then the frontend types, shared table component, page
layout, i18n, and frontend tests. A final verification task builds and tests both sides.

Implementation language: C# (backend, .NET / xUnit + FsCheck.Xunit) and TypeScript/React
(frontend), matching the existing codebase. The design specifies concrete code, so no language
selection is required.

## Tasks

- [x] 1. Add the `LeaderboardsResponse` DTO
  - Create `poker_web_api/Data/LeaderboardsResponse.cs` with
    `public record LeaderboardsResponse(IReadOnlyList<LeaderboardEntry> Active, IReadOnlyList<LeaderboardEntry> All);`
  - Reuse the existing `LeaderboardEntry` record unchanged for both list element types
  - _Requirements: 5.1_

- [x] 2. Refactor `LeaderboardRepository` to compute both lists
  - [x] 2.1 Add constants and the new `GetLeaderboards` signature
    - Add `public const int DefaultActiveMinGames = 10;` and `public const int DefaultAllMinGames = 5;`
    - Replace `GetLeaderboard(minGames, sortBy)` with
      `Task<LeaderboardsResponse> GetLeaderboards(int allMinGames, string sortBy, DateOnly? today = null)`
    - Default `today` to `DateOnly.FromDateTime(DateTime.UtcNow)` evaluated once per call
    - _Requirements: 1.5, 3.1, 5.1_

  - [x] 2.2 Factor out `LoadPlayerGameNets` shared aggregation
    - Group `GameEntries` by `{ PlayerId, GameId }`, compute `Net = Σ CashOut − Σ BuyIn` with
      missing sides contributing zero, join `Game.Date` and player `Name`
    - Return an in-memory list of `(PlayerId, Name, GameId, Date, Net)` rows loaded once so the
      window list needs no second DB round-trip
    - _Requirements: 2.2_

  - [x] 2.3 Factor out `BuildEntries(rows, minGames)`
    - Group rows by player, keep players with `distinct game count >= minGames`, compute
      `Games` (distinct game count), `Result = Σ Net`, `Rate = Σ Net / Games`
    - Rely on the aggregation collapsing rebuys to one `(PlayerId, GameId)` row so row-count per
      player equals distinct game count
    - _Requirements: 2.1, 2.3, 2.4, 3.2, 3.5, 3.6_

  - [x] 2.4 Factor out `OrderEntries(entries, sortBy)` deterministic ordering
    - `net`: `Result` desc, then `Rate` desc, then `Name` ascending using `StringComparer.Ordinal`
    - `rate` (default and fallback for any non-`net` value): `Rate` desc, then `Result` desc, then
      `Name` ascending using `StringComparer.Ordinal`
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 2.5 Assemble Active and All lists in `GetLeaderboards`
    - Compute `windowStart = today.AddMonths(-12)` (relies on `DateOnly.AddMonths` last-day-of-month
      clamping) and filter rows to `windowStart <= Date <= today` inclusive for the Active list
    - Active list: `OrderEntries(BuildEntries(windowRows, DefaultActiveMinGames), sortBy)` — request
      `minGames` is NOT applied to Active
    - All list: `OrderEntries(BuildEntries(allRows, allMinGames), sortBy)`
    - Return `new LeaderboardsResponse(active, all)`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 3.1, 3.3, 3.4, 5.5_

- [x] 3. Update the `GetLeaderboard` endpoint
  - In `poker_web_api/Endpoints/Public/GetLeaderboard.cs`, default `sortBy` to `"rate"` and return
    `400 Bad Request` with an error body (no list data) when a provided value is not `net` or `rate`
  - Default `minGames` to `LeaderboardRepository.DefaultAllMinGames` (5); return `400` with an error
    body when the bound integer is `< 1`; rely on `int?` model binding to 400 non-integer values
  - Call `repo.GetLeaderboards(allMinGames, sort)` and return `Results.Ok(result)` carrying both lists
  - _Requirements: 4.5, 5.1, 5.2, 5.3, 5.4, 5.6, 5.7_

- [x] 4. Adapt existing repository unit tests to the new return shape
  - Update `poker_web_api.Tests/Unit/LeaderboardRepositoryTests.cs` cases to call `GetLeaderboards`
    and read `response.All` where they previously read the flat list (e.g. `EmptyDatabase_ReturnsEmptyList`,
    `CountsAsOneGame`, `OnlyBuyInsNoCashOut`, `InvalidSortBy_DefaultsToRate`)
  - _Requirements: 2.2, 3.1, 3.2, 4.4_

- [x] 5. Add example and edge-case repository unit tests
  - Active default threshold boundary: a player with 9 in-window games is excluded, exactly 10 is
    included
  - All default threshold applied when `allMinGames = 5`
  - Last-day-of-month window fallback: `today = 2024-02-29` yields window start `2023-02-28`
  - A player present in the All list but absent from the Active list, and the reverse, confirming
    the lists are computed independently
  - _Requirements: 1.5, 5.5, 5.6, 1.1_

- [x] 6. Add property-based repository tests (FsCheck.Xunit)
  - Create `poker_web_api.Tests/Unit/LeaderboardRepositoryPropertyTests.cs` using FsCheck with
    FsCheck.Xunit `[Property]`, reusing `TestDbHelper` and passing a fixed `today`
  - Each property runs a minimum of 100 iterations (raise `MaxTest` if needed) and is tagged with a
    comment in the format `// Feature: dual-leaderboard, Property {number}: {property_text}`
  - [x] 6.1 Property 1: window scope inclusive on both endpoints
    - **Property 1: Window scope is inclusive on both endpoints**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 1: Window scope is inclusive on both endpoints`
    - **Validates: Requirements 1.1, 1.2**
  - [x] 6.2 Property 2: Active membership follows the game-count threshold
    - **Property 2: Active membership follows the game-count threshold**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 2: Active membership follows the game-count threshold`
    - **Validates: Requirements 1.3, 1.4**
  - [x] 6.3 Property 3: per-game net equals cash-outs minus buy-ins
    - **Property 3: Per-game net equals cash-outs minus buy-ins**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 3: Per-game net equals cash-outs minus buy-ins`
    - **Validates: Requirements 2.2**
  - [x] 6.4 Property 4: Result and Rate are a consistent aggregation of in-scope games
    - **Property 4: Result and Rate are a consistent aggregation of in-scope games**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 4: Result and Rate are a consistent aggregation of in-scope games`
    - **Validates: Requirements 2.3, 2.4, 3.5, 3.6**
  - [x] 6.5 Property 5: Active stats ignore out-of-window games
    - **Property 5: Active stats ignore out-of-window games**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 5: Active stats ignore out-of-window games`
    - **Validates: Requirements 2.1**
  - [x] 6.6 Property 6: All membership follows the all-time game-count threshold
    - **Property 6: All membership follows the all-time game-count threshold**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 6: All membership follows the all-time game-count threshold`
    - **Validates: Requirements 3.1, 3.3, 3.4**
  - [x] 6.7 Property 7: repeated entries in one game count as a single game
    - **Property 7: Repeated entries in one game count as a single game**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 7: Repeated entries in one game count as a single game`
    - **Validates: Requirements 3.2**
  - [x] 6.8 Property 8: each list is a deterministic total ordering
    - **Property 8: Each list is a deterministic total ordering**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 8: Each list is a deterministic total ordering`
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
  - [x] 6.9 Property 9: the minGames parameter affects only the All list
    - **Property 9: The minGames parameter affects only the All list**
    - FsCheck.Xunit `[Property]`, minimum 100 iterations
    - `// Feature: dual-leaderboard, Property 9: The minGames parameter affects only the All list`
    - **Validates: Requirements 5.5**

- [x] 7. Add endpoint tests for `GetLeaderboard`
  - [x] 7.1 Response shape and row fields
    - Response contains both `active` and `all` arrays, each row carrying `name`, `games`,
      `result`, `rate`
    - _Requirements: 5.1, 5.4_
  - [x] 7.2 Defaults
    - No `sortBy` orders by rate; no `minGames` applies All threshold 5
    - _Requirements: 4.5, 5.6_
  - [x] 7.3 Invalid-parameter rejection
    - `sortBy=bogus` returns `400` with no lists; `minGames=0`, `minGames=-3`, `minGames=abc` each
      return `400` with no lists
    - _Requirements: 5.3, 5.7_

- [x] 8. Checkpoint - backend complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Update frontend leaderboard types and fetch
  - In `poker_frontend/src/pages/Leaderboard.tsx`, update `LeaderboardEntry` to
    `{ name: string; games: number; result: number; rate: number }` (drop the unused
    `positionDelta`) and add `interface LeaderboardsResponse { active: LeaderboardEntry[]; all: LeaderboardEntry[] }`
  - Change the fetch to `apiFetch<LeaderboardsResponse>(\`/leaderboard?sortBy=${sortBy}&minGames=5\`)`
    and store both arrays from the response
  - _Requirements: 5.1, 5.4_

- [x] 10. Extract a shared `LeaderboardTable` sub-component
  - In `Leaderboard.tsx`, create a `LeaderboardTable` component that renders its heading, the table
    with columns rank, name, Games, Result, Rate, a per-list 1-based rank (`i + 1` scoped to the
    list), and an empty-state message when `entries.length === 0`
  - Reuse the existing per-row rendering unchanged (Link to player, `CrownName`, colored net/rate,
    `toFixed`)
  - _Requirements: 4.6, 6.4, 7.1, 7.2_

- [x] 11. Render both tables with shared sort and independent empty states
  - Keep one `sortBy` state (`'net' | 'rate'`) defaulting to `'rate'`; render `LeaderboardTable`
    for Active above All, passing the shared sort state
  - Place the single shared sort controls (exactly `net` and `rate`) above both tables; toggling
    updates `sortBy` for both
  - Evaluate each table's empty state independently from its own `entries.length`, keeping the
    heading and shared controls visible while a list is empty
  - _Requirements: 6.1, 6.5, 6.6, 7.3, 7.4, 7.5_

- [x] 12. Add i18n keys
  - In `poker_frontend/src/i18n/translations.ts`, add `leaderboard.active`, `leaderboard.all`,
    `leaderboard.empty.active`, `leaderboard.empty.all` for both `en` and `ru`
  - en: "Active Players", "All Players", "No active players yet", "No players yet"
  - ru: "Активные игроки", "Все игроки", "Пока нет активных игроков", "Пока нет игроков"
  - _Requirements: 6.2, 6.3, 7.1, 7.2_

- [x] 13. Add frontend component tests for `Leaderboard.tsx`
  - [x] 13.1 Layout, columns, and ranks
    - Active table renders above All; columns in order rank, name, Games, Result, Rate; ranks are
      `1..n` per list
    - _Requirements: 6.1, 6.4, 4.6_
  - [x] 13.2 Localized headings
    - English and Russian headings resolve for `leaderboard.active` / `leaderboard.all`
    - _Requirements: 6.2, 6.3_
  - [x] 13.3 Shared sort behavior
    - Controls default to rate on load and re-sort both tables when toggled
    - _Requirements: 6.5, 6.6_
  - [x] 13.4 Independent empty states
    - Active empty + All non-empty shows the active empty-state and the all rows, and the reverse;
      heading and controls stay visible while empty
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 14. Final verification - build and test both sides
  - Backend: build `poker_web_api` and run `poker_web_api.Tests` (repository, property, endpoint tests)
  - Frontend: run the frontend build/typecheck and the component test suite
  - Fix any failures, clean up temporary files, ask the user if questions arise
  - _Requirements: 5.1, 5.3, 5.7, 6.1, 7.1, 7.2_

## Notes

- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core
  implementation sub-tasks are never marked optional.
- Each task references specific requirement sub-clauses for traceability.
- Checkpoints ensure incremental validation between the backend and frontend halves.
- Property-based tests (Task 6) use FsCheck.Xunit `[Property]` with a minimum of 100 iterations and
  the `// Feature: dual-leaderboard, Property N: ...` tagging comment, one test per correctness
  property. Unit, endpoint, and component tests cover the example, edge, validation, layout, and
  i18n behaviors the properties do not pin down.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "2.4"] },
    { "id": 3, "tasks": ["2.5"] },
    { "id": 4, "tasks": ["3"] },
    { "id": 5, "tasks": ["4", "5", "6.1", "6.2", "6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9", "7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["9"] },
    { "id": 7, "tasks": ["10", "12"] },
    { "id": 8, "tasks": ["11"] },
    { "id": 9, "tasks": ["13.1", "13.2", "13.3", "13.4"] }
  ]
}
```
