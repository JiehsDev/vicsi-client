// Assets/_Project/Scripts/Deduction/EvidenceBoardController.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the connections between EvidenceNodes on the IOC evidence board.
/// Dropping one node onto another toggles a connection between them - this
/// is a drag-and-connect corkboard, not a multiple-choice quiz. Also wires
/// the Submit Theory button (in code, not an Inspector-serialized UnityEvent)
/// to DeductionScorer and displays the result.
/// </summary>
public class EvidenceBoardController : MonoBehaviour
{
    [SerializeField] private Transform lineContainer;
    [SerializeField] private ConnectionLine linePrefab;
    [SerializeField] private DeductionScorer scorer;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text resultLabel;

    private readonly Dictionary<NodePairId, ConnectionLine> activeConnections = new();

    private void Start()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitTheory);
        }
    }

    public void BeginDrag(EvidenceNode node)
    {
        node.RectTransform.SetAsLastSibling();
    }

    public void EndDrag(EvidenceNode node)
    {
        // Connections are made/broken via OnDrop, not here - dropping on
        // empty board space just repositions the card.
    }

    /// <summary>Toggles the connection between two nodes: makes it if absent, breaks it if present.</summary>
    public void TryConnect(EvidenceNode a, EvidenceNode b)
    {
        var pair = MakePair(a.nodeId, b.nodeId);

        if (activeConnections.TryGetValue(pair, out var existingLine))
        {
            if (existingLine != null)
            {
                Destroy(existingLine.gameObject);
            }
            activeConnections.Remove(pair);
            return;
        }

        var line = Instantiate(linePrefab, lineContainer);
        line.Bind(a.RectTransform, b.RectTransform);
        activeConnections[pair] = line;
    }

    public bool IsConnected(string nodeIdA, string nodeIdB)
    {
        return activeConnections.ContainsKey(MakePair(nodeIdA, nodeIdB));
    }

    public void SubmitTheory()
    {
        if (scorer == null)
        {
            return;
        }

        var result = scorer.Score(this);

        if (resultLabel != null)
        {
            resultLabel.text =
                $"Theory score: {result.total:P0}\n" +
                $"Links {result.requiredLinksScore:P0} - No contradictions {result.contradictionScore:P0} - Time {result.timeScore:P0}";
        }

        Debug.Log($"[EvidenceBoardController] Theory submitted. total={result.total:F2} " +
            $"required={result.requiredLinksScore:F2} contradictions={result.contradictionScore:F2} " +
            $"time={result.timeScore:F2} elapsed={result.elapsedSeconds:F1}s");
    }

    // Order-independent so (A,B) and (B,A) resolve to the same connection.
    private static NodePairId MakePair(string idA, string idB)
    {
        return string.CompareOrdinal(idA, idB) <= 0 ? new NodePairId(idA, idB) : new NodePairId(idB, idA);
    }
}
