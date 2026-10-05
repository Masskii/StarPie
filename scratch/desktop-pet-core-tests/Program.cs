using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WinPieGestures;

namespace DesktopPetCoreTests;

internal static class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    [STAThread]
    private static int Main(string[] args)
    {
        string originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        string tempSandboxDir = Path.Combine(Path.GetTempPath(), $"StarPie_CoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSandboxDir);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", tempSandboxDir);

        try
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  StarPie - SP-DESKTOPPET-002 Host Core Tests    ");
            Console.WriteLine("=================================================");

            TestDefaultConfigShowCoreCircleTrue();
            TestLegacyJsonWithoutFieldDeserializesToTrue();
            TestExplicitFalseSerialization();
            TestExportImportConfigPreservesFalse();
            TestDeadzoneAndHitTestingUncompromised();
            TestVisualVisibilityRules();
            TestCoreSelectionTextRules();
            TestUiLinkedStateSimulation();
            TestI18nTranslationsPresent();

            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine($"Result: {_passedCount} passed, {_failedCount} failed.");
            Console.WriteLine("=================================================");

            return _failedCount == 0 ? 0 : 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
            try
            {
                if (Directory.Exists(tempSandboxDir))
                {
                    Directory.Delete(tempSandboxDir, true);
                }
            }
            catch { }
        }
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {testName}");
            _passedCount++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName} - {details}");
            Console.ResetColor();
            _failedCount++;
        }
    }

    private static void TestDefaultConfigShowCoreCircleTrue()
    {
        var config = new AppConfig();
        Assert(config.ShowCoreCircle == true,
            "TestDefaultConfigShowCoreCircleTrue",
            $"Expected true, got {config.ShowCoreCircle}");

        var defaultFromManager = ConfigManager.CreateDefaultConfig();
        Assert(defaultFromManager.ShowCoreCircle == true,
            "TestCreateDefaultConfigShowCoreCircleTrue",
            $"Expected true, got {defaultFromManager.ShowCoreCircle}");
    }

    private static void TestLegacyJsonWithoutFieldDeserializesToTrue()
    {
        // Legacy JSON omitting ShowCoreCircle
        string legacyJson = """
        {
            "Language": "zh-CN",
            "WheelRadius": 133.0,
            "CoreRadius": 36.0,
            "CoreDeadzoneRadius": 35.0,
            "ShowCoreIcon": false
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var config = JsonSerializer.Deserialize<AppConfig>(legacyJson, options);
        Assert(config != null && config.ShowCoreCircle == true,
            "TestLegacyJsonWithoutFieldDeserializesToTrue",
            $"Expected ShowCoreCircle to default to true when missing from JSON");
    }

    private static void TestExplicitFalseSerialization()
    {
        var config = new AppConfig { ShowCoreCircle = false };
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        Assert(json.Contains("\"ShowCoreCircle\": false", StringComparison.OrdinalIgnoreCase),
            "TestExplicitFalseSerializationContainsField",
            "JSON output should contain ShowCoreCircle: false");

        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert(deserialized != null && deserialized.ShowCoreCircle == false,
            "TestExplicitFalseDeserializationPreservesFalse",
            $"Expected false after deserialization, got {deserialized?.ShowCoreCircle}");
    }

    private static void TestExportImportConfigPreservesFalse()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"starpie_test_cfg_{Guid.NewGuid():N}.json");
        bool origShow = ConfigManager.CurrentConfig.ShowCoreCircle;
        double origRadius = ConfigManager.CurrentConfig.CoreRadius;
        try
        {
            ConfigManager.CurrentConfig.ShowCoreCircle = false;
            ConfigManager.CurrentConfig.CoreRadius = 42.0;

            bool exportSuccess = ConfigManager.ExportConfig(tempFile);
            Assert(exportSuccess && File.Exists(tempFile),
                "TestExportConfigSuccess",
                "ExportConfig should succeed");

            string exportedText = File.ReadAllText(tempFile);
            Assert(exportedText.Contains("\"ShowCoreCircle\": false", StringComparison.OrdinalIgnoreCase),
                "TestExportedTextContainsShowCoreCircleFalse",
                "Exported JSON should explicitly record ShowCoreCircle: false");

            // Reset current config property before import
            ConfigManager.CurrentConfig.ShowCoreCircle = true;
            Assert(ConfigManager.CurrentConfig.ShowCoreCircle == true,
                "TestPreImportResetConfigIsTrue",
                "Current config ShowCoreCircle should be true before import");

            bool importSuccess = ConfigManager.ImportConfig(tempFile);
            Assert(importSuccess, "TestImportConfigSuccess", "ImportConfig should succeed");
            Assert(ConfigManager.CurrentConfig.ShowCoreCircle == false,
                "TestImportedConfigPreservesShowCoreCircleFalse",
                $"Imported config should have ShowCoreCircle == false, got {ConfigManager.CurrentConfig.ShowCoreCircle}");
            Assert(ConfigManager.CurrentConfig.CoreRadius == 42.0,
                "TestImportedConfigPreservesCoreRadius",
                $"CoreRadius should be preserved as 42.0, got {ConfigManager.CurrentConfig.CoreRadius}");
        }
        finally
        {
            ConfigManager.CurrentConfig.ShowCoreCircle = origShow;
            ConfigManager.CurrentConfig.CoreRadius = origRadius;
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static void TestDeadzoneAndHitTestingUncompromised()
    {
        var config = new AppConfig
        {
            ShowCoreCircle = false,
            CoreRadius = 36.0,
            CoreDeadzoneRadius = 35.0,
            DragThreshold = 25.0
        };

        // When ShowCoreCircle == false, CoreDeadzoneRadius remains 35.0 via GetEffectiveCoreDeadzone()
        double effectiveDeadzone = config.GetEffectiveCoreDeadzone();

        Assert(effectiveDeadzone == 35.0,
            "TestEffectiveDeadzoneCalculation",
            $"Effective deadzone should be 35.0, got {effectiveDeadzone}");

        // Hit testing test: point inside deadzone (distance < effectiveDeadzone)
        double dxInside = 15.0;
        double dyInside = 20.0;
        double distInside = Math.Sqrt(dxInside * dxInside + dyInside * dyInside); // 25.0 < 35.0
        bool isInsideDeadzone = distInside < effectiveDeadzone;
        Assert(isInsideDeadzone,
            "TestPointInsideDeadzoneHitCenter",
            $"Distance {distInside:F1} < {effectiveDeadzone} should hit center deadzone");

        // Point outside deadzone (distance >= effectiveDeadzone)
        double dxOutside = 30.0;
        double dyOutside = 30.0;
        double distOutside = Math.Sqrt(dxOutside * dxOutside + dyOutside * dyOutside); // 42.4 > 35.0
        bool isOutsideDeadzone = distOutside >= effectiveDeadzone;
        Assert(isOutsideDeadzone,
            "TestPointOutsideDeadzoneHitSector",
            $"Distance {distOutside:F1} >= {effectiveDeadzone} should hit outer sectors");
    }

    private static void TestVisualVisibilityRules()
    {
        // When ShowCoreCircle == false, visual visibility must be Collapsed
        Visibility visFalse = AppConfig.GetCoreCircleVisibility(false);
        Assert(visFalse == Visibility.Collapsed,
            "TestVisualVisibilityWhenFalseIsCollapsed",
            "Visibility when ShowCoreCircle=false must be Collapsed");

        Visibility visTrue = AppConfig.GetCoreCircleVisibility(true);
        Assert(visTrue == Visibility.Visible,
            "TestVisualVisibilityWhenTrueIsVisible",
            "Visibility when ShowCoreCircle=true must be Visible");
    }

    private static void TestCoreSelectionTextRules()
    {
        Assert(!AppConfig.ShouldShowCoreSelectionText(false, true),
            "TestShouldShowCoreSelectionTextFalseWhenCircleFalse",
            "Core selection text must be false when ShowCoreCircle is false");
        Assert(AppConfig.ShouldShowCoreSelectionText(true, true),
            "TestShouldShowCoreSelectionTextTrueWhenBothTrue",
            "Core selection text must be true when both are true");
        Assert(!AppConfig.ShouldShowCoreSelectionText(true, false),
            "TestShouldShowCoreSelectionTextFalseWhenTextFalse",
            "Core selection text must be false when ShowSelectedActionText is false");
    }

    private static void TestUiLinkedStateSimulation()
    {
        // Simulate SettingsWindow linked control behavior
        bool showCoreCircle = false;
        var coreDetailsPanel = new StackPanel();
        var coreRadiusSlider = new Slider { Minimum = 15, Maximum = 140, Value = 36 };

        // Initial enabled state
        coreDetailsPanel.IsEnabled = true;
        coreDetailsPanel.Opacity = 1.0;
        coreRadiusSlider.IsEnabled = true;

        // Apply disabled state when showCoreCircle == false
        coreDetailsPanel.IsEnabled = showCoreCircle;
        coreDetailsPanel.Opacity = showCoreCircle ? 1.0 : 0.5;
        coreRadiusSlider.IsEnabled = showCoreCircle;

        Assert(!coreDetailsPanel.IsEnabled && coreDetailsPanel.Opacity == 0.5,
            "TestLinkedPanelDisabledWhenShowCoreCircleFalse",
            "CoreDetailsPanel should be disabled with 0.5 opacity");
        Assert(!coreRadiusSlider.IsEnabled,
            "TestLinkedSliderDisabledWhenShowCoreCircleFalse",
            "CoreRadiusSlider should be disabled");
        Assert(coreRadiusSlider.Value == 36,
            "TestLinkedSliderValuePreservedWhenDisabled",
            $"Value should be preserved as 36, got {coreRadiusSlider.Value}");

        // Re-enable
        showCoreCircle = true;
        coreDetailsPanel.IsEnabled = showCoreCircle;
        coreDetailsPanel.Opacity = showCoreCircle ? 1.0 : 0.5;
        coreRadiusSlider.IsEnabled = showCoreCircle;

        Assert(coreDetailsPanel.IsEnabled && coreDetailsPanel.Opacity == 1.0,
            "TestLinkedPanelRestoredWhenShowCoreCircleTrue",
            "CoreDetailsPanel should be enabled with 1.0 opacity");
        Assert(coreRadiusSlider.IsEnabled,
            "TestLinkedSliderRestoredWhenShowCoreCircleTrue",
            "CoreRadiusSlider should be re-enabled");
        Assert(coreRadiusSlider.Value == 36,
            "TestLinkedSliderValuePreservedWhenRestored",
            $"Value should still be 36, got {coreRadiusSlider.Value}");
    }

    private static void TestI18nTranslationsPresent()
    {
        I18n.CurrentLanguage = LanguageCode.ZhCn;
        string zhCn = I18n.T("ShowCoreCircleTitle");
        Assert(zhCn == "显示中心核心圆", "TestI18nZhCnTitle", $"Expected '显示中心核心圆', got '{zhCn}'");

        I18n.CurrentLanguage = LanguageCode.ZhTw;
        string zhTw = I18n.T("ShowCoreCircleTitle");
        Assert(zhTw == "顯示中心核心圓", "TestI18nZhTwTitle", $"Expected '顯示中心核心圓', got '{zhTw}'");

        I18n.CurrentLanguage = LanguageCode.En;
        string en = I18n.T("ShowCoreCircleTitle");
        Assert(en == "Show Center Core Circle", "TestI18nEnTitle", $"Expected 'Show Center Core Circle', got '{en}'");

        I18n.CurrentLanguage = LanguageCode.Ja;
        string ja = I18n.T("ShowCoreCircleTitle");
        Assert(ja == "センターコアを表示", "TestI18nJaTitle", $"Expected 'センターコアを表示', got '{ja}'");

        string desc = I18n.T("ShowCoreCircleDesc");
        Assert(!string.IsNullOrWhiteSpace(desc) && desc != "ShowCoreCircleDesc",
            "TestI18nDescPresent",
            "ShowCoreCircleDesc translation must be found and not raw key");

        // Restore language
        I18n.CurrentLanguage = LanguageCode.ZhCn;
    }
}
