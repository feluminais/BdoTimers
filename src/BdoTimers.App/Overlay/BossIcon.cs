using System.Windows.Media;

namespace BdoTimers.App.Overlay;

/// <summary>Small outline faces; an unfamiliar boss keeps its name.</summary>
public sealed record BossIcon(string Name, Geometry? Outline)
{
    static readonly IReadOnlyDictionary<string, Geometry> Outlines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Nouver"] = "M9,4 L8,1 Q12,0 16,1 L15,4 Q19,2 21,5 L18,8 Q21,7 23,11 L20,12 Q23,17 19,19 Q17,19 16,21 L12,24 L8,21 Q7,19 5,19 Q1,17 4,12 L1,11 Q3,7 6,8 L3,5 Q5,2 9,4 M12,6 L10,12 Q12,14 14,12 Z M12,14 L10,18 Q12,20 14,18 Z M6,13 Q7,15 9,15 M15,15 Q17,15 18,13 M9,21 L9,20 M15,21 L15,20",
        ["Kzarka"] = "M5,9 L2,3 L8,6 L12,2 L16,6 L22,3 L19,9 L20,17 L16,22 L8,22 L4,17 Z M6,11 L10,13 M18,11 L14,13 M8,18 L10,16 L12,19 L14,16 L16,18 M12,8 L12,12",
        ["Karanda"] = "M8,7 L12,2 L16,7 Q20,6 22,10 L18,11 L22,14 L18,15 L20,18 L15,18 L12,23 L9,18 L4,18 L6,15 L2,14 L6,11 L2,10 Q4,6 8,7 M7,11 L10,12 M17,11 L14,12 M12,12 L9,16 L12,18 L15,16 Z",
        ["Kutum"] = "M8,5 L6,2 M16,5 L18,2 M5,11 L2,9 M19,11 L22,9 M5,17 L2,19 M19,17 L22,19 M12,3 Q5,4 5,13 Q5,22 12,23 Q19,22 19,13 Q19,4 12,3 Z M8,10 Q12,7 16,10 L15,18 L9,18 Z M9,13 L15,13 M10,16 L14,16",
        ["Offin"] = "M8,10 L5,7 L5,2 M5,6 L2,4 M16,10 L19,7 L19,2 M19,6 L22,4 M8,8 L12,5 L16,8 L18,16 L15,20 L17,23 L12,21 L7,23 L9,20 L6,16 Z M8,12 L10,13 M16,12 L14,13 M10,17 L14,17",
        ["Garmoth"] = "M7,8 L3,1 L10,5 L12,3 L14,5 L21,1 L17,8 L22,11 L18,14 L18,18 L12,23 L6,18 L6,14 L2,11 Z M6,10 L10,12 M18,10 L14,12 M9,17 L12,15 L15,17 L12,20 Z",
        ["Vell"] = "M4,8 Q4,2 12,2 Q20,2 20,8 L18,14 M6,14 L4,8 M8,10 L10,11 M16,10 L14,11 M9,15 Q12,12 15,15 M6,13 Q2,14 3,20 Q4,24 7,21 M9,16 L8,21 M12,16 L12,23 M15,16 L16,21 M18,13 Q22,14 21,20 Q20,24 17,21",
        ["Quint"] = "M5,5 L10,2 L18,3 L22,9 L19,12 L20,19 L15,23 L7,21 L3,15 Z M5,5 L8,9 L6,13 M18,3 L15,8 L18,12 M8,12 L10,12 M14,12 L16,12 M9,18 L15,18 M11,3 L12,7",
        ["Muraka"] = "M5,7 L2,10 L4,15 L4,19 L8,23 L16,23 L20,19 L20,15 L22,10 L19,7 L16,3 L8,3 Z M6,9 L10,11 M18,9 L14,11 M7,17 Q7,12 12,12 Q17,12 17,17 L15,20 L9,20 Z M10,16 L14,16",
        ["Golden Pig King"] = "M7,6 L5,2 L9,4 L12,1 L15,4 L19,2 L17,6 Z M5,10 L2,6 L7,8 M19,10 L22,6 L17,8 M5,10 Q2,20 12,23 Q22,20 19,10 M7,12 L8,12 M16,12 L17,12 M8,16 Q12,14 16,16 L16,19 Q12,21 8,19 Z M10,17 L10,18 M14,17 L14,18",
        ["Sangoon"] = "M5,8 Q1,1 8,4 L12,3 L16,4 Q23,1 19,8 Q23,18 17,21 L12,23 L7,21 Q1,18 5,8 Z M10,4 L12,7 L14,4 M3,11 L7,12 M21,11 L17,12 M4,16 L7,15 M20,16 L17,15 M8,10 L10,11 M16,10 L14,11 M10,15 L14,15 L12,17 Z M12,17 L12,20 M9,19 Q12,21 15,19",
        ["Bulgasal"] = "M6,8 L2,2 L9,5 L12,3 L15,5 L22,2 L18,8 L21,13 L18,21 L12,23 L6,21 L3,13 Z M7,11 L10,13 M17,11 L14,13 M7,18 L9,16 L12,19 L15,16 L17,18 M10,8 L12,6 L14,8",
        ["Uturi"] = "M4,10 Q4,2 12,2 Q20,2 20,10 L18,19 L12,23 L6,19 Z M4,8 L20,8 M8,8 L8,4 M16,8 L16,4 M7,12 L10,13 M17,12 L14,13 M10,18 L14,18 M3,10 L1,15 L5,17 M21,10 L23,15 L19,17",
    }.ToDictionary(pair => pair.Key, pair =>
    {
        var outline = Geometry.Parse(pair.Value);
        outline.Freeze();
        return outline;
    }, StringComparer.OrdinalIgnoreCase);

    public static BossIcon For(string name) => new(name, Outlines.GetValueOrDefault(name.Trim()));
}
