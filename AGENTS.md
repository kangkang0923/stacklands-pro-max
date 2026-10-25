项目类型：程序类（mod 工程，含内容目录 `Cards/`、`Blueprints/`、`Boosterpacks/`、`Icons/`、`Sounds/`）
# Stacklands Pro Max · Agent 入口

## 这是什么

Stacklands 的玩法增强 mod：磁力系列容器（磁力箱 / 硬币磁力箱 / 贝壳磁力箱 / 磁力信用卡 / 磁力容器）+ 高级仓库 / 高级温室 / 魔法收集器 / 高级集市 / 高级回收中心 + 一批体验向功能。已发布 Steam 创意工坊（item id `3813989104`）。

## 命令

```powershell
# 编译 + 部署到游戏 Mods 目录（改代码后必跑；⚠️ 先完全退出游戏）
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 只同步 Cards/Icons 等资源，不重新编译
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipBuild

# 离线自检：本地化 term / JSON 引用 / _字段 / 设置项 term（退出码 0 = 通过）
powershell -ExecutionPolicy Bypass -File .\verify.ps1

# 发布版编译（脚本自动按配置取产物路径）
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release

# 改 Mod ID / 名称（同时改所有引用位置）
powershell -ExecutionPolicy Bypass -File .\rename-mod.ps1 -NewId <Id> -NewName "<Name>"

# 一次性设置：之后可直接 .\build.ps1（当前是 Restricted）
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

验证方式与进游戏回归清单见 `docs/DESIGN.md` 的「验证方式」。

## 硬约束（改代码前必读）

- 改代码后必须跑 `build.ps1`；只 `dotnet build` 时游戏加载的仍是旧 DLL（表现为"改了没生效"）。
- 编译/部署前完全退出游戏，否则 DLL 被锁。
- `workshop.txt` 必须入库：它决定工坊"更新已有条目"还是"新建重复条目"，丢了会拆掉订阅者。
- 发版要抬 `manifest.json` 的 `version`，并写 `CHANGELOG.md`。
- 反编译产物在私有仓库 `../stacklands-mod-common/decompiled/`，只读参考，不进本仓库、不进 Mods 目录、不公开分享。
- `.ps1` 保持纯 ASCII：Windows PowerShell 5.1 按系统 ANSI（中文系统 GBK）解析无 BOM 脚本，写中文会语法错误。
- 一个 mod 只能有一个 `Mod` 子类；新增补丁类必须登记到 `Mod.PatchClasses`（`verify.ps1` 会校验数量与注册）。
- 有计时器/状态条的卡必须重写 `CanHaveCardsWhileHasStatus()` 返回 `true`，否则材料吸过来会在卡顶弹跳进不去。
- mod 蓝图在 Cities 场地不触发；只有被 `CitiesCardSupport.MarkAsCitiesCard` 标记的卡（磁力容器 / 高级回收中心 / 磁力信用卡）例外。
- 卡总开关只影响"新局还能不能得到"：创意摘除、图鉴隐藏、配方失效；旧存档里已存在的卡照旧可用。

## 非直觉的坑

- **卡片外观/图标/配色不要只靠 JSON `icon`**：mod 卡加载早于原版卡，`Icon`/`MyPalette` 可能为 null，需在运行时从原版预制体拷（`ApplyVanilla*Look`），配色还要抄 `IsBuilding`（决定建筑粉 vs 结构深灰）。
- **摘叠用 `SetParent(null)`，不要 `RemoveFromStack()`**：后者会 `SetParent(null)+SetChild(null)`，把上面叠的卡全部弹开。
- **读键必须走 `InputController`**：`UnityEngine.Input` 在本游戏被剥离。
- **已被认领的卡别再动**：`BounceTarget != null`（磁力/管道/牌堆已认领）时不要再弹射或 `SendIt`，否则卡会飞出去又掉头。
- **`CanBeDragged()` 不能作为每帧筛选**：`BeingDragged` 时为 false，正在拖的卡会被自己从选区删掉。
- 调试菜单：进游戏按住 `K`+`O` 再按 `F1`；`F3` 打开日志查看器；日志在 `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Player.log`。

## 文档导航

| 文件 | 内容 | 状态 |
|---|---|---|
| `docs/REQUIREMENTS.md` | 要什么、验收标准 | ✅ 已确认（2026-10-25） |
| `docs/DESIGN.md` | 实现契约、不变量、验证方式 | ✅ |
| `docs/PLAN.md` | 待办与下一步 | ✅ |
| `docs/MEMORY.md` | 当前状态、交接提示、本机环境 | ✅ |
| `docs/DECISIONS.md` | 为什么这么定（含被否方案） | ✅ |
| `docs/创意工坊发布指南.md` | 发布清单、商店页文案 | ✅ |
| `CHANGELOG.md` | 版本变化（0.1.0 ~ 0.6.7） | ✅ |

## 当前状态

见 `docs/MEMORY.md`；下一步见 `docs/PLAN.md`，此处不复述。
