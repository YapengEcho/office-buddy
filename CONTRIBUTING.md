# 参与改进

欢迎提交 Issue 和 Pull Request。描述问题时附上 Windows 版本、显示缩放比例、角色款式、复现步骤和期望结果。截图请遮住其他软件中的个人信息。

项目使用 .NET Framework C# 编译器，不使用 NuGet。先运行 `build.ps1` 和 `verify.ps1`；修改窗口或动效时，在有桌面的 Windows 会话中运行 `verify.ps1 -Overlay`，覆盖 `pixel`、`cream`、`midnight`、`nailong` 四款角色。运行前从托盘正常退出搭子，避免验证与日常实例争用输入。

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `OfficeBuddy.cs` | 窗口、Raw Input、计数、存档、渲染与命令行入口 |
| `PixelEye.cs` / `SpriteMotion.cs` | 三款像素角色的瞳孔、眨眼、独立手掌与袖口动画 |
| `PixelDecor.cs` | 商店与桌宠共用的五件像素装饰 |
| `NailongPet.cs` | 奶龙状态、连续大笑、左右独立抬手与图集渲染 |
| `NailongVideo.cs` | 使用独立数据生成演示视频帧 |
| `WorkFeatures.cs` / `BuddyFeatures.cs` / `SettingsForm.cs` | 位置、设置、快捷键、陪伴状态、提醒、统计与导出 |
| `FeatureChecks.cs` / `verify.ps1` / `check-motion.py` | 功能、原生窗口与像素角色图像检查 |
| `assets/` / `application.manifest` | 嵌入素材与 Per-Monitor V2 DPI 声明 |
| `build.ps1` / `package.ps1` | 构建和清单打包 |

## 验证

`verify.ps1` 的核心检查使用独立数据，覆盖旧存档迁移、奖励、位置恢复、提醒与勿扰、CSV 和恢复备份，以及奶龙动画连续性、单侧手部区域、脚底和透明背景。`-Overlay` 检查实际窗口的点击穿透、悬停命中、界面往返、外观切换、计时器推进和文字独立缩放。

像素角色的图像回归检查需要 Python 与 Pillow；运行和构建搭子本身不需要它们。检查四方向瞳孔、固定眼眶、眨眼、左右手独立运动、双手同时运动、休息闭眼与透明背景：

```powershell
foreach ($theme in 'pixel','cream','midnight') {
    Start-Process .\OfficeBuddyStudio.exe -ArgumentList '--motion-frames',"work/motion-$theme",$theme -WindowStyle Hidden -Wait
    py .\check-motion.py "work/motion-$theme"
    if ($LASTEXITCODE -ne 0) { throw "图像检查失败：$theme" }
}
```

奶龙的姿势和连续动作帧可以单独导出供目视检查；这两个入口使用演示数据，不读取真实存档。连续动画的数值与像素区域检查已包含在 `verify.ps1` 核心检查中。

```powershell
Start-Process .\OfficeBuddyStudio.exe -ArgumentList '--nailong-poses','work/nailong-poses' -WindowStyle Hidden -Wait
Start-Process .\OfficeBuddyStudio.exe -ArgumentList '--nailong-motion-frames','work/nailong-motion' -WindowStyle Hidden -Wait
```

演示视频生成方法见 [NAILONG-NOTES.md](NAILONG-NOTES.md)。窗口检查需要本地 Windows 桌面会话；GitHub Actions 运行构建、核心检查和打包。

## 数据与素材

改动应保留已有 `save.json` 的兼容性，不能收集键盘、窗口标题或点击坐标。生成素材的来源与提示词记录在 [ASSETS.md](ASSETS.md) 和 [NAILONG-NOTES.md](NAILONG-NOTES.md)。请使用可随项目分发的原创素材，并说明素材权利。

发布包由 `package.ps1` 的文件清单导出，不包含个人存档、旧截图、Git 内部文件或临时检查数据。
