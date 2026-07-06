# JTLP 截屏录屏工具

## 功能

| 快捷键 | 功能 |
|--------|------|
| Alt+1 | 全屏截图 |
| Alt+2 | 开始录屏 |
| Alt+3 | 停止录屏 |

- 系统托盘常驻，左键打开设置，右键弹出菜单
- 录屏支持自动分段（默认10分钟）+ 停止后自动合并
- 支持录制分辨率、压缩质量、开机自启动等设置
- 单实例校验、热键冲突检测

## 技术栈

- C# .NET 8 WinForms
- FFmpeg（录屏依赖，需单独下载）
- 独立发布（self-contained，无需安装 .NET 运行时）

## 项目结构

```
src/JTLP/
├── Program.cs          # 主窗体 + 托盘 + 截图/录屏 + 热键
├── AppSettings.cs      # 配置模型（JSON 持久化）
├── ScreenRecorder.cs   # FFmpeg 录屏器（自动分段 + 合并）
├── SettingsForm.cs     # 设置窗口
└── JTLP.csproj         # 项目文件
```

## 快速开始

1. 下载 FFmpeg 并放到 `tools/ffmpeg/ffmpeg.exe`
2. `dotnet build` 或 `dotnet publish` 编译
3. 运行 `JTLP.exe`

## 发布

运行 `publish.bat` 自动打包。
