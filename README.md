# DeskFence —— 极简 Windows 桌面收纳盒

> 一个只有「收纳盒」功能的桌面整理工具。单文件、免安装、无后台服务、无联网、无弹窗。
> A minimal, single-file desktop fence for Windows. No installer, no telemetry, no popups.

Windows 桌面一直没有像样的分类工具。Stardock Fences 收费，360 桌面助手 / 腾讯桌面整理这类国产工具会带一堆常驻服务和弹窗。DeskFence 只解决一件事：**在桌面上放几个半透明盒子，把快捷方式拖进去，桌面就干净了。**

## 下载即用

前往 [**Releases**](../../releases/latest) 下载 `DeskFence.exe`，双击即可，**无需安装、无需 .NET SDK**。

- **系统要求**：Windows 10 1809+ / Windows 11（依赖系统自带的 .NET Framework 4.8，Win10 1903 起已内置）
- exe 放在任何目录都能运行（配置写在 `%APPDATA%\DeskFence\`，不写程序目录）
- **首次运行提示「Windows 已保护你的电脑」**：这是未做代码签名的正常提示 —— 点「更多信息」→「仍要运行」即可
- 纯单文件，可用 [VirusTotal](https://www.virustotal.com/) 自行核验；也可以按下面的步骤自己编译

首次启动后，桌面左上角会出现一个空盒子，托盘区多一个图标。

## 特性

- **单文件绿色版**（约 62 KB），依赖仅系统自带的 .NET Framework 4.8
- **逐像素透明**的分层窗口：蒙版只有一层淡淡的暗灰/浅灰，图标和文字始终不透明，浓度调到 8% 也不糊
- **盒子就是文件夹**：拖进去的文件是真正被移动到 `%APPDATA%\DeskFence\boxes\<id>\`，卸载程序不影响文件，随时可在资源管理器里直接操作
- **拖动排序**：盒内拖动图标时，两个图标之间显示蓝色插入线；从资源管理器拖入也会按落点插入
- **磁吸对齐**：拖动/缩放时自动吸附屏幕四边和其他盒子的边缘
- **八向缩放**：任意边、任意角拉伸，尺寸自动记住
- **锁定**：位置和大小锁定后不可误拖，标题栏显示 🔒
- **外观可调**：蒙版颜色（暗灰/浅灰）、浓度（8%–100%）、图标大小（36/48/64/80 px，>56px 由外壳按尺寸渲染 256px 图标）
- **重命名**：右键首项，或悬停标题栏点铅笔
- **开机自启**：写当前用户 Run 项，不需要管理员权限
- 不写服务、不装驱动、不建计划任务、不联网

## 使用

1. 运行 `DeskFence.exe`，桌面出现一个空盒子
2. 把快捷方式 / 文件 / 文件夹拖进去
3. 双击图标用系统默认方式打开；右键图标可移回桌面、重命名、删除到回收站
4. 右键盒子空白处：新建盒子、一键收纳桌面全部快捷方式、排序、锁定、外观、设置
5. 退出：托盘图标右键 →「退出」

### 数据目录

```
%APPDATA%\DeskFence\
├─ config.json          盒子位置/大小/名称/锁定/外观/图标顺序
└─ boxes\<盒子ID>\       盒子里的文件本体
```

卸载：删掉 `DeskFence.exe` 即可；文件不受影响。想彻底还原，先右键「把内容移回桌面并删除此盒」，再删掉 `%APPDATA%\DeskFence`。

## 构建

需要 .NET Framework 4.8 和一个 Roslyn 编译器（VS 2017+ 或 Build Tools 均可，`build.cmd` 会用 `vswhere` 自动查找）。

```bat
git clone https://github.com/Stontar/DeskFence.git
cd DeskFence
build.cmd
```

产物为仓库根目录的 `DeskFence.exe`。推 `v*` 标签时 GitHub Actions 会自动编译并把 exe 挂到 Release 上。

## 已知限制

- 拖进去是**移动**，不是复制（想留原件请先复制；向外拖时按住 Ctrl 则是复制）
- 「此电脑」「回收站」等系统图标不是磁盘文件，无法收纳
- 多显示器下配置保存的是绝对坐标，换显示器布局后可能需要重新摆放
- 未做代码签名，SmartScreen 首次运行会提示

## License

MIT。与 Stardock Fences 无任何关联，名称仅表示用途类似。
