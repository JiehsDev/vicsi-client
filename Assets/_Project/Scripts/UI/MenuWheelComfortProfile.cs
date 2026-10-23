using UnityEngine;

/// <summary>Utility/menu wheels only. The Tool Wheel deliberately does not use this profile.</summary>
public static class MenuWheelComfortProfile
{
    public const float Diameter = 660f;
    public const float LabelRadius = 250f;
    public const float CanvasScale = 0.0006f;
    public const float Distance = 0.65f;
    public const float FontSize = 24f;
    public const float MinimumPlaceholderAlpha = 0.65f;
    public static Vector2 LabelSize => new Vector2(112f, 60f);

    // Presentation only; the entry, action, availability and ordering stay intact.
    public static string Label(string name) => name switch
    {
        "IOC Checklist" => "IOC\nChecks",
        "Relationships" => "Evidence\nLinks",
        "Item Logs" => "Item\nLogs",
        "Submit Conclusion" => "Conclude",
        "Evidence Inventory" => "Inventory",
        "Closeout Inventory" => "Closeout",
        "Dialogue History" => "Dialogue\nHistory",
        "Witness Accounts" => "Witness\nAccounts",
        "Evidence Procedure" => "Procedure",
        "Scene Sketch" => "Scene\nSketch",
        _ => name
    };
}
