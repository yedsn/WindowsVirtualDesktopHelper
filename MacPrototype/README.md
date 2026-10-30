# macOS 技术验证版

这是 Windows Virtual Desktop Helper 的独立 macOS 技术验证应用，用来验证：

- 从 macOS 原生 Space 读取桌面、当前桌面和窗口归属。
- 在菜单栏同时显示每个桌面的编号，点击图标直接切换桌面。
- 用当前桌面的高亮状态跟随系统切换。
- 在一个窗口中查看所有原生桌面及其应用窗口。
- 将一个已打开窗口移动到另一个桌面。
- 跳转到一个其他桌面的窗口并尝试激活它。

它不会影响 `Source/` 下的 Windows 应用，也不会自动移动任何窗口。

## 构建与运行

```bash
cd MacPrototype
./run.sh
```

开发时也可以只构建：

```bash
./build.sh
```

构建产物为 `MacPrototype/.build/WVDHMacPrototype.app`。首次使用“跳转并激活”时需要在系统设置中授予辅助功能权限。

测试“所有窗口”界面时可以直接打开：

```bash
./run.sh --show-overview
```

## 已使用的接口

桌面切换和窗口归属依赖隔离在 `SkyLightService.swift` 中的 SkyLight 私有接口。窗口列表使用 CoreGraphics，当前可见窗口的激活使用 Accessibility API。

已在 macOS 26.6.2（Apple M2）上验证。接口包含私有 API，不能作为 Mac App Store 沙盒应用分发，也必须针对新的 macOS 版本重新验证。

## 本版边界

- 目标桌面必须已经存在；不会创建或删除原生 Space。
- 显示在所有桌面的窗口只列出一次，并禁止移动。
- 其他桌面的窗口可以枚举，但辅助功能通常只能在切换到该桌面后执行激活。
- 多显示器、桌面重排、注销重启、全屏 Space 和默认 SIP 环境仍需单独验证。
- 这是技术验证版，不包含 Windows 版的规则快照、清理锁定和一键清理移植。
