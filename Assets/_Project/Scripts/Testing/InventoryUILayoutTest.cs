using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>Real authored inventory UI, synthetic sealed items, rendered glyph bounds and scroll/pointer navigation.</summary>
public sealed class InventoryUILayoutTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly StringBuilder report = new("=== InventoryUILayoutTest ===\n");
    private int failures;
    private void Check(string message, bool passed) { report.AppendLine((passed ? "PASS: " : "FAIL: ") + message); if (!passed) failures++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    private static bool Fits(TMP_Text text)
    {
        text.ForceMeshUpdate(true);
        var rect = text.rectTransform.rect;
        return !text.enableAutoSizing && !text.isTextTruncated && text.textInfo.characterInfo.Take(text.textInfo.characterCount)
            .Where(c => c.isVisible).All(c => c.topLeft.x >= rect.xMin - .5f && c.bottomRight.x <= rect.xMax + .5f
                && c.topLeft.y <= rect.yMax + .5f && c.bottomRight.y >= rect.yMin - .5f);
    }
    public void RunAllTests() => StartCoroutine(Run());
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(InventoryUILayoutTest));
        RelationshipReviewTestUtil.BeginCaseFixture();
        NarrativeEventManager.Instance.StopAllCoroutines(); NarrativeEventManager.Instance.enabled = false;
        var ui = Resources.FindObjectsOfTypeAll<EvidenceInventoryUI>().First(x => x.gameObject.scene.IsValid());
        ui.Show(); yield return null;
        var layout = ui.GetComponent<ResponsivePanelLayout>();
        var empty = Field<TMP_Text>(ui, "emptyLabel");
        var root = (RectTransform)ui.transform;
        Check("Empty inventory opens in a flexible bounded container", ui.gameObject.activeSelf && root.rect.height < 740 && empty.gameObject.activeSelf && Fits(empty));
        Check("Inventory retains shared wall-safe HUD placement", ui.GetComponent<HudFollowCamera>() != null);
        var records = Field<Dictionary<string, EvidenceRecord>>(EvidenceStateManager.Instance, "records");
        var entries = Field<IDictionary>(EvidenceInventory.Instance, "entries");
        var definitions = new List<EvidenceDefinition>();
        for (int i = 0; i < 6; i++)
        {
            var def = ScriptableObject.CreateInstance<EvidenceDefinition>();
            def.evidenceId = "inventory-layout-" + i;
            def.displayName = i == 1 ? string.Join(" ", Enumerable.Repeat("Lifted fingerprint sample from the kitchen knife with multiple identified contributors", 4)) : "Sealed item " + i;
            definitions.Add(def);
            records.Add(def.evidenceId, new EvidenceRecord { definition = def, status = EvidenceStatus.Sealed, tentNumber = i + 1 });
            Check("Sealed fixture item " + i + " enters through real inventory admission", EvidenceInventory.Instance.Add(def.evidenceId));
        }
        yield return null;
        var rows = Field<List<EvidenceInventoryEntryView>>(ui, "rows");
        var longRow = rows[1];
        longRow.Bind(definitions[1].evidenceId, definitions[1], false, false,
            string.Join(" ", Enumerable.Repeat("This item cannot be equipped while another panel owns the interaction. Close that panel before continuing.", 3)));
        Invoke(ui, "RefreshLayout"); yield return null;
        Check("Rows grow independently for long names and lock reasons", ((RectTransform)longRow.transform).rect.height > ((RectTransform)rows[0].transform).rect.height);
        Check("All visible item text wraps without shrinking, clipping or glyph overflow", rows.All(r => r.GetComponentsInChildren<TMP_Text>().All(Fits)));
        Check("Readable name and detail font sizes are preserved", Field<TMP_Text>(longRow, "nameLabel").fontSize == 28 && Field<TMP_Text>(longRow, "lockedReasonLabel").fontSize == 24);
        Check("Rows retain equal widths and non-overlapping 12-unit gaps", rows.All(r => Mathf.Abs(((RectTransform)r.transform).rect.width - 720) < .1f)
            && Enumerable.Range(1, rows.Count - 1).All(i => { var a=(RectTransform)rows[i-1].transform; var b=(RectTransform)rows[i].transform; return Mathf.Abs(a.anchoredPosition.y-b.anchoredPosition.y-a.rect.height-12)<.2f; }));
        Check("Name retains 20-unit horizontal and 16-unit vertical row padding", Field<TMP_Text>(longRow,"nameLabel").transform.parent is RectTransform col
            && col.offsetMin == new Vector2(20,16) && col.offsetMax == new Vector2(-20,-16));
        Check("Overflow caps the panel at 740 with real vertical scrolling", root.rect.height == 740 && layout.Body.CanScroll && layout.Body.Scroll.vertical && !layout.Body.Scroll.horizontal);
        float height = ((RectTransform)longRow.transform).rect.height;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 600); Invoke(ui,"RefreshLayout"); yield return null;
        Check("Narrower container increases row height and retains all text", ((RectTransform)longRow.transform).rect.height > height && longRow.GetComponentsInChildren<TMP_Text>().All(Fits));
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800); Invoke(ui,"RefreshLayout");
        Invoke(ui,"SetSelectedIndex",5);
        var last=(RectTransform)rows[5].transform;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Body.Viewport,last);
        Check("Thumbstick selection scrolls the last item fully into view", bounds.min.y >= layout.Body.Viewport.rect.yMin-.5f && bounds.max.y <= layout.Body.Viewport.rect.yMax+.5f);
        var pointer=Field<WorldPanelPointer>(ui,"pointer");
        Check("Clipped rows cannot intercept inventory pointer hits", pointer.HitTest(pointer.WorldCentre("inventory-item-0"))?.id != "inventory-item-0");
        Invoke(ui,"SetSelectedIndex",4);
        layout.EnsureVisible(last);
        var point=pointer.WorldCentre("inventory-item-5");
        string hit=pointer.ClickWithRay(new Ray(point-ui.transform.forward*.5f,ui.transform.forward));
        Check("Resized inventory row remains selectable by controller ray", hit=="inventory-item-5" && Field<int>(ui,"selectedIndex")==5);
        Invoke(ui,"HandleSelect");
        Check("Locked item action preserves custody", !EvidenceInventory.Instance.IsHeld(definitions[5].evidenceId));
        ui.Hide(); Check("Closing releases UI ownership", !PlayerUIGate.IsBlocked);
        ui.Show(); yield return null;
        Check("Reopening retains one layout and restores six real entries", ui.GetComponents<ResponsivePanelLayout>().Length==1 && Field<List<EvidenceInventoryEntryView>>(ui,"rows").Count==6);
        foreach(var def in definitions) { entries.Remove(def.evidenceId); records.Remove(def.evidenceId); Destroy(def); }
        Invoke(ui,"RefreshEntries"); yield return null;
        Check("Removing items restores a compact readable empty state", Field<List<EvidenceInventoryEntryView>>(ui,"rows").Count==0 && empty.gameObject.activeSelf && Fits(empty) && root.rect.height<740);
        ui.Hide();
        report.AppendLine(failures==0 ? "=== PASS, 0 failures ===" : "=== FAIL: "+failures+" failure(s) ===");
        Report=report.ToString(); IsDone=true;
    }
}
