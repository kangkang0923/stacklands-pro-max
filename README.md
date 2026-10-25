# Stacklands Pro Max

Stacklands 的玩法增强 mod：自带磁铁的磁力系列容器、高级生产建筑，以及一批体验向功能。已发布 Steam 创意工坊：[创意工坊条目](https://steamcommunity.com/sharedfiles/filedetails/?id=3813989104)。

> 当前状态见 `docs/MEMORY.md`，此处不复述。
> 通用 mod 开发内容与反编译产物在私有仓库 `../stacklands-mod-common/`（本地并排放置）；JSON 的 `$schema` 指向它，仅开发期校验用，不影响构建与发布。

## 快速开始

```powershell
# 编译 + 部署（先完全退出游戏）
powershell -ExecutionPolicy Bypass -File .\build.ps1
# 离线自检（不上游戏）
powershell -ExecutionPolicy Bypass -File .\verify.ps1
```

游戏内：主菜单 → Mods → 本 Mod（41 项设置，改完立即生效）；`K`+`O` 再按 `F1` 开调试菜单。

## 文档

| 文档 | 适合谁 |
|---|---|
| `AGENTS.md` | AI / 新接手者：命令、硬约束、坑、导航 |
| `docs/REQUIREMENTS.md` | 想确认「做了什么、怎么算做完」 |
| `docs/DESIGN.md` | 要改代码：文件地图、补丁清单、每张卡与每个功能的实现契约 |
| `docs/PLAN.md` | 下一步做什么 |
| `docs/MEMORY.md` | 接手：现在到哪一步、本机环境 |
| `docs/DECISIONS.md` | 为什么这么定、被否方案 |
| `docs/创意工坊发布指南.md` | 发布/更新流程与商店页文案 |
| `CHANGELOG.md` | 版本变化 |

## 目录结构

```
Mod.cs ModConfig.cs ModSettings.cs   Mod 入口、41 项设置、统一参数
Patches/                            75 个补丁类（一个功能一个文件）
Cards/ Blueprints/                  内容 JSON（10 张卡）
Icons/ Sounds/ localization.tsv     资源与本地化
build.ps1 verify.ps1 rename-mod.ps1 构建、离线校验、改名
docs/                               需求、设计、计划、状态、决策、卡牌与发布指南
```

详细结构见 `docs/DESIGN.md`。

## 官方与参考资料

- 模组 Wiki：<https://modding.stacklands.co/en/latest/>
- 模板仓库：<https://github.com/stacklandsdev/mod-template>
- 官方 Discord（提问最快）：<https://discord.gg/j3FjwZVyWh>
- 上手参考 mod（官方 BugFixes）：<https://github.com/benediktwerner/Stacklands-BugFixes-Mod>
- 游戏内调试：创意工坊订阅 UnityExplorer（id `2966585489`）；读原版实现用 ILSpy 打开 `Stacklands_Data\Managed\GameScripts.dll`
