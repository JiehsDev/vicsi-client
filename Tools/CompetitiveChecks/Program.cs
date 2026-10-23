using System.Text.Json;

int checks = 0;
void Check(string name, bool ok) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); checks++; }
CompetitiveRunRecord Run(string player, float score, double seconds, string rules = "1.7.0") => new() {
    runId = Guid.NewGuid().ToString(), playerId = player, username = "Detective", score = score, seconds = seconds,
    completedUtc = DateTime.UtcNow.ToString("O"), rulesVersion = rules, recorderPoints = 18 };
var history = new CompetitiveRunHistory();
string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), c = Guid.NewGuid().ToString();
Check("plain display names accepted", CompetitiveRanking.ValidUsername("Detective One") && CompetitiveRanking.ValidUsername("A_1-2"));
Check("empty, short, markup and oversized names rejected", new[] { "", "A", "<b>name</b>", new string('A',21), "bad\nname" }.All(n => !CompetitiveRanking.ValidUsername(n)));
var slow = Run(a, 90, 900); var fast = Run(a, 90, 600); var low = Run(a, 80, 1); var other = Run(b, 90, 700); var top = Run(c, 100, 10000);
foreach (var run in new[] {slow, fast, low, other, top}) Check("completed run retained", CompetitiveRanking.Add(history, run));
Check("duplicate completion is idempotent", !CompetitiveRanking.Add(history, fast) && history.runs.Count == 5);
var board = CompetitiveRanking.Leaderboard(history, "1.7.0");
Check("accuracy outranks speed", board[0].runId == top.runId);
Check("time breaks equal scores", board[1].runId == fast.runId && board[2].runId == other.runId);
Check("best run per profile, all attempts in history", board.Count == 3 && history.runs.Count == 5);
CompetitiveRanking.Add(history, Run(a, 100, 1, "other-case-version"));
Check("different rules never share rankings", CompetitiveRanking.Leaderboard(history, "1.7.0")[0].runId == top.runId);
var bad = Run(a, float.NaN, 10); Check("nonfinite scores rejected", !CompetitiveRanking.Add(history,bad));
bad = Run(a, 50, double.PositiveInfinity); Check("nonfinite times rejected", !CompetitiveRanking.Add(history,bad));
bad = Run(a, 101, 3); Check("out of range scores rejected", !CompetitiveRanking.Add(history,bad));
bad = Run(a, 50, -1); Check("negative times rejected", !CompetitiveRanking.Add(history,bad));
Check("hour-long runs format without truncation", CompetitiveRanking.FormatTime(3661) == "01:01:01");
var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
var restored = JsonSerializer.Deserialize<CompetitiveRunHistory>(JsonSerializer.Serialize(history,jsonOptions),jsonOptions)!;
Check("all run fields survive persistence", restored.runs.Count == history.runs.Count && restored.runs[1].runId == fast.runId && restored.runs[1].recorderPoints == 18);
Check("rankings survive persistence", CompetitiveRanking.Leaderboard(restored,"1.7.0")[1].runId == fast.runId);
int savedCount = history.runs.Count;
var demoBoard = CompetitiveRanking.Leaderboard(history, "1.7.0", true);
Check("demo opponents are valid and visibly tagged", demoBoard.Count(r => r.isDemo) == 6 && demoBoard.All(CompetitiveRanking.ValidRun));
Check("demo opponents do not enter saved history", history.runs.Count == savedCount && history.runs.All(r => !r.isDemo));
Check("demo entries cannot be saved as played runs", !CompetitiveRanking.Add(history, CompetitiveRanking.DemoRuns("1.7.0").First()));
Check("demo entries can be disabled without losing real scores", CompetitiveRanking.Leaderboard(history, "1.7.0").Count == 3);
Check("demo profiles are stable across refreshes", CompetitiveRanking.DemoRuns("1.7.0").Select(r => r.playerId).SequenceEqual(CompetitiveRanking.DemoRuns("1.7.0").Select(r => r.playerId)));
Check("demo opponents use the current rules", CompetitiveRanking.Leaderboard(new CompetitiveRunHistory(), "new-rules", true).All(r => r.rulesVersion == "new-rules"));
Check("real scores outrank demo scores by the same rules", demoBoard[0].runId == top.runId && demoBoard[1].isDemo && demoBoard[1].score == 96);
Console.WriteLine($"{checks} checks passed.");
