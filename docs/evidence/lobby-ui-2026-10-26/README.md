# Lobby UI refresh

Research and Competitive now share a lobby palette and visual hierarchy: an opaque navy panel, readable light text, a single gold entry action, aligned secondary options, and a short Collect / Connect / Conclude guide. The existing fixed menu placement and chair interaction remain available. Research keeps student identification, instructor management and device status. Competitive keeps usernames, rankings, run history, settings and the demo toggle.

References consulted:

- [Halo Infinite menu guide](https://support.halowaypoint.com/hc/en-us/articles/4408368163348-Guide-to-Halo-Infinite-Menus): game menu organization and distinct entry actions; used as a structural reference, without copying artwork.
- [Xbox Accessibility Guideline 101: Text display](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101): readable type sizes and spacing.
- [Xbox Accessibility Guideline 102: Contrast](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102): separation of text, controls and background.

The lobby panel is 1000 x 900 canvas units at 0.001 world scale. Body text is 26-28 units; the smallest new home-screen labels are 24. The primary action uses dark text on gold. Focus on secondary controls uses a blue fill and a visible gold edge instead of creating a second gold primary action. The research student-code flow uses the same palette and preserves its existing event bindings.

Competitive username entry has a dedicated input display, a complete keyboard, clear editing actions, and separate validation help. Rankings use aligned Rank / Player / Score / Time columns. Long names can wrap over two lines at 24 units. Paging and Back controls occupy separate, nonoverlapping targets. The empty-history message explains how to create the first record.

Validation:

- Research scripts compile in Unity 6000.5.6f1. Existing Start and Instructor buttons each retain their event binding.
- Research home and student-code screens show no visible TMP text overflow; the Start button opens the identification flow.
- Competitive runtime sources compile against the existing competitive assembly using Unity references.
- Competitive presentation was exercised in an isolated, temporary assembly in the research editor. Two research-only hide calls used preview shims; the menu and ranking source were otherwise the competitive source. The inactive preview edition used synthetic in-memory data and did not load or save real competitive history.
- Competitive home, username, leaderboard and empty-history screens pass visible-text overflow checks, including a 20-character all-W player name and a full page of demo opponents.
- Pointer selection opens username entry; a keyboard key enters A; leaderboard Next moves to page 2; Back returns home; settings open and return correctly.
- Simulated controller A opens username entry and B returns home. Seating remains available from the main menu, is blocked by settings, and keeps the menu anchor fixed while seated.
- Editor views were visually inspected at standing and seated heights. Screenshots and preview DLLs remain local under Temp, outside committed evidence.

No headset APK was rebuilt or installed. Headset legibility and comfort still need on-device review.
