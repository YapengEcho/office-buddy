# 发布到 GitHub

项目已包含 README、`.gitignore`、MIT 许可证和 Windows 自动构建。推荐仓库名 `office-buddy`。首次公开版本为 v0.8.0。

## 1. 创建空仓库

在 GitHub 的新建仓库页面填写：

| 字段 | 填写 |
|---|---|
| 仓库名称 | `office-buddy` |
| 描述 | 小小桌面上班搭子：全局鼠标点击统计、像素宠物互动与工位装饰，适用于 Windows。 |
| 可见性 | 公共 / Public |
| 添加自述文件 | 关 |
| 添加 .gitignore | 无 |
| 添加许可证 | 无 |

后面三个保持关闭，是因为源码目录已经包含这些文件。点击创建仓库，复制页面中的 HTTPS 仓库地址。

## 2. 上传源码

使用 `package.ps1` 导出的 `office-buddy-github-v0.8.0` 目录（或解压 `office-buddy-v0.8.0-source.zip`），在文件夹内打开 PowerShell。不要将运行版的 EXE 或 Windows 个人存档当作源码上传。

逐行运行：

```powershell
git init -b main
git add .
git status --short
git commit -m "Release Office Buddy v0.8.0"
git remote add origin https://github.com/YapengEcho/office-buddy.git
git push -u origin main
```

仓库名称和账户不同就将 URL 替换成刚复制的地址。首次 push 按弹出的 GitHub 登录窗口授权。如果提示找不到 `git`，先安装 [Git for Windows](https://git-scm.com/download/win)，再重新打开 PowerShell。

如果 commit 提示 `Author identity unknown`，先设置当前仓库的提交身份：

```powershell
git config user.name "你的 GitHub 名称"
git config user.email "GitHub 设置页中复制的 noreply 邮箱"
git commit -m "Release Office Buddy v0.8.0"
```

邮箱可从 GitHub Settings → Emails 获取隐私邮箱。然后继续 remote 和 push 两条命令。遇到报错时先处理该条，成功后再继续下一条，不使用强制推送。

推送后刷新仓库页面，确认 README 图片显示正常，并查看 Actions 中 Windows build 的结果。自动检查尚需第一次推送后在 GitHub 上实际运行。

## 3. 发布给普通用户下载

源码上传成功后，打开仓库的 Releases → Draft a new release：

- Tag：创建 `v0.8.0`，Target 选 `main`。
- Title：`上班搭子 v0.8.0`。
- 描述：复制 `CHANGELOG.md` 中 v0.8.0 的条目，可添加 `64 位 Windows 10 / 11，解压后运行 OfficeBuddyStudio.exe`。
- 上传附件：`OfficeBuddyStudio-v0.8.0-windows-x64.zip`，最后点击 Publish release。

GitHub 自动生成的 Source code 下载只包含源码。将运行包作为附件上传，大家就能直接下载 EXE 使用。建议设置主题 `desktop-pet`、`windows`、`pixel-art`、`mouse-tracking`、`csharp`。

## 4. 后续更新

在已经上传的源码目录中修改并构建，验证通过后提交：

```powershell
git add .
git commit -m "Describe the change"
git push
```

发布新版本时同步 `application.manifest`、界面版本号、`package.ps1`、`RUNNING.md` 与 `CHANGELOG.md`，生成新运行包，再创建对应的新 Release。

参考：[GitHub 官方上传本地项目说明](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github)、[管理 Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)。
