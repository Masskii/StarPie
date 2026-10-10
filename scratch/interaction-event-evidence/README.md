# 第一阶段收口证据（2026-10-09，R3）

状态：本轮两个代码缺口已修复；自动门禁与独立复核候选收口，**人工门禁未通过，完整音效插件迁移未完成**。

工作区：C:\Users\A\Desktop\git repo\StarPie。
基准：81bb64f2c30a9371b2b1f270ececeb87ce359ce4。
官方子模块：f3f6519a1856b6b46bde29750df67a5a9e27a8f7（用户已有指针差异，内部跟踪文件未改）。
保留用户 AGENTS.md 修改及原有未跟踪文本文件。

## 本轮授权与实现

用户追加 RadialWindow.xaml.cs 的最小完成接缝以及一轮收口。不重置前两轮已使用的预算，不提前进入音效迁移。

- 旧四参数 Present 接口保持；新内部入口在已有 Render 回调的内容揭示成功后通知。双守卫排除旧版本、关闭、处置与重入失效。
- ReserveDismissal 在 Sticky Gate 内 FreezeEnd 后撤销当前会话；FinishDismissal 锁外完成。End 竞争取走同一冻结事件，确保原因不被旧 Present 抢写且只发布一次。
- 闸门测试强制 Reserve 返回、Finish 停住、实际旧 Present 抢先运行；不是重复串行 Dismiss→Present。

## 自动验证

- 主程序 Release build：退出码0，0 warning / 0 error；candidate-build.log。
- 无 GUI/无音频行为验证：108通过，0失败；candidate-interaction-tests.log。
- 正确既有 KeypadLayer 夹具完整 --skip-invoke 自检：退出码0、PASS；candidate-keypad-selftest.log。
- PluginSelfTest 原有17个段号标记集合保持不变。
- git diff --check：退出码0。LF→CRLF 提示不是编译 warning。
- 十项安全变异：generation、lease、barrier、capacity、host-lock、discard、dismiss-reservation、render-version、render-order、end-lock。均有运行时行为红态，finally逐字节恢复后正常候选重跑通过；不把编译失败算红态。
- 旧 mutation-dismiss.log 属于 R2 串行回归证据；R3 用 dismiss-reservation 的强制交错证明替代它，不能以旧日志声称并发安全。

## 独立复核

Standards 与 Spec 本轮只读源码复核均无发现，两项原阻断代码语义闭环。两位审查者各自独立no-build复跑均108通过、0失败、退出码0；未构建、未编辑、未运行GUI或音频。不是仅实施者自报。

## 未验证与边界

没有构造真实轮盘窗口、播放声音、执行真实插件动作或输入/窗口/硬件操作；没有运行 tests/ 弹窗套件。

生产 Render 纯接缝测试不证明显示器实际合成、DPI、多屏或手感，需用户实机验收。建议核对普通/粘滞轮盘的首次与连续呼出、快速关闭/替代、中心/主扇区/子扇区、返回/取消、DPI与多屏；既有音效行为应保持。

音效引擎、旧音效配置、设置UI及官方子模块源码尚未迁移；没有提交、推送、打包、Tag或Release。完整产品任务与人工门禁不得标完成。

当前源码与文档（含未跟踪文件）标识见 candidate-files.sha256。已有自检夹具前置条件纠正：Folder不能满足[3g]的keypadLayer/keyMap要求；原报告保留，不当作已证实产品缺陷。
