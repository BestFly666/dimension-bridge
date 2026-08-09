# 项目文件索引 — 次元译桥

> **最后更新**：2026-08-09
> **项目名称**：次元译桥（Dimension Bridge，代码工程 SimpleXmlEditor）
> **技术栈**：C# / .NET 8.0 / WPF / Newtonsoft.Json / Microsoft.Extensions.DI / xUnit
> **说明**：本索引是 code review 与快速定位的首选入口 —— 找功能实现、查文件职责、确认架构分层，先查这里。
>
> **目录约定**：`Windows/` 收纳全部窗口（xaml + code-behind），`Utils/` 收纳通用工具类，`Services/`、`Plugins/`、`Dictionary/`、`ViewModels/` 等按职责分层。所有服务面向接口（`Services/Interfaces.cs`）经 DI 注入。

---

## 目录

- [1. 项目配置与构建](#1-项目配置与构建)
- [2. 核心入口](#2-核心入口)
- [3. UI 层 — 窗口（Windows/）](#3-ui-层--窗口windows)
- [4. ViewModel 层](#4-viewmodel-层)
- [5. 服务层（Service）](#5-服务层service)
- [6. 插件系统](#6-插件系统)
- [7. 字典 / 规则 / 术语表](#7-字典--规则--术语表)
- [8. 专家配置](#8-专家配置)
- [9. 本地化](#9-本地化)
- [10. 工具类与命令](#10-工具类与命令)
- [11. 命令行工具（SimpleXmlEditor.Cli）](#11-命令行工具simplexmleditorcli)
- [12. 测试项目](#12-测试项目)
- [13. 数据文件](#13-数据文件)
- [14. 脚本（scripts/）](#14-脚本scripts)
- [15. CI/CD](#15-cicd)
- [16. 文档](#16-文档)

---

## 1. 项目配置与构建

| 文件 | 说明 | 路径 |
|------|------|------|
| **SimpleXmlEditor.sln** | 解决方案文件，包含主项目、Cli、测试项目 | [SimpleXmlEditor.sln](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.sln) |
| **SimpleXmlEditor.csproj** | 主项目工程文件（WPF, .NET 8.0） | [SimpleXmlEditor.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SimpleXmlEditor.csproj) |
| **SimpleXmlEditor.Cli.csproj** | 命令行工具工程文件（net8.0-windows, 无 WPF UI） | [SimpleXmlEditor.Cli.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Cli/SimpleXmlEditor.Cli.csproj) |
| **SimpleXmlEditor.Tests.csproj** | 测试项目工程文件（xUnit） | [SimpleXmlEditor.Tests.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/SimpleXmlEditor.Tests.csproj) |
| **.gitignore** | Git 忽略规则 | [.gitignore](file:///e:/translate/xml-ai-translator-main/.gitignore) |

---

## 2. 核心入口

| 文件 | 职责 | 路径 |
|------|------|------|
| **App.xaml** | WPF 应用程序资源定义、全局主题资源（蓝色调）、按钮/DataGrid 共享样式 | [App.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml) |
| **App.xaml.cs** | 应用程序入口、DI 容器初始化、服务注册（**注意：`TranslationOrchestrator` 不注册进 DI**，由 MainViewModel 自建带真实日志回调的实例，避免 DI no-op 回调吞掉内部日志） | [App.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml.cs) |

---

## 3. UI 层 — 窗口（Windows/）

每个窗口包含 `.xaml`（布局）和 `.xaml.cs`（代码后置）两个文件，统一存放于 `Windows/` 目录。**MainWindow 系列是纯前端 View 层**，业务逻辑在 Services/ 与 ViewModels/（架构边界，见 [HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md)）。

### 3.1 主窗口（partial class 拆分）

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainWindow.xaml** | 主界面布局：DataGrid、工具栏、筛选栏、状态栏 | [MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.xaml) |
| **MainWindow.xaml.cs** | 主窗口入口类（partial）：构造函数、InitializeComponent、事件挂接 | [MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.xaml.cs) |
| **MainWindow.Events.cs** | 通用 UI 事件（partial）：剪贴板、筛选、批替换、Undo、快捷键、日志、主题/菜单 | [MainWindow.Events.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Events.cs) |
| **MainWindow.Events.File.cs** | 文件域事件（partial）：加载/保存/快速保存/导出/退出/自动保存 | [MainWindow.Events.File.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Events.File.cs) |
| **MainWindow.Events.Translation.cs** | 翻译域事件（partial）：翻译选中/全部、暂停/停止、清缓存、批大小 | [MainWindow.Events.Translation.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Events.Translation.cs) |
| **MainWindow.Events.Tools.cs** | 工具域事件（partial）：评估/投票/预翻译/一致性/设置/统计/术语表/黑名单 | [MainWindow.Events.Tools.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Events.Tools.cs) |
| **MainWindow.Grid.cs** | DataGrid 核心（partial）：逻辑全选模型、行头整行选择、VisualTree 工具、批量添加 | [MainWindow.Grid.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.cs) |
| **MainWindow.Grid.Sorting.cs** | DataGrid 列头交互（partial）：列头自适应、列字母整列选择、Ctrl+A 全选 | [MainWindow.Grid.Sorting.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.Sorting.cs) |
| **MainWindow.Grid.ContextMenu.cs** | DataGrid 右键菜单（partial）：全选/反选、标记审校状态 | [MainWindow.Grid.ContextMenu.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.ContextMenu.cs) |
| **MainWindow.Grid.Editing.cs** | DataGrid 选中同步与行编辑（partial）：选择变化、复选联动、行高拖拽 | [MainWindow.Grid.Editing.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.Editing.cs) |
| **MainWindow.Handlers.cs** | ViewModel 事件订阅 + 结果渲染回调（partial）：评估/投票/预翻译/一致性结果展示 | [MainWindow.Handlers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Handlers.cs) |
| **MainWindow.FileOps.cs** | 文件操作与初始化（partial）：LoadXml/SaveXml、配置初始化、模型加载、窗口生命周期 | [MainWindow.FileOps.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.FileOps.cs) |
| **MainWindow.Helpers.cs** | UI 辅助方法（partial）：对话框、导出、状态更新 | [MainWindow.Helpers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Helpers.cs) |
| **MainWindow.Localization.cs** | 界面本地化逻辑（partial） | [MainWindow.Localization.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Localization.cs) |
| **MainWindow.Theme.cs** | 主题切换逻辑（partial） | [MainWindow.Theme.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Theme.cs) |

### 3.2 设置窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **SettingsWindow.xaml** | 设置界面布局：AI 提供商、API Key、翻译参数、评估/投票专用模型 | [SettingsWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/SettingsWindow.xaml) |
| **SettingsWindow.xaml.cs** | 设置窗口入口类（partial）：构造函数、字段、事件挂接 | [SettingsWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/SettingsWindow.xaml.cs) |
| **SettingsWindow.Save.cs** | 设置读写（partial）：LoadSettings/SaveSettings、API Key 加密存储（允许不填 Key 保存） | [SettingsWindow.Save.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/SettingsWindow.Save.cs) |
| **SettingsWindow.Models.cs** | 模型/提供商管理（partial）：模型列表加载、AI 提供商下拉、Refresh 模型 | [SettingsWindow.Models.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/SettingsWindow.Models.cs) |
| **SettingsWindow.Profiles.cs** | 专家 profile 管理（partial）：NewProfile/EditProfile、profile 增删改、列表填充 | [SettingsWindow.Profiles.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/SettingsWindow.Profiles.cs) |

### 3.3 术语表窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryWindow.xaml** | 术语表管理界面：术语对照表、筛选、编辑 | [GlossaryWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/GlossaryWindow.xaml) |
| **GlossaryWindow.xaml.cs** | 术语表窗口入口类（partial）：构造函数、字段、事件声明、本地化 | [GlossaryWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/GlossaryWindow.xaml.cs) |
| **GlossaryWindow.Filter.cs** | 数据刷新与筛选（partial）：RefreshAll、筛选联动、统计、防抖搜索 | [GlossaryWindow.Filter.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/GlossaryWindow.Filter.cs) |
| **GlossaryWindow.ImportExport.cs** | CSV/JSON 导入导出（partial）：导入/导出/分享、冲突报告导出 | [GlossaryWindow.ImportExport.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/GlossaryWindow.ImportExport.cs) |
| **GlossaryWindow.TermOps.cs** | 术语增删改查与工具（partial）：CRUD、专家 profile 合并、冲突检测触发、编辑对话框 | [GlossaryWindow.TermOps.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/GlossaryWindow.TermOps.cs) |

### 3.4 黑名单窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **BlacklistWindow.xaml** | 黑名单管理界面：Key 前缀 + 原文精确匹配两组规则 | [BlacklistWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/BlacklistWindow.xaml) |
| **BlacklistWindow.xaml.cs** | 黑名单规则增删、列表刷新（委托给 IBlacklistManager） | [BlacklistWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/BlacklistWindow.xaml.cs) |

### 3.5 评估窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **EvaluationWindow.xaml** | 翻译质量评估/多代理投票结果展示界面 | [EvaluationWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/EvaluationWindow.xaml) |
| **EvaluationWindow.xaml.cs** | 评估结果展示逻辑 | [EvaluationWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/EvaluationWindow.xaml.cs) |

### 3.6 投票候选确认窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **VotingReviewWindow.xaml** | 投票候选对比弹窗布局：列出 AI 建议改动的条目，每条显示原文、当前译文、候选译文（带评分）下拉选择 | [VotingReviewWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/VotingReviewWindow.xaml) |
| **VotingReviewWindow.xaml.cs** | 候选分组评分、默认选中 AI best、`GetSelections()` 返回用户选择（key → 译文） | [VotingReviewWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/VotingReviewWindow.xaml.cs) |

### 3.7 对话框

| 文件 | 职责 | 路径 |
|------|------|------|
| **InputDialog.xaml / .cs** | 通用输入对话框（如输入 API Key、名称等） | [InputDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/InputDialog.xaml) |
| **FileTypeDialog.xaml / .cs** | 文件类型选择对话框（选择要加载的 XML 格式） | [FileTypeDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/FileTypeDialog.xaml) |

---

## 4. ViewModel 层

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainViewModel.cs** | 核心业务状态协调器（partial 主文件）：Outcome 模型、字段、事件、Commands、构造函数、基础方法 | [MainViewModel.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.cs) |
| **MainViewModel.Properties.cs** | 全部绑定属性 + PropertyChanged/LogMessage 事件声明；`ActiveExpertProfileName` setter **同步 `_profileManager.ActiveProfileName` + SaveProfiles()**（双状态单向同步，保证 UI 选的专家在翻译执行路径生效） | [MainViewModel.Properties.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Properties.cs) |
| **MainViewModel.Undo.cs** | Undo 栈：PushUndoSnapshot / UndoLast | [MainViewModel.Undo.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Undo.cs) |
| **MainViewModel.Config.cs** | 配置读写：LoadConfig / SaveConfig / 缓存信息展示 | [MainViewModel.Config.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Config.cs) |
| **MainViewModel.Cache.cs** | 缓存/评分协调：SyncEntriesToCache、SyncScoresToCache、SaveScoreCache、RestoreScores、SaveCache | [MainViewModel.Cache.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Cache.cs) |
| **MainViewModel.EntryProcessing.cs** | 条目加载处理 + 术语表应用 + 黑名单刷新 + SaveXml | [MainViewModel.EntryProcessing.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.EntryProcessing.cs) |
| **MainViewModel.Translation.cs** | 翻译流水线：TranslateEntriesAsync、进度追踪、命令实现 | [MainViewModel.Translation.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Translation.cs) |
| **MainViewModel.Evaluation.cs** | AI 质量评估：EvaluateEntriesAsync / EvaluateEntry / 评估上下文 | [MainViewModel.Evaluation.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Evaluation.cs) |
| **MainViewModel.Voting.cs** | 多代理投票：VoteEntriesAsync / VoteEntry / ApplyVotingSelections | [MainViewModel.Voting.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Voting.cs) |
| **MainViewModel.Consistency.cs** | 一致性扫描 + 智能预翻译：ScanConsistencyIssues / SmartPreTranslate | [MainViewModel.Consistency.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Consistency.cs) |

---

## 5. 服务层（Service）

> 所有服务均面向接口编程，通过 DI 容器注入，接口定义见 `Interfaces.cs`。

### 5.1 接口定义

| 文件 | 职责 | 路径 |
|------|------|------|
| **Interfaces.cs** | 所有服务接口定义：`IAiTranslationService`、`IXmlRepository`、`IConfigService`、`IGlossaryManager`、`IExpertProfileManager`、`ITranslationEvaluator`、`IBlacklistManager`、`IFileFormatPlugin` | [Interfaces.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/Interfaces.cs) |

### 5.2 服务实现

> 大文件均以 partial class 按职责拆分，主文件保留类声明、字段、构造函数与核心入口。

| 文件 | 实现接口 | 职责 | 路径 |
|------|----------|------|------|
| **AiTranslationService.cs** | `IAiTranslationService` | AI 翻译核心（partial 主文件）：字段、构造函数、翻译入口（按提供商分派）、成本/限流统计、日志脱敏 | [AiTranslationService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.cs) |
| **AiTranslationService.Models.cs** | — | 模型管理（partial）：静态模型表、OpenAI 兼容厂商动态拉取模型列表（GET /models）、限流估算 | [AiTranslationService.Models.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.Models.cs) |
| **AiTranslationService.Providers.cs** | — | 提供商请求构建（partial）：Gemini / OpenAI 兼容请求发送、鉴权头设置 | [AiTranslationService.Providers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.Providers.cs) |
| **ConfigService.cs** | `IConfigService` | 配置管理（partial 主文件）：API Key 加密存储（Windows DPAPI）、LoadConfig/SaveConfig、评估专用模型配置 | [ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs) |
| **ConfigService.Cache.cs** | — | 翻译缓存（partial）：translation_cache.json / translation_progress.json 读写（2026-08-05 起统一存于 AppData）、进度恢复、GetCacheKey、SyncEntriesToCache | [ConfigService.Cache.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.Cache.cs) |
| **ConfigService.Scores.cs** | — | 评分缓存（partial）：score_cache.json 读写、SyncScoresToCache、ClearScoreCache | [ConfigService.Scores.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.Scores.cs) |
| **XmlRepository.cs** | `IXmlRepository` | XML 文件读写（partial 主文件）：安全解析（禁用 DTD/外部实体，防 XXE）、格式嗅探（LocalisationData/Excel）、SaveXml | [XmlRepository.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.cs) |
| **XmlRepository.Models.cs** | — | 数据模型（partial）：`LocalizationEntry`（含评估分数/改进建议）、`XmlFormat`/`ReviewStatus` 枚举 | [XmlRepository.Models.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.Models.cs) |
| **TranslationEvaluator.cs** | `ITranslationEvaluator` | 翻译质量评估 + 多代理投票（partial 主文件）：评估专用模型实例管理、EvaluateAsync/VoteAsync/批量入口 | [TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs) |
| **TranslationEvaluator.Prompts.cs** | — | 评估/投票提示词构建（partial） | [TranslationEvaluator.Prompts.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.Prompts.cs) |
| **TranslationEvaluator.Parsing.cs** | — | 评估/投票响应解析（partial）：JSON 提取、正则回退（code fence 清理复用 `AiResponseParser.StripCodeFence`） | [TranslationEvaluator.Parsing.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.Parsing.cs) |
| **TranslationEvaluator.Utils.cs** | — | 结果聚合工具（partial）：投票结果分组均分、多模型合并、Chunk | [TranslationEvaluator.Utils.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.Utils.cs) |
| **TranslationOrchestrator.cs** | — | 翻译编排器：协调翻译流程各步骤（分批、术语注入、**专家 Context 注入**——`BuildPrompt` 先 `BuildGlossaryContext` → `BuildExpertContext(glossary)` 并入专家块 → `{EXPERT_CONTEXT}` 替换或追加、`{GLOSSARY}` 兼容替换、进度；响应解析委托给 AiResponseParser） | [TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) |
| **AiResponseParser.cs** | — | 统一 AI 响应解析工具（static）：code fence 清理 `StripCodeFence`、标准 translations JSON 提取、三级回退策略（JSON 片段 → 正则 `N. "译文"` → 逐行解析）；供 TranslationOrchestrator / TranslationEvaluator 复用 | [AiResponseParser.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiResponseParser.cs) |
| **ReviewExporter.cs** | — | 审校导出：审查状态 CSV、术语冲突/一致性检测结果 CSV 导出（`ConsistencyIssue` 数据模型） | [ReviewExporter.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ReviewExporter.cs) |
| **PluginLoader.cs** | — | 文件格式插件加载器 | [PluginLoader.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/PluginLoader.cs) |

---

## 6. 插件系统

| 文件 | 职责 | 路径 |
|------|------|------|
| **AndroidStringsPlugin.cs** | Android `strings.xml` 格式解析/导出 | [AndroidStringsPlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/AndroidStringsPlugin.cs) |
| **JsonI18nPlugin.cs** | JSON i18n 格式解析/导出 | [JsonI18nPlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/JsonI18nPlugin.cs) |
| **PoFilePlugin.cs** | Gettext `.po` 格式解析/导出 | [PoFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/PoFilePlugin.cs) |
| **TxtFilePlugin.cs** | 通用键值对 `.txt` 解析/导出（KEY=value、UTF-8/GBK 自动识别 + 原编码写回） | [TxtFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/TxtFilePlugin.cs) |
| **TextEncodingDetector.cs** | 共享文本编码检测（UTF-8 BOM → 严格 UTF-8 → GBK），CSV/INI/PROPERTIES/TXT 统一复用 | [TextEncodingDetector.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/TextEncodingDetector.cs) |
| **CsvFilePlugin.cs** | CSV 解析/导出：列结构自动识别（3 列 Key/Original/Translation 或 2 列）、引号转义、GBK 兼容 | [CsvFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/CsvFilePlugin.cs) |
| **IniFilePlugin.cs** | INI 解析/导出：`[Section]` 段 + key=value，带段 Key 存 `[Section]key` 并还原段结构 | [IniFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/IniFilePlugin.cs) |
| **YamlFilePlugin.cs** | YAML 解析/导出（YamlDotNet）：嵌套字典点分展平、数组 `[i]` 展开 | [YamlFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/YamlFilePlugin.cs) |
| **ResxFilePlugin.cs** | RESX 解析/导出：data name/value 读写，输出标准 resheader | [ResxFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/ResxFilePlugin.cs) |
| **PropertiesFilePlugin.cs** | Java Properties 解析/导出：`\\ \n \t \uXXXX` 转义、续行、注释 | [PropertiesFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/PropertiesFilePlugin.cs) |

---

## 7. 字典 / 规则 / 术语表

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryManager.cs** | 术语表管理（partial 主文件）：字段、构造函数、`GlossaryTerm` 模型、Count、`MAX_GLOSSARY_CONTEXT_TERMS = 200`（单批提示词术语注入容量上限，50 → 200）；`translation_dictionary.json`/`glossary_terms.json` 从**运行目录**加载（csproj 已配 `CopyToOutputDirectory`） | [GlossaryManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.cs) |
| **GlossaryManager.Persistence.cs** | 持久化与导入导出（partial）：Load/Save、CSV/JSON 导入导出、MergeFromProfile | [GlossaryManager.Persistence.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Persistence.cs) |
| **GlossaryManager.Index.cs** | 倒排索引与搜索（partial）：索引构建、术语匹配、上下文注入、分类/状态筛选；**批量术语注入算法**——先全局验证收集命中术语 → 每条 entry 至少贡献自己的术语（最长优先）→ 剩余名额按命中条目数降序补充（防长术语挤掉短术语）；`IsTermRelated` 宽松相关判定（首核心词 + 类修饰词 / 按序命中半数核心词，仅用于 AI 提示注入） | [GlossaryManager.Index.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Index.cs) |
| **GlossaryManager.Conflict.cs** | 冲突检测（partial）：`DetectConflicts`（支持进度回调）+ `GlossaryConflict` 模型 | [GlossaryManager.Conflict.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Conflict.cs) |
| **GlossaryManager.Crud.cs** | 术语增删改（partial）：SetEntry/SetTerm/RemoveEntry/Clear | [GlossaryManager.Crud.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Crud.cs) |
| **BlacklistManager.cs** | 黑名单规则管理：Key 前缀 + 原文精确匹配、持久化 blacklist.json（兼容旧版数组格式）、线程安全 | [BlacklistManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/BlacklistManager.cs) |
| **CsvHelper.cs** | CSV 文件读写工具 | [CsvHelper.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/CsvHelper.cs) |

---

## 8. 专家配置

| 文件 | 职责 | 路径 |
|------|------|------|
| **ExpertProfile.cs** | 专家配置数据模型：Name/Description/Context/Glossary；`BuildExpertContextBlock(targetLanguage, glossary = "")` 构建专家知识块（Context 中 `{LANGUAGE}` 占位符替换，**并入批量匹配到的术语**，术语指导随专家一同进入 API） | [ExpertProfile.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfile.cs) |
| **ExpertProfileManager.cs** | 专家配置管理：增删改查、持久化；`ProfilesFile` 在 **`AppContext.BaseDirectory`（bin 运行目录）** 的 expert_profiles.json；`EnsureDefaultsExist` 注册默认三档案（星球大战 EaW 汉化 / 漫威 / 校对）；`ActiveProfileName` 与 MainViewModel 双状态同步 | [ExpertProfileManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfileManager.cs) |

---

## 9. 本地化

| 文件 | 职责 | 路径 |
|------|------|------|
| **LocalizationManager.cs** | 程序界面本地化管理（partial 主文件，**UI 文案禁止硬编码，必须走这里**）：GetString、语言切换 | [LocalizationManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Localization/LocalizationManager.cs) |
| **LocalizationManager.Dicts.En.cs** | 英文界面文案字典（静态数据，partial） | [LocalizationManager.Dicts.En.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Localization/LocalizationManager.Dicts.En.cs) |
| **LocalizationManager.Dicts.Zh.cs** | 中文界面文案字典（静态数据，partial） | [LocalizationManager.Dicts.Zh.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Localization/LocalizationManager.Dicts.Zh.cs) |

---

## 10. 工具类与命令

| 文件 | 职责 | 路径 |
|------|------|------|
| **StringExtensions.cs** | 字符串扩展方法：`HasChineseChars()` | [StringExtensions.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Utils/StringExtensions.cs) |
| **PromptTemplates.cs** | AI 提示词模板：翻译规则、格式要求（保证合法 JSON 输出）、术语替换规则 | [PromptTemplates.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Utils/PromptTemplates.cs) |
| **RelayCommand.cs** | MVVM 命令基类（Commands 目录） | [RelayCommand.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Commands/RelayCommand.cs) |

---

## 11. 命令行工具（SimpleXmlEditor.Cli）

| 文件 | 职责 | 路径 |
|------|------|------|
| **Program.cs** | 命令行翻译工具：`translate` / `batch` / `export-tmx` / `validate` / `help` 五个命令，复用主项目服务（ConfigService/GlossaryManager/AiTranslationService/XmlRepository 等） | [Program.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Cli/Program.cs) |
| **SimpleXmlEditor.Cli.csproj** | 工程配置（net8.0-windows、无 WPF UI、引用主项目） | [SimpleXmlEditor.Cli.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Cli/SimpleXmlEditor.Cli.csproj) |

---

## 12. 测试项目

> 使用 xUnit + Moq，**58 个测试用例全部通过**（2026-08-07 核对）。

| 文件 | 测试数 | 测试内容 | 路径 |
|------|--------|----------|------|
| **BlacklistManagerTests.cs** | 13 | 黑名单：前缀匹配、原文精确匹配、去重、持久化、旧格式兼容 | [BlacklistManagerTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/BlacklistManagerTests.cs) |
| **GlossaryManagerTests.cs** | 15 | 术语表 CRUD、CSV 读写、宽松相关判定（Xyston/Quasar/Skipray 省略变体） | [GlossaryManagerTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/GlossaryManagerTests.cs) |
| **FileFormatPluginsTests.cs** | 10 | 五格式插件：CSV 列识别/引号/GBK、INI 段往返、YAML 嵌套、RESX、PROPERTIES 转义、编码检测 | [FileFormatPluginsTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/FileFormatPluginsTests.cs) |
| **TxtFilePluginTests.cs** | 7 | 键值对 TXT：分隔符、注释、GBK 编码、保存编码保持 | [TxtFilePluginTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/TxtFilePluginTests.cs) |
| **ConfigServiceTests.cs** | 4 | 配置读写、加密存储 | [ConfigServiceTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/ConfigServiceTests.cs) |
| **StringExtensionsTests.cs** | 4 | `HasChineseChars()` 边界条件、`GetCacheKey()` 空值处理 | [StringExtensionsTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/StringExtensionsTests.cs) |
| **XmlRepositoryTests.cs** | 3 | XML 格式嗅探：LocalisationData / Excel / 未知格式 | [XmlRepositoryTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/XmlRepositoryTests.cs) |
| **TranslationOrchestratorTests.cs** | 2 | 翻译编排：批次切分、动态批大小 | [TranslationOrchestratorTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/TranslationOrchestratorTests.cs) |

---

## 13. 数据文件

> 程序运行时在 AppData 目录生成（非仓库内提交），`config.json` 与缓存文件存于用户 AppData。

| 文件 | 用途 | 说明 |
|------|------|------|
| **config.json** | 用户配置：AI 提供商、API Key（加密）、批次大小、评估专用模型 | AppData 目录 |
| **translation_cache.json** | 翻译缓存（原文 → 译文），大幅降低 API 调用成本 | AppData 目录 |
| **translation_progress.json** | 崩溃恢复临时文件（翻译中断时增量保存；QuickSave/翻译完成后删除） | AppData 目录（2026-08-05 起与主缓存统一，原在 bin 程序目录） |
| **score_cache.json** | 评估分数缓存（按条目 Key 关联：分数 + 改进建议） | AppData 目录 |
| **glossary_terms.json** | 术语对照表持久化文件 | **运行目录**（bin，csproj `CopyToOutputDirectory` 自动复制；保存时写回运行目录） |
| **translation_dictionary.json** | 翻译对照词典（原文 → 译文，术语注入候选源） | **运行目录**（bin，csproj `CopyToOutputDirectory` 自动复制） |
| **expert_profiles.json** | 专家配置持久化文件（含 `ActiveProfileName`） | **bin 运行目录**（`AppContext.BaseDirectory`） |
| **blacklist.json** | 黑名单规则持久化文件（`{"Prefixes":[...], "ExactOriginals":[...]}`） | AppData 目录 |

---

## 14. 脚本（scripts/）

> 游戏本地化辅助 Python 脚本，**存放于仓库外**的 `e:\translate\scripts\`（不在 `xml-ai-translator-main` 项目内），按版本前缀区分（`3.5_*` / `4.0_*` / `5.0_*`）与 `通用_*`（跨版本工具）。

| 文件 | 用途 | 路径 |
|------|------|------|
| **datlib.py** | EAW 引擎 DAT 文件读写库：`read_dat` / `write_dat` / `to_xml` / `xml_to_dat` 命令行（UTF-16LE + CRC32 索引，兼容重制版与 THR Alamo 格式） | [datlib.py](file:///e:/translate/scripts/datlib.py) |
| **3.5_添加换行写入DAT.py** | 3.5 引擎翻译：34 字符硬截断换行 + 79 字符空格填充后写回 DAT；`EXCLUDE_KEYWORDS` 含 `GC_COMPLETE_DISC_ONEPLANET`（固定宽度文本不换行） | [3.5_添加换行写入DAT.py](file:///e:/translate/scripts/3.5_添加换行写入DAT.py) |
| **4.0_添加换行写入DAT.py** | 4.0 引擎翻译：34 字符硬截断换行 + 79 字符空格填充后写回 DAT | [4.0_添加换行写入DAT.py](file:///e:/translate/scripts/4.0_添加换行写入DAT.py) |
| **5.0_添加换行写入DAT.py** | 5.0 引擎翻译：加换行空格后写回 DAT（方案与 4.0 一致） | [5.0_添加换行写入DAT.py](file:///e:/translate/scripts/5.0_添加换行写入DAT.py) |
| **4.0_字体加粗对齐.py** | 4.0 字体加粗对齐：按 5.0 英文版字重分布，对 4.0 相同控件块加粗/恢复普通（`EmpireAtWar-Bold`/`Arial Bold`/`Arial Black`） | [4.0_字体加粗对齐.py](file:///e:/translate/scripts/4.0_字体加粗对齐.py) |
| **通用_XML翻译写入DAT.py** | 跨版本通用：XML 翻译批量写入 DAT（按 Key 替换） | [通用_XML翻译写入DAT.py](file:///e:/translate/scripts/通用_XML翻译写入DAT.py) |
| **通用_合并翻译写入DAT.py** | 跨版本通用：合并多份翻译后写入 DAT | [通用_合并翻译写入DAT.py](file:///e:/translate/scripts/通用_合并翻译写入DAT.py) |
| **通用_添加换行空格_v2.py** | 跨版本通用：中文文本加换行空格（宽度感知） | [通用_添加换行空格_v2.py](file:///e:/translate/scripts/通用_添加换行空格_v2.py) |
| **通用_构建术语表.py / 通用_翻译校对.py / 通用_版本对比.py / 通用_对比manifest.py / 通用_查找缺失标签.py / 通用_检查XML错误.py / 通用_清理缓存污染.py** | 术语表构建、翻译校对、版本对比、manifest 对比、缺失标签查找、XML 错误检查、缓存污染清理 | `e:\translate\scripts\通用_*.py` |

---

## 15. CI/CD

| 文件 | 说明 | 路径 |
|------|------|------|
| **ci.yml** | CI 工作流：push/PR 到 main/master 时 restore → build → test → publish (win-x64 self-contained) → upload artifact | [ci.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/ci.yml) |
| **release.yml** | 发布工作流：push `v*` 标签时构建 + 打包 zip + 创建 GitHub Release（prerelease） | [release.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/release.yml) |

---

## 16. 文档

| 文件 | 用途 | 读者 | 路径 |
|------|------|------|------|
| **README.md** | 项目简介、快速上手、支持的模型与功能一览 | 所有用户 | [README.md](file:///e:/translate/xml-ai-translator-main/README.md) |
| **PRODUCT_PLAN.md** | 完整产品规划（PRD：问题陈述/用户画像与故事/方案概述/状态复盘 + 路线图 Now/Next/Later + 发布计划 + GTM + Non-Goals 与版本历史） | 产品/项目管理 | [PRODUCT_PLAN.md](file:///e:/translate/xml-ai-translator-main/PRODUCT_PLAN.md) |
| **DEVELOPMENT_LOG.md** | 开发日志（按日期记录） | 开发者 | [DEVELOPMENT_LOG.md](file:///e:/translate/xml-ai-translator-main/DEVELOPMENT_LOG.md) |
| **HANDOVER.md** | 项目交接文档（架构、构建、问题排查、已知问题） | 新接手的开发者 | [HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md) |
| **PROJECT_INDEX.md** | 项目文件索引（本文件）—— code review 与快速定位首选入口 | 开发者/协作者 | [PROJECT_INDEX.md](file:///e:/translate/xml-ai-translator-main/PROJECT_INDEX.md) |

---

## 架构总览

```
xml-ai-translator-main/
├── SimpleXmlEditor.sln                          ← 解决方案（主项目 + Cli + 测试）
├── .gitignore
├── .github/workflows/
│   ├── ci.yml                                   ← CI（push/PR → build+test+publish）
│   └── release.yml                              ← 发布（v* tag → Release + zip）
│
├── SimpleXmlEditor/                             ← 主项目
│   ├── App.xaml / App.xaml.cs                   ← 入口 + DI + 主题资源
│   ├── Windows/                                 ← UI 窗口层（纯 View）
│   │   ├── MainWindow.xaml                      ← 主界面布局
│   │   ├── MainWindow.xaml.cs                   ← 主窗口 partial 入口
│   │   ├── MainWindow.Events.cs                 ← 事件处理
│   │   ├── MainWindow.Grid.cs                   ← DataGrid 交互
│   │   ├── MainWindow.Helpers.cs                ← UI 辅助
│   │   ├── MainWindow.Localization.cs           ← 界面本地化
│   │   ├── MainWindow.Theme.cs                  ← 主题切换
│   │   ├── GlossaryWindow.xaml / .cs            ← 术语表
│   │   ├── BlacklistWindow.xaml / .cs           ← 黑名单管理
│   │   ├── SettingsWindow.xaml / .cs            ← 设置
│   │   ├── EvaluationWindow.xaml / .cs          ← 评估/投票展示
│   │   ├── VotingReviewWindow.xaml / .cs        ← 投票候选对比（人工确认）
│   │   ├── InputDialog.xaml / .cs               ← 输入对话框
│   │   └── FileTypeDialog.xaml / .cs            ← 文件类型选择
│   ├── Commands/RelayCommand.cs                 ← MVVM 命令基类
│   ├── ViewModels/MainViewModel.cs              ← MVVM 核心状态
│   ├── Services/                                ← 服务层（面向接口）
│   │   ├── Interfaces.cs                        ← 所有接口
│   │   ├── AiTranslationService.cs              ← AI 翻译（动态模型列表）
│   │   ├── ConfigService.cs                     ← 配置/加密/缓存
│   │   ├── XmlRepository.cs                     ← XML 安全读写（格式嗅探）
│   │   ├── TranslationEvaluator.cs              ← 质量评估/投票
│   │   ├── TranslationOrchestrator.cs           ← 翻译编排
│   │   ├── AiResponseParser.cs                  ← 统一 AI 响应解析（code fence + 回退）
│   │   ├── ReviewExporter.cs                    ← 审校/冲突/一致性导出
│   │   └── PluginLoader.cs                      ← 插件加载
│   ├── Plugins/                                 ← 文件格式插件
│   │   ├── AndroidStringsPlugin.cs              ← Android strings.xml
│   │   ├── JsonI18nPlugin.cs                    ← JSON i18n
│   │   ├── PoFilePlugin.cs                      ← Gettext PO
│   │   ├── TxtFilePlugin.cs                     ← 通用键值对 TXT
│   │   ├── TextEncodingDetector.cs              ← 共享编码检测（UTF-8/GBK）
│   │   ├── CsvFilePlugin.cs                     ← CSV（列自动识别）
│   │   ├── IniFilePlugin.cs                     ← INI（[Section] 段）
│   │   ├── YamlFilePlugin.cs                    ← YAML（YamlDotNet）
│   │   ├── ResxFilePlugin.cs                    ← RESX（.NET 资源）
│   │   └── PropertiesFilePlugin.cs              ← Java Properties
│   ├── Dictionary/                              ← 字典 / 规则 / 术语
│   │   ├── GlossaryManager.cs                   ← 术语表逻辑
│   │   ├── BlacklistManager.cs                  ← 黑名单规则
│   │   └── CsvHelper.cs                         ← CSV 工具
│   ├── ExpertProfiles/
│   │   ├── ExpertProfile.cs                     ← 数据模型 + BuildExpertContextBlock（并入术语）
│   │   └── ExpertProfileManager.cs              ← 管理逻辑（默认三档案：星球大战/漫威/校对）
│   ├── Localization/LocalizationManager.cs      ← 本地化（禁止硬编码文案）
│   ├── Themes/DarkColors.xaml、LightColors.xaml ← 主题色板（运行时切换）
│   └── Utils/
│       ├── StringExtensions.cs                  ← 字符串工具
│       └── PromptTemplates.cs                   ← AI 提示词
│
├── SimpleXmlEditor.Cli/                         ← 命令行翻译工具
│   ├── Program.cs                               ← translate/batch/export-tmx/validate
│   └── SimpleXmlEditor.Cli.csproj
│
├── SimpleXmlEditor.Tests/                       ← 测试项目 (58/58 ✅)
│   ├── BlacklistManagerTests.cs                 ← 黑名单（13）
│   ├── GlossaryManagerTests.cs                  ← 术语表 + 宽松相关判定（15）
│   ├── FileFormatPluginsTests.cs                ← 五格式插件（10）
│   ├── TxtFilePluginTests.cs                    ← 键值对 TXT（7）
│   ├── ConfigServiceTests.cs                    ← 配置/缓存（4）
│   ├── StringExtensionsTests.cs                 ← 字符串工具（4）
│   ├── XmlRepositoryTests.cs                    ← XML 嗅探（3）
│   └── TranslationOrchestratorTests.cs          ← 翻译编排/动态批大小（2）
│
├── scripts/（实际位于仓库外 e:\translate\scripts\）← 游戏本地化辅助脚本
│   ├── datlib.py                                ← EAW DAT 读写库（UTF-16LE + CRC32）
│   ├── 3.5_添加换行写入DAT.py                   ← 3.5 换行写回 DAT（含 GC_COMPLETE_DISC_ONEPLANET 排除）
│   ├── 4.0_添加换行写入DAT.py                   ← 4.0 换行写回 DAT
│   ├── 5.0_添加换行写入DAT.py                   ← 5.0 换行写回 DAT
│   ├── 4.0_字体加粗对齐.py                      ← 按 5.0 字重对齐 4.0 字体
│   └── 通用_*.py                                ← 跨版本工具（翻译写入/校对/版本对比等）
│
└── docs (README, PRODUCT_PLAN, DEVELOPMENT_LOG, HANDOVER, PROJECT_INDEX)
```
