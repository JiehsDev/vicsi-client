using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation of an already saved result. Never changes scoring or progression.</summary>
public sealed class CompetitiveResultsReveal : MonoBehaviour
{
    TMP_Text scoreLabel, rankLabel, timeLabel, detailLabel, boardLabel;
    readonly List<RectTransform> rows = new();
    RectTransform playerRow;
    CanvasGroup fade;
    string revealedRunId;
    CompetitiveRunRecord run;
    int rank, total, playerSlot;
    const float RowTop = -176, RowStep = 44;
    public bool IsRevealing { get; private set; }

    public static CompetitiveResultsReveal Create(ResponsivePanelLayout layout)
    {
        var image = ForensicUIStyle.Rect(layout.Body.Content, "Competitive results reveal", ForensicUIStyle.PanelBackground);
        var element = image.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = 490;
        layout.AddFixedRow(image.rectTransform);
        var view = image.gameObject.AddComponent<CompetitiveResultsReveal>();
        view.fade = image.gameObject.AddComponent<CanvasGroup>();
        view.scoreLabel = view.Label("Score", 52, 0, -28, 650, 60, TextAlignmentOptions.Center);
        view.timeLabel = view.Label("Time", 23, 0, -79, 650, 30, TextAlignmentOptions.Center);
        view.rankLabel = view.Label("Rank", 27, 0, -112, 650, 36, TextAlignmentOptions.Center);
        view.boardLabel = view.Label("Leaderboard heading", 22, 0, -150, 650, 30);
        view.detailLabel = view.Label("Result detail", 21, 0, -438, 650, 80, TextAlignmentOptions.Center);
        view.detailLabel.color = ForensicUIStyle.TextSecondary;
        image.gameObject.SetActive(false);
        return view;
    }

    TMP_Text Label(string name, float size, float x, float y, float width, float height,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, Transform parent = null)
    {
        var text = ForensicUIStyle.Text(parent != null ? parent : transform, name, size,
            ForensicUIStyle.TextPrimary, alignment: alignment);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y);
        text.richText = false;
        return text;
    }

    public bool Show(CompetitiveEdition edition)
    {
        var result = edition.CompleteRun();
        return result != null && Show(result, edition.Leaderboard(result.rulesVersion), edition.ShowDemoRankings, edition.StorageMessage);
    }

    public bool Show(CompetitiveRunRecord result, List<CompetitiveRunRecord> board, bool demoEntries, string storageMessage)
    {
        if (result == null || board == null) return false;
        run = result;
        int index = board.FindIndex(r => r.playerId == run.playerId);
        if (index < 0) return false;
        StopAllCoroutines();
        foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
        total = board.Count; rank = index + 1;
        int first = Mathf.Clamp(index - 2, 0, Mathf.Max(0, total - 5));
        playerSlot = index - first;
        for (int i = first; i < Mathf.Min(first + 5, total); i++)
        {
            var entry = board[i]; bool you = entry.playerId == run.playerId;
            var image = ForensicUIStyle.Rect(transform, you ? "Your leaderboard row" : "Leaderboard row",
                you ? ForensicUIStyle.SelectedFill : ForensicUIStyle.HeaderBackground);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(650, 44);
            rect.anchoredPosition = new Vector2(0, RowTop - (i - first) * RowStep);
            var name = Label("Name", 23, -130, -22, 330, 38, parent: rect);
            name.text = $"#{i + 1}  " + (you ? "YOU  " : "") + entry.username + (entry.isDemo ? "  [DEMO]" : "");
            var rowResult = Label("Score and time", 22, 195, -22, 240, 38, TextAlignmentOptions.Right, rect);
            rowResult.text = $"{entry.score:0.##}   |   {CompetitiveRanking.FormatTime(entry.seconds)}";
            if (you) { playerRow = rect; name.fontStyle = FontStyles.Bold; }
            rows.Add(rect);
        }
        boardLabel.text = "LEADERBOARD  /  BEST RUN PER PLAYER";
        timeLabel.text = "Time taken  " + CompetitiveRanking.FormatTime(run.seconds);
        bool retainedBest = board[index].runId != run.runId;
        detailLabel.text = (retainedBest ? "Your earlier personal best keeps your leaderboard position.\n" : "Your result is on the leaderboard.\n")
            + (demoEntries ? "DEMO = sample opponent. " : "")
            + "Recorder notes: " + run.recorderPoints.ToString("0") + " / 24\n" + storageMessage;
        gameObject.SetActive(true);
        if (revealedRunId == run.runId) Finish();
        else { revealedRunId = run.runId; IsRevealing = true; StartCoroutine(Reveal()); }
        return true;
    }

    IEnumerator Reveal()
    {
        // Five visible rows keep the animation readable even on a large local board.
        float targetY = RowTop - playerSlot * RowStep;
        float bottomY = RowTop - (rows.Count - 1) * RowStep;
        for (int i = 0; i < rows.Count; i++)
            if (i > playerSlot) rows[i].anchoredPosition += Vector2.up * RowStep;
        playerRow.anchoredPosition = new Vector2(0, bottomY);
        for (float elapsed = 0; elapsed < 1.35f; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / 1.35f);
            fade.alpha = Mathf.Clamp01(elapsed / .22f);
            scoreLabel.text = (run.score * (1 - Mathf.Pow(1 - t, 3))).ToString("0.##") + " / 100";
            rankLabel.text = "Finding your leaderboard position...";
            yield return null;
        }
        scoreLabel.text = run.score.ToString("0.##") + " / 100";
        for (float elapsed = 0; elapsed < .9f; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / .9f);
            rankLabel.text = "Leaderboard rank  #" + Mathf.RoundToInt(Mathf.Lerp(total, rank, t)) + " of " + total;
            playerRow.anchoredPosition = new Vector2(0, Mathf.Lerp(bottomY, targetY, t));
            for (int i = playerSlot + 1; i < rows.Count; i++)
                rows[i].anchoredPosition = new Vector2(0, RowTop - (i - 1 + t) * RowStep);
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        IsRevealing = false;
        fade.alpha = 1;
        scoreLabel.text = run.score.ToString("0.##") + " / 100";
        rankLabel.text = "Leaderboard rank  #" + rank + " of " + total;
        for (int i = 0; i < rows.Count; i++) rows[i].anchoredPosition = new Vector2(0, RowTop - i * RowStep);
    }

    void OnDisable() { StopAllCoroutines(); IsRevealing = false; }
}
