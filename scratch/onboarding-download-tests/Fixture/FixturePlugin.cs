using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace TestFixturePlugin;

public sealed class FixturePlugin : IStarPiePlugin
{
    private IPluginContext? _ctx;

    public void Initialize(IPluginContext context)
    {
        _ctx = context;
        context.Actions.Register(new KeypadLayerAction());
    }

    public void Shutdown()
    {
        _ctx = null;
    }
}

public sealed class KeypadLayerAction : IActionContribution
{
    public ActionDescriptor Descriptor { get; } = new()
    {
        Id = "keypadLayer",
        DisplayName = "按键映射",
        Category = "按键与映射"
    };

    public IReadOnlyList<ParameterField> Parameters { get; } = new List<ParameterField>
    {
        new()
        {
            Key = "keyMap",
            Label = "映射配置",
            Type = ParameterFieldType.KeyMap,
            Required = true
        },
        new()
        {
            Key = "targetProcess",
            Label = "目标进程",
            Type = ParameterFieldType.Text,
            Required = false
        }
    };

    public string? Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("keyMap", out var km) || string.IsNullOrWhiteSpace(km))
        {
            return "缺失必填参数：keyMap";
        }
        if (km.Contains("Invalid") || km.Contains("->"))
        {
            return "语法错误：非法映射配置";
        }
        return null;
    }

    public string Preview(IReadOnlyDictionary<string, string> parameters)
    {
        return parameters.TryGetValue("keyMap", out var v) && !string.IsNullOrWhiteSpace(v)
            ? $"映射：{v}"
            : "未配置映射";
    }

    public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
    {
        return Task.FromResult(ActionResult.Ok());
    }
}
