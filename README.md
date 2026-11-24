
# Recommendation match score

The recommendation API returns a deterministic 0–100 match percentage. It scores only preferences extracted from the query, then divides the weighted score by the total weight of the preferences that were present:

| Preference | Weight | Score |
|---|---:|---|
| Mood | 40 | 100 when the game has that mood, otherwise 0 |
| Budget | 25 | `round(100 × (budget − price) / budget)` to the nearest integer, clamped to 0–100; for a zero budget, free games score 100 and others 0 |
| Player count | 15 | 100 when the game supports the requested number, otherwise 0 |
| Experience | 20 | `max(0, 100 − 50 × abs(game difficulty − target difficulty))`; beginner, intermediate, and experienced map to difficulty 1, 2, and 3 |

The budget and player-count filters are hard constraints; mood and experience are scored preferences. If no scorable preference was extracted, the match score is a neutral 50. Results are ordered by score descending, with title and ID as stable tie-breakers. `backend/Gamify.Api.Tests/RecommendationScoringTests.cs` verifies the weighted formula and its neutral and mismatch cases.

Users can save a generated playlist with a trimmed, required name of up to 100 characters. Saved playlists are private to their owner and keep a snapshot of the game details and match explanations. The authenticated `/api/saved-playlists` endpoints list, create, open, rename, and delete playlists; the profile page provides the corresponding actions.
