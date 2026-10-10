# 参与改进

欢迎提交 Issue 和 Pull Request。描述问题时附上 Windows 版本、显示缩放比例、角色款式、复现步骤和期望结果。截图请遮住其他软件中的个人信息。

项目使用 .NET Framework C# 编译器，不使用 NuGet。先运行 `build.ps1` 和 `verify.ps1`；修改窗口或动效时，在有桌面的 Windows 会话中运行 `verify.ps1 -Overlay`，并逐一检查三款角色。运行前从托盘正常退出搭子，避免验证与日常实例争用输入。

图像回归检查需要 Python 与 Pillow：

```powershell
foreach ($theme in 'pixel','cream','midnight') {
    Start-Process .\OfficeBuddyStudio.exe -ArgumentList '--motion-frames',"work/motion-$theme",$theme -Wait
    py .\check-motion.py "work/motion-$theme"
    if ($LASTEXITCODE -ne 0) { throw "图像检查失败：$theme" }
}
```

改动应保留已有 `save.json` 的兼容性，不能收集键盘、窗口标题或点击坐标。生成素材的来源与提示词记录在 `ASSETS.md`。请使用可随项目分发的原创素材。

发布包由 `package.ps1` 的文件清单导出，不包含个人存档、旧截图、Git 内部文件或临时检查数据。
