using System.Reflection;
using System.IO;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinPieGestures;

internal static class ArtStyleTests
{
    private static int failures;
    private static int passed;
    [STAThread]
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(Path.GetTempPath(), "StarPie-ArtStyle-Tests-" + Guid.NewGuid().ToString("N")));
        _ = new Application();
        RunDataTests();
        if (args.Contains("--data")) return Summary();
        SetConfig(JsonSerializer.Deserialize<AppConfig>("{\"SelectedArtStyleId\":\"paper\"}")!);
        var root = new Border();
        AppThemeManager.ApplyTheme(root, "Light");
        Check(((SolidColorBrush)root.Resources["WindowBackgroundBrush"]).Color.ToString() == "#FFF5F1E8", "selected paper style supplies app resources");
        Check(root.TryFindResource("CardCornerRadius") is CornerRadius { TopLeft: 4 }, "paper corner radius resource");
        Check(root.TryFindResource("ArtFontFamily") is FontFamily, "profile typography resource");
        foreach (var p in All(ConfigManager.CurrentConfig))
        {
            var colors = Value(p, "Colors");
            Check((double)Call("ArtStyleResolver","Contrast",Value(colors,"Text"),Value(colors,"Background")) >= 4.5, "body contrast " + Value(p,"Id"));
            Check((double)Call("ArtStyleResolver","Contrast",Value(colors,"WheelHighlightText"),Value(colors,"WheelHighlight")) >= 4.5, "hover contrast " + Value(p,"Id"));
        }
        bool hasNotify = typeof(AppConfig).Assembly.GetType("WinPieGestures.ArtStyles.ArtStyleService")!.GetMethod("NotifyChanged") != null;
        Check(hasNotify, "open-window refresh service exists");
        if (hasNotify)
        {
            var first = new Window(); var second = new Window();
            AppThemeManager.ApplyTheme(first,"Light"); AppThemeManager.ApplyTheme(second,"Light");
            Put(ConfigManager.CurrentConfig,"SelectedArtStyleId","obsidian");
            Call("ArtStyleService","NotifyChanged");
            Check(((SolidColorBrush)second.Resources["WindowBackgroundBrush"]).Color.ToString() == "#FF11171B", "second host window refreshes");
            Check(AppThemeManager.CurrentEffectiveTheme == "Dark", "dark metadata controls effective mode");
            Check(((SolidColorBrush)second.Resources["TextPrimaryBrush"]).IsFrozen, "semantic brushes frozen");
            Put(ConfigManager.CurrentConfig,"SelectedArtStyleId","");
            Call("ArtStyleService","NotifyChanged");
            Check(second.Resources["CardCornerRadius"] is CornerRadius { TopLeft: 12 }, "legacy metrics restored after leaving art style");
            first.Close(); second.Close();
        }
        return Summary();
    }
    private static int Summary() { Console.WriteLine($"Art style tests: {passed} passed, {failures} failed"); return failures == 0 ? 0 : 1; }
    private static object Call(string type, string method, params object[] args)
    {
        var target = typeof(AppConfig).Assembly.GetType("WinPieGestures.ArtStyles." + type)!;
        try { return target.GetMethods().Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!; }
        catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException!).Throw(); throw; }
    }
    private static object Value(object item, string name) => item.GetType().GetProperty(name)!.GetValue(item)!;
    private static void Put(object item, string name, object value) => item.GetType().GetProperty(name)!.SetValue(item, value);
    private static object[] All(AppConfig c) => ((IEnumerable)Call("ArtStyleCatalog", "GetAll", c)).Cast<object>().ToArray();
    private static void Reject(AppConfig c, string json, string name)
    {
        int before = All(c).Length;
        try { Call("ArtStyleService", "Import", c, json); Check(false, name); }
        catch (InvalidDataException) { Check(All(c).Length == before, name + " without mutation"); }
    }
    private static void RunDataTests()
    {
        bool exists = typeof(AppConfig).Assembly.GetType("WinPieGestures.ArtStyles.ArtStyleCatalog") != null;
        Check(exists, "five-style catalog exists");
        if (!exists) return;
        var c = new AppConfig();
        var profiles = All(c);
        Check(profiles.Length == 5, "five built-in complete styles");
        Put(Value(profiles[0], "Colors"), "Accent", "#123456");
        Check((string)Value(Value(All(c)[0], "Colors"), "Accent") != "#123456", "catalog copy is deeply isolated");
        foreach (var profile in All(c))
        {
            Check(!((IEnumerable)Call("ArtStyleResolver", "Validate", profile)).Cast<object>().Any(), "valid preset " + Value(profile, "Id"));
            string json = (string)Call("ArtStyleService", "Export", profile);
            var imported = Call("ArtStyleService", "Import", c, json);
            Check((string)Value(imported, "Id") != (string)Value(profile, "Id"), "import has independent ID " + Value(profile, "Id"));
            Check(JsonSerializer.Serialize(Value(imported, "Colors")) == JsonSerializer.Serialize(Value(profile, "Colors")), "palette roundtrip " + Value(profile, "Id"));
        }
        string original = (string)Call("ArtStyleService", "Export", All(c)[0]);
        var bad = JsonNode.Parse(original)!;
        bad["schemaVersion"] = 999; Reject(c, bad.ToJsonString(), "reject future format");
        bad = JsonNode.Parse(original)!; bad["theme"]!["colors"]!["accent"] = "not-a-color"; Reject(c, bad.ToJsonString(), "reject invalid color");
        bad = JsonNode.Parse(original)!; bad["theme"]!["cornerRadius"] = 1000; Reject(c, bad.ToJsonString(), "reject out-of-range radius");
        bad = JsonNode.Parse(original)!; bad["theme"]!["colors"] = null; Reject(c, bad.ToJsonString(), "reject null palette");
        bad = JsonNode.Parse(original)!; bad["theme"] = null; Reject(c, bad.ToJsonString(), "reject null theme");
        Reject(c, "{}", "reject missing envelope");
        Reject(c, new string(' ', 65537), "reject oversized file");
        var minimal = Call("ArtStyleService", "Import", c, "{\"schemaVersion\":1,\"theme\":{\"name\":\"Minimal\"}}");
        Check(Value(minimal, "Colors") != null, "missing optional fields use defaults");
        var data = All(c).Last(); Put(data, "ShadowBlur", double.NaN);
        Check(((IEnumerable)Call("ArtStyleResolver", "Validate", data)).Cast<object>().Any(), "reject non-finite value");
        double radius = c.WheelRadius;
        string actions = JsonSerializer.Serialize(c.Profiles);
        Call("ArtStyleService", "Select", c, "paper", true);
        Check(c.WheelRadius == radius && JsonSerializer.Serialize(c.Profiles) == actions, "theme selection preserves wheel geometry/actions");
        var custom = Call("ArtStyleService", "Import", c, original);
        Call("ArtStyleService", "Select", c, (string)Value(custom,"Id"), true);
        Call("ArtStyleService", "Delete", c, (string)Value(custom,"Id"));
        Check((string)Value(c,"SelectedArtStyleId") == "paper", "deleting active custom style selects built-in default");
        string configJson = JsonSerializer.Serialize(c);
        Check(All(JsonSerializer.Deserialize<AppConfig>(configJson)!).Length == All(c).Length, "user themes survive full config roundtrip");
        Check(Call("ArtStyleResolver", "Resolve", new AppConfig()) == null, "old config retains legacy appearance");
        Put(c,"SelectedArtStyleId","missing");
        Check((string)Value(Call("ArtStyleResolver","Resolve",c),"Id") == "paper", "missing theme uses safe fallback");
    }
    private static void SetConfig(AppConfig config) => typeof(ConfigManager).GetProperty("CurrentConfig")!.SetValue(null, config);
    private static void Check(bool condition, string name)
    {
        if (condition) { passed++; Console.WriteLine("PASS " + name); }
        else { failures++; Console.WriteLine("FAIL " + name); }
    }
}
