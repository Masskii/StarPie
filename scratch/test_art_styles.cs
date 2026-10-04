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
using WinPieGestures.ArtStyles;
using System.Windows.Media.Imaging;

internal static class ArtStyleTests
{
    private static int failures;
    private static int passed;
    [STAThread]
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(Path.GetTempPath(), "StarPie-ArtStyle-Tests-" + Guid.NewGuid().ToString("N")));
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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
        RunRendererTests();
        RunStudioTests(args);
        return Summary();
    }
    private static void RunStudioTests(string[] args)
    {
        var studioType = typeof(AppConfig).Assembly.GetType("WinPieGestures.ArtStyles.ThemeStudio");
        Check(studioType != null,"theme studio exists");
        if (studioType == null) return;
        var c = new AppConfig { SelectedArtStyleId = "paper" }; SetConfig(c);
        var studio = (UserControl)Activator.CreateInstance(studioType)!;
        var edit = studioType.GetMethod("BeginEdit",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var cancel = studioType.GetMethod("CancelDraft",BindingFlags.Instance|BindingFlags.NonPublic)!;
        edit.Invoke(studio,[true]);
        var font=(ComboBox)studio.FindName("FontBox"); font.ApplyTemplate();
        Check(font.Template.FindName("PART_EditableTextBox",font) is TextBox,"font selection supports editable text template");
        var name = (TextBox)studio.FindName("DraftNameBox");
        name.Text = "Saved Theme";
        string before = JsonSerializer.Serialize(c);
        var draft = studioType.GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(studio)!;
        Put(Value(draft,"Colors"),"Accent","#123456");
        Check(JsonSerializer.Serialize(c) == before,"draft editing does not alter current config");
        var fields=(StackPanel)studio.FindName("PaletteFields");
        var colorInput=fields.Children.OfType<Grid>().First().Children.OfType<TextBox>().Single();
        try
        {
            colorInput.Text="oops";
            Check(!((Button)studio.FindName("SaveDraftButton")).IsEnabled,"invalid color disables save without crashing editor");
        }
        catch (Exception) { Check(false,"invalid color disables save without crashing editor"); }
        cancel.Invoke(studio,null);
        Check(JsonSerializer.Serialize(c) == before,"cancel leaves current config intact");
        edit.Invoke(studio,[true]); name.Text = "Saved Theme";
        studioType.GetMethod("SaveDraft_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(studio,[studio,new RoutedEventArgs()]);
        Check(c.CustomArtStyles.Count == 1 && c.CustomArtStyles[0].Name == "Saved Theme","studio saves independent user theme");
        Check(c.SelectedArtStyleId == c.CustomArtStyles[0].Id,"studio applies saved theme");
        var paletteBefore = ((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color;
        var previewApi = typeof(AppConfig).Assembly.GetType("WinPieGestures.ArtStyles.ArtStyleResources")!.GetMethod("ApplyPreview");
        Check(previewApi != null,"isolated draft preview resources exist");
        if (previewApi != null)
        {
            previewApi.Invoke(null,[new Border(),Call("ArtStyleCatalog","Find",c,"obsidian")]);
            Check(((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color == paletteBefore,"draft preview does not publish application resources");
        }
        foreach (var lang in Enum.GetValues<LanguageCode>())
        {
            I18n.CurrentLanguage = lang;
            studioType.GetMethod("Refresh")!.Invoke(studio,null);
            Check(!string.IsNullOrEmpty(((TextBlock)studio.FindName("TitleText")).Text),"studio title translated " + lang);
            Check(ArtStyleText.Keys.All(key=>!string.IsNullOrWhiteSpace(ArtStyleText.Get(key)) && (lang!=LanguageCode.En || !System.Text.RegularExpressions.Regex.IsMatch(ArtStyleText.Get(key),"[\\u4E00-\\u9FFF]"))),"all studio strings translated " + lang);
        }
        if (args.Contains("--render")) RenderStyles();
        c.AutoCheckUpdate=false; c.EnableMultiTier=true;
        var actions=Enumerable.Range(0,8).Select(i=>new ActionItem { Name="Command "+i,Parameter="CTRL+C" }).ToList();
        actions[0].SubActions=[new ActionItem { Name="Subcommand",Parameter="CTRL+V" }];
        c.Profiles=[new WheelProfile { Actions=actions }];
        SetConfig(c);
        var settings=new SettingsWindow();
        Check(settings.FindName("ThemeStudioControl") is UserControl,"studio integrated in native settings");
        ((Grid)settings.FindName("AppearanceSettingsGrid")).Visibility=Visibility.Visible;
        var render=typeof(SettingsWindow).GetMethod("RenderLiveWheelPreview",BindingFlags.NonPublic|BindingFlags.Instance)!;
        render.Invoke(settings,null);
        var field=typeof(SettingsWindow).GetField("_previewSubContainers",BindingFlags.NonPublic|BindingFlags.Instance)!;
        int firstCount=((IList)field.GetValue(settings)!).Count;
        render.Invoke(settings,null);
        Check(firstCount>0 && ((IList)field.GetValue(settings)!).Count==firstCount,"repeated preview does not retain stale secondary containers");
        if (args.Contains("--render")) Capture((FrameworkElement)settings.Content,1220,740,Path.GetFullPath("artifacts/art-styles/settings-studio.png"));
        typeof(SettingsWindow).GetField("_isClosingForRelease",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(settings,true);
        settings.Close();
    }
    private static void RenderStyles()
    {
        I18n.CurrentLanguage=LanguageCode.ZhCn;
        var folder=Path.GetFullPath("artifacts/art-styles"); Directory.CreateDirectory(folder);
        var gallery=new StackPanel { Orientation=Orientation.Horizontal };
        foreach (var profile in ArtStyleCatalog.GetAll(new AppConfig()))
        {
            SetConfig(new AppConfig { SelectedArtStyleId=profile.Id });
            var studio=new ThemeStudio { Width=440 };
            var surface=new Border { Background=ArtStyleResources.Brush(profile.Colors.Background),Padding=new Thickness(15),Child=studio };
            ArtStyleResources.ApplyPreview(surface,profile);
            Capture(surface,470,920,Path.Combine(folder,profile.Id+"-studio.png"));
            var tile=new Border { Width=300,Padding=new Thickness(20),Background=ArtStyleResources.Brush(profile.Colors.Background) };
            var stack=new StackPanel();
            stack.Children.Add(new TextBlock { Text=ArtStyleText.Name(profile),FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=ArtStyleResources.Brush(profile.Colors.Text),Margin=new Thickness(0,0,0,18) });
            stack.Children.Add(ArtStyleSamples.Wheel(profile,260));
            stack.Children.Add(new TextBlock { Text=ArtStyleText.Get(profile.Id+".Desc"),TextWrapping=TextWrapping.Wrap,FontSize=14,Foreground=ArtStyleResources.Brush(profile.Colors.Muted),Margin=new Thickness(0,20,0,0) });
            tile.Child=stack; gallery.Children.Add(tile);
        }
        Capture(gallery,1500,430,Path.Combine(folder,"style-gallery.png"));
        SetConfig(new AppConfig { SelectedArtStyleId="obsidian" });
        var editor=new ThemeStudio { Width=440 };
        typeof(ThemeStudio).GetMethod("BeginEdit",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,[true]);
        ((Expander)editor.FindName("AdvancedPanel")).IsExpanded=true;
        Capture(editor,440,1900,Path.Combine(folder,"theme-editor.png"));
    }
    private static void Capture(FrameworkElement visual,int width,int height,string path)
    {
        visual.Measure(new Size(width,height)); visual.Arrange(new Rect(0,0,width,height)); visual.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(path); encoder.Save(stream);
    }
    private static void RunRendererTests()
    {
        var c = JsonSerializer.Deserialize<AppConfig>("{\"SelectedArtStyleId\":\"paper\"}")!;
        SetConfig(c);
        var factory = typeof(StyleRendererFactory).GetMethods().FirstOrDefault(m => m.Name == "CreateRenderer" && m.GetParameters().Length == 3);
        Check(factory != null, "wheel factory supports art style and tier context");
        if (factory == null) return;
        IRadialStyleRenderer Create(bool sub) => (IRadialStyleRenderer)factory.Invoke(null, ["ClassicRing", c, sub])!;
        var wheel = Create(false); wheel.Initialize("Light",c);
        Check(((SolidColorBrush)wheel.DefaultSectorBrush).Color.ToString() == "#FFFFFCF5", "wheel uses paper palette");
        Check(((SolidColorBrush)wheel.DefaultSectorBrush).IsFrozen, "wheel brushes frozen");
        var path = new System.Windows.Shapes.Path(); wheel.ApplySectorHighlight(path,true);
        Check(path.Effect == null || path.Effect.IsFrozen, "wheel effect frozen");
        c.ArtStyleFollowsWheel = false;
        Check(Create(false).GetType().Name == "ClassicRingRenderer", "follow disabled retains legacy renderer");
        c.ArtStyleFollowsWheel = true;
        c.UseIndependentSubWheelTheme = true;
        Check(Create(true).GetType().Name == "ClassicRingRenderer", "independent secondary appearance preserved");
        c.UseIndependentSubWheelTheme = false;
        Check(Create(true).GetType().Name == "ArtStyleRenderer", "secondary inherits whole style");
        var custom = Call("ArtStyleService","Import",c,(string)Call("ArtStyleService","Export",All(c)[0]));
        Put(custom,"DecorationStrength",0d); Call("ArtStyleService","Save",c,custom);
        Call("ArtStyleService","Select",c,Value(custom,"Id"),true);
        wheel = Create(false); wheel.Initialize("Light",c);
        var canvas = new Canvas(); wheel.RenderDecorations(canvas,new Grid(),100,100,80,30,0);
        Check(canvas.Children.Count == 0,"zero decoration strength draws no decoration");
        c.SelectedArtStyleId = "obsidian";
        c.ShowText = true; c.IconLayoutMode = "TextOnly";
        var actions = Enumerable.Range(0,8).Select(i => new ActionItem { Name = "Action " + i, Type = "Hotkey", Parameter = "CTRL+C" }).ToList();
        actions[1].CustomTextColor = "#FFAA00";
        actions[0].SubActions = [new ActionItem { Name = "Sub", CustomTextColor = "#FFAA00", LayoutMode = "TextOnly" }];
        var profile = new WheelProfile { Actions = actions, SectorCount = 8 };
        c.Profiles = [profile];
        var radial = new RadialWindow(new Point(400,400),profile);
        typeof(RadialWindow).GetMethod("RebuildVisualsFromCurrentConfiguration",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(radial,[ConfigManager.ConfigurationRevision]);
        var texts = (IEnumerable)typeof(RadialWindow).GetField("_contentTextBlocks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(radial)!;
        var textItems = texts.Cast<TextBlock?>().ToArray();
        radial.HighlightSector(0,-1,false);
        Check(((SolidColorBrush)textItems[0]!.Foreground).Color.ToString() == "#FF102421", "real wheel uses readable Obsidian hover text");
        radial.HighlightSector(1,-1,false);
        Check(((SolidColorBrush)textItems[1]!.Foreground).Color.ToString() == "#FFFFAA00", "real wheel preserves per-action color on hover");
        radial.HighlightSector(0,0,true);
        var containers = (IEnumerable)typeof(RadialWindow).GetField("_subContentContainers",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(radial)!;
        var subText = containers.Cast<Grid>().SelectMany(g=>g.Children.OfType<StackPanel>()).SelectMany(s=>s.Children.OfType<TextBlock>()).FirstOrDefault();
        Check(subText != null && ((SolidColorBrush)subText.Foreground).Color.ToString() == "#FFFFAA00","real secondary wheel preserves action color on hover");
        radial.Close();
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
        Reject(c, "{\"schemaVersion\":1,\"theme\":{\"name\":\"Unicode\"},\"ignored\":\""+new string('雪',23000)+"\"}", "reject oversized UTF-8 file");
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
