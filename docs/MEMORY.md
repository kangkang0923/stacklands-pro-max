# 当前状态

> 覆盖式：只反映现在。历史看 git 或 `CHANGELOG.md`。最后更新：2026-10-25

## 做到哪了

- 版本 **0.6.7**（未验收、未上传）：在 0.6.6 三卡之上加了 ① **时间倍数沿用**——月结（换月亮 / 下一回合）与换地图后保持原速度，含与「切换地点后自动暂停」的配合（`Patches\GameSpeedKeepPatch.cs`）；② **取物优先落空地**——资源箱 / 城市资源容器 / 食物容器取出的卡逐个候选落点避开已有的卡（`Patches\ChestEject.cs` + `Patches\FoodWarehouseEjectPatch.cs`，磁力箱点击取出的兜底也一起改了）。M3 城市场地三卡（磁力容器 / 高级回收中心 / 磁力信用卡城市版）已由用户验收通过（2026-10-22「功能正常了」）；下一步见 `PLAN.md`。
- 文档体系 2026-10-25 重构完成：`AGENTS.md` + `docs/{REQUIREMENTS,DESIGN,PLAN,MEMORY,DECISIONS}` + `CHANGELOG.md`；旧文档（功能说明 / 开发与交接说明 / 环境准备 / docs 决策单篇）已提炼后删除；磁力系列卡牌已并入 `docs/DESIGN.md` 后删除；发布指南移入 `docs/` 保留。
- 仓库结构：`stacklands-pro-max`（本仓库）、`stacklands-diablo3`、`stacklands-mod-common`（私有，含通用文档 + 反编译产物），三者并排放置。

## 交接提示

- **改完代码只跑 `dotnet build` 等于没改**：游戏加载的是 Mods 目录里的 DLL，必须 `build.ps1` 部署（0.5.9 因此误判过"补丁无效"）。
- 只改 JSON / 本地化 / 图标时用 `build.ps1 -SkipBuild` 秒同步。
- 补丁类总数 79，`Mod.PatchClasses` 必须与 `Patches/*.cs` 数量一致（`verify.ps1` 校验）。
- 设置界面共 41 项（8 卡总开关 + 20 功能开关 + 12 数值输入框 + 1 快捷键文本框），改完立即生效，配置存 `Mods\StacklandsProMax\config.json`，删掉即恢复默认全开。
- 反编译产物在 `../stacklands-mod-common/decompiled/`（私有仓库）；本地反编译可能滞后于实际游戏 DLL，写补丁前用 ILSpy 对实际 DLL 核一遍。本机重反编译现成命令：`ilspycmd -o <输出目录> -p "<游戏>\Stacklands_Data\Managed\GameScripts.dll"`（2026-10-25 核过，同一份用于 0.6.7 的三个新补丁）。
- **临时草稿统一放 `../stacklands-mod-common/Temp.md`**（本仓库不再保留 Temp.md，已在 `.gitignore` 中，2026-10-25 起）。

## 环境与部署

- 游戏：`D:\SteamLibrary\steamapps\common\Stacklands`（含 Cities_DLC、Spirits_DLC），`Stacklands_Data\Managed\` 有 129 个 dll。
- .NET SDK 6.0.428（模板目标 `netstandard2.1`，SDK 6 实测 0 警告 0 错误，不必装 7）。
- Windows PowerShell 5.1，执行策略 Restricted，跑脚本加 `-ExecutionPolicy Bypass`。
- 部署目录：`%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\StacklandsProMax\`。
- 日志：`%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Player.log`；游戏内 `F3` 查看器，`K`+`O` 再 `F1` 开调试菜单。
- 创意工坊 item id `3813989104`，存在 `workshop.txt`（已入库）。
