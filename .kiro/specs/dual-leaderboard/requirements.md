# Requirements Document

## Introduction

The Leaderboard page currently renders a single table of players ranked by average rate or net
result, filtered by a minimum number of games played. This feature replaces that single table with
two stacked leaderboards on the same page:

1. **Active Players** (top): a qualified leaderboard that lists only players who played at least a
   configured number of games (default 10) within a rolling 12-month window ending today. This
   window + game-count rule is a membership filter only; a qualified player's displayed stats are
   all-time, identical to what they show on the All Players list.
2. **All Players** (bottom): the existing all-time leaderboard that lists every player meeting the
   all-time minimum-games threshold (default 5 games, to filter out one-time guests), with no date
   restriction.

Both leaderboards share the same column layout (rank, name, games, net result, rate) and the same
sort controls (net or rate). The backend supplies the data for both lists, and each list handles
the case where no players qualify. This document specifies the qualification rule, date-window
semantics, ordering, labeling, API shape, sorting, and empty-state handling.

## Glossary

- **Leaderboard_Page**: The frontend page (`poker_frontend/src/pages/Leaderboard.tsx`) that renders
  the leaderboards.
- **Leaderboard_API**: The backend HTTP endpoint that returns leaderboard data
  (`poker_web_api/Endpoints/Public/GetLeaderboard.cs`, route `GET /leaderboard`).
- **Leaderboard_Repository**: The data-access component that computes leaderboard statistics
  (`poker_web_api/Data/LeaderboardRepository.cs`).
- **Active_Leaderboard**: The top leaderboard listing only qualified players, labeled "Active
  Players".
- **All_Leaderboard**: The bottom leaderboard listing all players that meet the existing
  minimum-games threshold, labeled "All Players".
- **Qualification_Window**: The rolling 12-month period ending on the current date, used to decide
  whether a player qualifies for the Active_Leaderboard.
- **Qualified_Player**: A player whose count of games played inside the Qualification_Window is
  greater than or equal to the Active_Min_Games threshold.
- **Active_Min_Games**: The minimum number of games, counted within the Qualification_Window, that a
  player must have to appear on the Active_Leaderboard. Default value is 10.
- **All_Min_Games**: The minimum number of games (all-time, no date restriction) that a player must
  have to appear on the All_Leaderboard. Default value is 5.
- **Player_Net**: For one player in one game, the sum of CashOut amounts minus the sum of BuyIn
  amounts for that player in that game.
- **Games_Count**: The number of distinct games a player participated in. For displayed statistics
  this is always all-time (for both leaderboards). The window-scoped count is used only to decide
  Active_Leaderboard membership.
- **Result**: A player's cumulative Player_Net summed across all of their games, all-time.
- **Rate**: A player's average Player_Net per game across all of their games, all-time.
- **Game_Date**: The calendar date assigned to a game (the `Date` field of the game record,
  a date without a time component).
- **Current_Date**: The calendar date on which the request is processed, as determined by the
  server.
- **Sort_Mode**: The ordering selection for a leaderboard, one of "net" or "rate".

## Requirements

### Requirement 1: Active Players leaderboard qualification

**User Story:** As a player viewing the leaderboard, I want a top list that shows only recently active players, so that I can compare myself against people who are currently playing regularly.

#### Acceptance Criteria

1. THE Leaderboard_Repository SHALL compute the Qualification_Window as the interval beginning on the date 12 months before Current_Date and ending on Current_Date, with both endpoints included, and WHERE the day-of-month 12 months prior does not exist in the target month THE Leaderboard_Repository SHALL use the last day of that target month as the start date.
2. WHEN counting games for qualification, THE Leaderboard_Repository SHALL set a player's Games_Count to the number of distinct games the player participated in whose Game_Date falls on or between the Qualification_Window start and end dates inclusive.
3. IF a player's Games_Count within the Qualification_Window is greater than or equal to Active_Min_Games, THEN THE Leaderboard_Repository SHALL include that player in the Active_Leaderboard.
4. IF a player's Games_Count within the Qualification_Window is less than Active_Min_Games, THEN THE Leaderboard_Repository SHALL exclude that player from the Active_Leaderboard.
5. THE Leaderboard_Repository SHALL set Active_Min_Games to a default value of 10.

### Requirement 2: Active Players leaderboard statistics scope

**User Story:** As a player viewing the Active Players list, I want each qualified player's games, net, and rate to reflect their all-time record, so that the numbers match the player's true standing rather than only a recent slice, with the recent-activity rule used only to decide who appears.

#### Acceptance Criteria

1. THE Leaderboard_Repository SHALL compute Games_Count, Result, and Rate for each Qualified_Player using all of that player's distinct games across all dates, with no restriction to the Qualification_Window; the Qualification_Window governs membership only.
2. THE Leaderboard_Repository SHALL compute Player_Net for a player in a game as the sum of that player's CashOut amounts in that game minus the sum of that player's BuyIn amounts in that game, treating a missing CashOut total or missing BuyIn total as zero.
3. THE Leaderboard_Repository SHALL compute Result for a Qualified_Player as the sum of that player's Player_Net across all of that player's games across all dates.
4. THE Leaderboard_Repository SHALL compute Rate for a Qualified_Player as the sum of that player's Player_Net across all of that player's games across all dates divided by that player's all-time Games_Count.

### Requirement 3: All Players leaderboard

**User Story:** As a player viewing the leaderboard, I want a bottom list that shows every player over all time, so that I can see the complete historical standing.

#### Acceptance Criteria

1. THE Leaderboard_Repository SHALL compute the All_Leaderboard using each player's games across all dates, with no restriction to the Qualification_Window.
2. THE Leaderboard_Repository SHALL compute all-time Games_Count for a player as the number of distinct games that player participated in across all Game_Date values, with no date restriction.
3. THE Leaderboard_Repository SHALL include a player in the All_Leaderboard WHERE that player's all-time Games_Count is greater than or equal to All_Min_Games.
4. THE Leaderboard_Repository SHALL exclude a player from the All_Leaderboard WHERE that player's all-time Games_Count is less than All_Min_Games.
5. THE Leaderboard_Repository SHALL compute Result for a player on the All_Leaderboard as the sum of that player's Player_Net across all of that player's games across all dates.
6. THE Leaderboard_Repository SHALL compute Rate for a player on the All_Leaderboard as the average of that player's Player_Net across all of that player's games across all dates.

### Requirement 4: Ordering of each leaderboard

**User Story:** As a player viewing either list, I want to sort by net or by rate, so that I can rank players by the measure I care about.

#### Acceptance Criteria

1. WHEN Sort_Mode is "net", THE Leaderboard_Repository SHALL order the Active_Leaderboard in descending order of Result, breaking ties by descending Rate and then by ascending player Name, so that the resulting order is deterministic and total.
2. WHEN Sort_Mode is "rate", THE Leaderboard_Repository SHALL order the Active_Leaderboard in descending order of Rate, breaking ties by descending Result and then by ascending player Name, so that the resulting order is deterministic and total.
3. WHEN Sort_Mode is "net", THE Leaderboard_Repository SHALL order the All_Leaderboard in descending order of Result, breaking ties by descending Rate and then by ascending player Name, so that the resulting order is deterministic and total.
4. WHEN Sort_Mode is "rate", THE Leaderboard_Repository SHALL order the All_Leaderboard in descending order of Rate, breaking ties by descending Result and then by ascending player Name, so that the resulting order is deterministic and total.
5. WHEN Sort_Mode is not specified, THE Leaderboard_API SHALL use a default Sort_Mode of "rate".
6. WHEN the Leaderboard_Page displays a leaderboard, THE Leaderboard_Page SHALL assign each row a rank number equal to its 1-based position in the ordered list for that leaderboard.

### Requirement 5: API shape returning both leaderboards

**User Story:** As a frontend developer, I want a single request to return both leaderboards, so that the page can render both lists without issuing multiple requests.

#### Acceptance Criteria

1. WHEN a GET request is made to the Leaderboard_API, THE Leaderboard_API SHALL return a single response containing both the Active_Leaderboard list and the All_Leaderboard list, where each list is an ordered collection of leaderboard rows.
2. THE Leaderboard_API SHALL accept an optional Sort_Mode query parameter named `sortBy` whose value is one of the defined Sort_Mode values ("net" or "rate"), and SHALL apply the supplied Sort_Mode to both the Active_Leaderboard list and the All_Leaderboard list.
3. IF the `sortBy` query parameter is provided with a value that is not one of the defined Sort_Mode values, THEN THE Leaderboard_API SHALL reject the request with an error response indicating the parameter is invalid, and SHALL return neither leaderboard list.
4. THE Leaderboard_API SHALL represent each leaderboard row with the fields Name, Games, Result, and Rate.
5. WHEN the `minGames` query parameter is provided as an integer greater than or equal to 1, THE Leaderboard_API SHALL apply its value as All_Min_Games to the All_Leaderboard only, and SHALL NOT apply it to the Active_Leaderboard.
6. WHEN the `minGames` query parameter is not provided, THE Leaderboard_API SHALL apply a default All_Min_Games value of 5 to the All_Leaderboard.
7. IF the `minGames` query parameter is provided with a value that is not an integer or is less than 1, THEN THE Leaderboard_API SHALL reject the request with an error response indicating the parameter is invalid, and SHALL return neither leaderboard list.

### Requirement 6: Page layout and labeling

**User Story:** As a player, I want the two lists clearly labeled and stacked, so that I can tell the active list from the all-time list.

#### Acceptance Criteria

1. THE Leaderboard_Page SHALL render the Active_Leaderboard above the All_Leaderboard.
2. WHERE the active locale is English, THE Leaderboard_Page SHALL display the heading "Active Players" for the Active_Leaderboard and "All Players" for the All_Leaderboard.
3. WHERE the active locale is Russian, THE Leaderboard_Page SHALL display the Active_Leaderboard and All_Leaderboard headings in Russian.
4. THE Leaderboard_Page SHALL render each leaderboard as a table whose columns, left to right, are rank, name, Games, Result, and Rate.
5. THE Leaderboard_Page SHALL provide a single shared set of Sort_Mode controls offering exactly "net" and "rate", applied to both the Active_Leaderboard and the All_Leaderboard simultaneously.
6. WHEN the Leaderboard_Page first loads, THE Leaderboard_Page SHALL set Sort_Mode to "rate".

### Requirement 7: Empty-state handling

**User Story:** As a player, I want a clear message when a list has no players, so that I understand the list is empty rather than broken.

#### Acceptance Criteria

1. IF the Active_Leaderboard list returned by the Leaderboard_API contains zero rows, THEN THE Leaderboard_Page SHALL display, in place of the Active_Leaderboard rows, an empty-state message that indicates no players qualify, distinct from any loading indicator or error message.
2. IF the All_Leaderboard list returned by the Leaderboard_API contains zero rows, THEN THE Leaderboard_Page SHALL display, in place of the All_Leaderboard rows, an empty-state message that indicates no players qualify, distinct from any loading indicator or error message.
3. WHEN a leaderboard's list returned by the Leaderboard_API contains one or more rows, THE Leaderboard_Page SHALL display that leaderboard's rows and SHALL NOT display an empty-state message for that leaderboard.
4. THE Leaderboard_Page SHALL evaluate and render the empty-state condition for the Active_Leaderboard and the All_Leaderboard independently, such that one leaderboard showing an empty-state message does not change whether the other leaderboard displays its rows.
5. WHILE a leaderboard displays an empty-state message, THE Leaderboard_Page SHALL continue to display that leaderboard's heading and the shared Sort_Mode controls.
