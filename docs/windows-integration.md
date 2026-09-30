# Windows 集成规范

## 低级钩子

- `WH_MOUSE_LL` 与 `WH_KEYBOARD_LL` 回调只捕获必要状态并快速返回。
- 禁止在回调线程执行文件/注册表 IO、网络、插件加载、目录扫描、等待或复杂日志格式化。
- 跨线程工作进入已有队列或 Dispatcher；不要在钩子中直接构造窗口。

## SendInput

- 对需要硬件语义的应用使用 `MapVirtualKey(vk, MAPVK_VK_TO_VSC)` 转换扫描码。
- 方向键、Delete、Insert、PageUp/PageDown、Home/End、Win 等扩展键设置 `KEYEVENTF_EXTENDEDKEY`。
- 组合键顺序为：修饰键按下 → 保持约 10–15 ms → 主键按下/释放 → 修饰键释放。
- 连续文本或多位数字优先使用 `KEYEVENTF_UNICODE` 字符流，避免输入法布局干扰。
- 自动测试不得向真实前台窗口发送输入；使用纯函数、假服务或空值短路探针。

## 全屏与场景隔离

- `Progman`、`WorkerW`、`SHELLDLL_DefView`、`SysListView32`、`Shell_TrayWnd`、`Shell_SecondaryTrayWnd` 不是需要拦截的独占全屏应用。
- `WhitelistedProcesses` 的旁路优先级高于全屏自动禁用，保证用户允许的 CAD、图像或开发工具仍可呼出轮盘。
- 多显示器和 DPI 计算明确窗口坐标、物理像素与 DIP；不要默认主屏或统一缩放。

## 托盘、UIPI 与退出

- `NotifyIcon`、托盘菜单、暂停/恢复、主题、语言、提权、退出和提示统一由 `TrayController` 管理。
- 管理员进程所需 `ChangeWindowMessageFilter` / `ChangeWindowMessageFilterEx` 放行保留在托盘/宿主生命周期，不放回设置窗口。
- 普通关闭设置窗口只隐藏/延迟释放；只有托盘退出、明确重启或应用退出流程调用 `Shutdown()`。

## Shell、窗口与系统服务

- 插件通过宿主服务请求命令、Shell 动词、窗口控制、截图、系统控制、轮盘和键盘映射，不在 UI 里复制执行逻辑。
- 每个会产生后果的服务检查自身能力；元数据列表可无能力读取，以便声明 UI。
- 能力门禁用于让授权说明与实际后果一致，不是进程内安全沙箱。
- 对不可撤销、提权或影响用户当前焦点的操作保留明确确认和可诊断失败信息。

## 验证

- 热路径修改需提供时序或性能证据，不能只靠构建。
- 钩子、UIPI、多显示器、输入模拟、前台焦点和系统动作必须由用户在隔离测试环境实机验收。
- 任何验证器都应选择即使门禁错误也无副作用的输入，例如空命令、空动词、无效坐标或空映射。
