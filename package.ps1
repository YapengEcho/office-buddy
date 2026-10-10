param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'work\packages'))
$ErrorActionPreference='Stop'
$taskVersion='0.8.0'
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
$taskSource=Join-Path $taskOutput ('office-buddy-github-v'+$taskVersion)
$taskPortable=Join-Path $taskOutput ('OfficeBuddyStudio-v'+$taskVersion+'-windows-x64')
foreach($taskZipName in @(('office-buddy-v'+$taskVersion+'-source.zip'),('OfficeBuddyStudio-v'+$taskVersion+'-windows-x64.zip'))) {
    if(Test-Path -LiteralPath (Join-Path $taskOutput $taskZipName)){throw '同版本 ZIP 已存在，请使用新的 -OutputDirectory。'}
}
$taskFiles=@('OfficeBuddy.cs','SpriteMotion.cs','PixelEye.cs','PixelDecor.cs','application.manifest',
    'build.ps1','verify.ps1','check-motion.py','package.ps1','.gitignore','.gitattributes','LICENSE',
    'README.md','RUNNING.md','ASSETS.md','DESIGN.md','CONTRIBUTING.md','CHANGELOG.md',
    '像素搭子.cmd','奶油搭子.cmd','夜猫搭子.cmd','统一眼睛与手.gif',
    'v080-catalog.png','v080-quiet.png','v080-compact.png','v080-overview.png','v080-shop.png','v080-style.png',
    '.github\workflows\build.yml',
    'assets\mint-cat.png','assets\mint-cat-rig.png','assets\mint-cat-water.png',
    'assets\apricot-rabbit.png','assets\apricot-rabbit-rig.png','assets\moon-cat.png','assets\moon-cat-rig.png')
$taskRuntimeFiles=@('OfficeBuddyStudio.exe','LICENSE','RUNNING.md','CHANGELOG.md','像素搭子.cmd','奶油搭子.cmd','夜猫搭子.cmd')
function Copy-Allowlist([string]$target,[string[]]$files) {
    # Refuse foreign/stale files instead of silently including or deleting them.
    if(Test-Path -LiteralPath $target) {
        $taskKnown=@($files|ForEach-Object{[IO.Path]::GetFullPath((Join-Path $target $_))})
        foreach($taskExisting in Get-ChildItem -LiteralPath $target -Recurse -File -Force) {
            if($taskExisting.FullName -notin $taskKnown){throw ('导出目录含清单外文件，请改用新的输出目录：'+$target)}
        }
    }
    foreach($taskRelative in $files) {
        $taskOriginal=Join-Path $PSScriptRoot $taskRelative
        if(-not (Test-Path -LiteralPath $taskOriginal -PathType Leaf)){throw ('缺少文件：'+$taskRelative)}
        $taskDestination=Join-Path $target $taskRelative
        New-Item -ItemType Directory -Force -Path (Split-Path $taskDestination -Parent)|Out-Null
        Copy-Item -LiteralPath $taskOriginal -Destination $taskDestination -Force
    }
}
Copy-Allowlist $taskSource $taskFiles
Copy-Allowlist $taskPortable $taskRuntimeFiles
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-Package([string]$folder,[string]$zip) {
    $taskArchive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach($taskFile in Get-ChildItem -LiteralPath $folder -Recurse -File -Force) {
            $taskRelative=$taskFile.FullName.Substring($folder.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskFile.FullName,$taskRelative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null
        }
    }finally{$taskArchive.Dispose()}
}
# Versioned archives are never silently overwritten.
Write-Package $taskSource (Join-Path $taskOutput ('office-buddy-v'+$taskVersion+'-source.zip'))
Write-Package $taskPortable (Join-Path $taskOutput ('OfficeBuddyStudio-v'+$taskVersion+'-windows-x64.zip'))
Write-Output ('源码目录：'+$taskSource)
Write-Output ('发布附件：'+(Join-Path $taskOutput ('OfficeBuddyStudio-v'+$taskVersion+'-windows-x64.zip')))
