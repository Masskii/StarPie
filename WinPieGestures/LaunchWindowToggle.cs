using System;
using System.Runtime.InteropServices;

namespace WinPieGestures;

/// <summary>
/// 窗口切换执行结果：强类型区分无窗口、已收回、已激活、以及激活失败。
/// </summary>
public enum LaunchWindowToggleResult
{
    /// <summary>未发现运行中进程或未发现合法的用户界面窗口，应继续走正常创建新实例流程。</summary>
    NoWindow,

    /// <summary>目标窗口此前处于前台活跃状态，已成功最小化收回。</summary>
    Minimized,

    /// <summary>目标窗口此前处于最小化或后台状态，已成功还原并激活至前台。</summary>
    Activated,

    /// <summary>发现有效窗口候选，但前台激活/还原尝试失败。禁止以此为由误启重复进程。</summary>
    ActivationFailed
}

/// <summary>
/// 窗口唤起与收回状态机：
/// 统一处理前台窗口收回、最小化窗口唤醒与激活核验。
/// 核心切换逻辑通过高阶函数解耦 Win32 API，便于离线无副作用回归测试。
/// </summary>
public static class LaunchWindowToggle
{
    private const int SW_SHOW = 5;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    /// <summary>
    /// 对已选出的目标窗口执行切换状态机：
    /// 1. 若当前窗口是前台窗口且未最小化：执行最小化收回；
    /// 2. 若窗口已最小化：执行还原并保守激活；
    /// 3. 若窗口在后台：显示并保守激活；
    /// 4. 激活后校验状态，区分 Activated 与 ActivationFailed，绝不返回伪成功。
    /// </summary>
    public static LaunchWindowToggleResult Toggle(
        WindowSnapshot candidate,
        Func<nint, bool> isForeground,
        Func<nint, bool> isIconic,
        Func<nint, bool> minimizeWindow,
        Func<nint, bool> restoreWindow,
        Func<nint, bool> activateWindow)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(isForeground);
        ArgumentNullException.ThrowIfNull(isIconic);
        ArgumentNullException.ThrowIfNull(minimizeWindow);
        ArgumentNullException.ThrowIfNull(restoreWindow);
        ArgumentNullException.ThrowIfNull(activateWindow);

        nint hwnd = candidate.Handle;

        // 若当前窗口处于前台且未最小化：收回（最小化）
        if (isForeground(hwnd) && !isIconic(hwnd))
        {
            minimizeWindow(hwnd);
            // 依据实际窗口状态判定，绝不盲信 ShowWindow 的“此前可见”返回值
            bool isMinimized = isIconic(hwnd);
            return isMinimized ? LaunchWindowToggleResult.Minimized : LaunchWindowToggleResult.ActivationFailed;
        }

        // 若处于最小化状态：先触发还原
        if (isIconic(hwnd))
        {
            restoreWindow(hwnd);
        }

        // 尝试激活至前台并核验状态：还原后不得仍为最小化，且必须成功激活
        bool activated = activateWindow(hwnd);
        bool stillIconic = isIconic(hwnd);
        return (activated && !stillIconic) ? LaunchWindowToggleResult.Activated : LaunchWindowToggleResult.ActivationFailed;
    }

    /// <summary>
    /// 生产环境入口：解析目标进程、确定性选择窗口候选，并执行切换状态机。
    /// </summary>
    public static LaunchWindowToggleResult TryToggle(string processOrExePath)
    {
        WindowSnapshot? candidate = ProcessWindowSelector.FindCandidateForProcess(processOrExePath);
        if (candidate == null)
        {
            return LaunchWindowToggleResult.NoWindow;
        }

        return Toggle(
            candidate,
            hwnd => GetForegroundWindow() == hwnd,
            hwnd => IsIconic(hwnd),
            hwnd => ShowWindow(hwnd, SW_MINIMIZE),
            hwnd => ShowWindow(hwnd, SW_RESTORE),
            hwnd => WindowTaskbarHelper.ActivateWindowConservative(hwnd) || GetForegroundWindow() == hwnd);
    }
}
