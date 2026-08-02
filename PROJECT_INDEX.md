# 项目文件索引 — XML AI Translator

> **最后更新**：2026-08-02
> **项目名称**：SimpleXmlEditor（XML AI Translator）
> **技术栈**：C# / .NET 8.0 / WPF / Newtonsoft.Json / Microsoft.Extensions.DI / xUnit
> **说明**：本索引是 code review 与快速定位的首选入口 —— 找功能实现、查文件职责、确认架构分层，先查这里。

---

## 目录

- [1. 项目配置与构建](#1-项目配置与构建)
- [2. 核心入口](#2-核心入口)
- [3. UI 层 — 窗口](#3-ui-层--窗口)
- [4. ViewModel 层](#4-viewmodel-层)
- [5. 服务层（Service）](#5-服务层service)
- [6. 插件系统](#6-插件系统)
- [7. 字典 / 术语表](#7-字典--术语表)
- [8. 专家配置](#8-专家配置)
- [9. 本地化](#9-本地化)
- [10. 工具类与命令](#10-工具类与命令)
- [11. 测试项目](#11-测试项目)
- [12. 数据文件](#12-数据文件)
- [13. CI/CD](#13-cicd)
- [14. 文档](#14-文档)

---

## 1. 项目配置与构建

| 文件 | 说明 | 路径 |
|------|------|------|
| **SimpleXmlEditor.sln** | 解决方案文件，包含主项目和测试项目 | [SimpleXmlEditor.sln](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.sln) |
| **SimpleXmlEditor.csproj** | 主项目工程文件（WPF, .NET 8.0） | [SimpleXmlEditor.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SimpleXmlEditor.csproj) |
| **SimpleXmlEditor.Tests.csproj** | 测试项目工程文件（xUnit） | [SimpleXmlEditor.Tests.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/SimpleXmlEditor.Tests.csproj) |
| **.gitignore** | Git 忽略规则 | [.gitignore](file:///e:/translate/xml-ai-translator-main/.gitignore) |

---

## 2. 核心入口

| 文件 | 职责 | 路径 |
|------|------|------|
| **App.xaml** | WPF 应用程序资源定义、全局主题资源（蓝色调） | [App.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml) |
| **App.xaml.cs** | 应用程序入口、DI 容器初始化、服务注册 | [App.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml.cs) |

---

## 3. UI 层 — 窗口

每个窗口包含 `.xaml`（布局）和 `.xaml.cs`（代码后置）两个文件。**MainWindow.xaml.cs 是纯前端 View 层**，业务逻辑在 Services/ 与 ViewModels/（架构边界，见 [HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md)）。

### 3.1 主窗口（partial class 拆分）

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainWindow.xaml** | 主界面布局：DataGrid、工具栏、筛选栏、状态栏 | [MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml) |
| **MainWindow.xaml.cs** | 主窗口入口类（partial），事件挂接 | [MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml.cs) |
| **MainWindow.Events.cs** | 事件处理逻辑（partial） | [MainWindow.Events.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Events.cs) |
| **MainWindow.Grid.cs** | DataGrid 交互逻辑（partial）：选中、排序、右键菜单 | [MainWindow.Grid.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Grid.cs) |
| **MainWindow.Helpers.cs** | UI 辅助方法（partial）：对话框、导出、状态更新 | [MainWindow.Helpers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Helpers.cs) |
| **MainWindow.Localization.cs** | 界面本地化逻辑（partial） | [MainWindow.Localization.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Localization.cs) |
| **MainWindow.Theme.cs** | 主题切换逻辑（partial） | [MainWindow.Theme.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Theme.cs) |

### 3.2 设置窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **SettingsWindow.xaml** | 设置界面布局：AI 提供商、API Key、翻译参数、评估/投票专用模型 | [SettingsWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SettingsWindow.xaml) |
| **SettingsWindow.xaml.cs** | 设置读写、API Key 加密存储逻辑（允许不填 Key 保存） | [SettingsWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SettingsWindow.xaml.cs) |

### 3.3 术语表窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryWindow.xaml** | 术语表管理界面：术语对照表、筛选、编辑 | [GlossaryWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml) |
| **GlossaryWindow.xaml.cs** | 术语表增删改查、CSV 导入导出、筛选逻辑 | [GlossaryWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml.cs) |

### 3.4 评估窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **EvaluationWindow.xaml** | 翻译质量评估/多代理投票结果展示界面 | [EvaluationWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/EvaluationWindow.xaml) |
| **EvaluationWindow.xaml.cs** | 评估结果展示逻辑 | [EvaluationWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/EvaluationWindow.xaml.cs) |

### 3.5 投票候选确认窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **VotingReviewWindow.xaml** | 投票候选对比弹窗布局：列出 AI 建议改动的条目，每条显示原文、当前译文、候选译文（带评分）下拉选择 | [VotingReviewWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/VotingReviewWindow.xaml) |
| **VotingReviewWindow.xaml.cs** | 候选分组评分、默认选中 AI best、`GetSelections()` 返回用户选择（key → 译文） | [VotingReviewWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/VotingReviewWindow.xaml.cs) |

### 3.6 对话框

| 文件 | 职责 | 路径 |
|------|------|------|
| **InputDialog.xaml / .cs** | 通用输入对话框（如输入 API Key、名称等） | [InputDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/InputDialog.xaml) |
| **FileTypeDialog.xaml / .cs** | 文件类型选择对话框（选择要加载的 XML 格式） | [FileTypeDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/FileTypeDialog.xaml) |

---

## 4. ViewModel 层

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainViewModel.cs** | 核心业务状态管理：条目列表、翻译流程、缓存/评分同步、排序筛选、评估投票、Undo 栈 | [MainViewModel.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.cs) |

---

## 5. 服务层（Service）

> 所有服务均面向接口编程，通过 DI 容器注入，接口定义见 `Interfaces.cs`。

### 5.1 接口定义

| 文件 | 职责 | 路径 |
|------|------|------|
| **Interfaces.cs** | 所有服务接口定义：`IAiTranslationService`、`IXmlRepository`、`IConfigService`、`IGlossaryManager`、`IExpertProfileManager`、`ITranslationEvaluator` | [Interfaces.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/Interfaces.cs) |

### 5.2 服务实现

| 文件 | 实现接口 | 职责 | 路径 |
|------|----------|------|------|
| **AiTranslationService.cs** | `IAiTranslationService` | AI 翻译核心：8 个 AI 提供商调用、批量翻译、速率限制、响应解析（JSON 回退策略）、OpenAI 兼容厂商动态拉取模型列表（GET /models） | [AiTranslationService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.cs) |
| **ConfigService.cs** | `IConfigService` | 配置管理：API Key 加密存储（Windows DPAPI）、翻译缓存 `translation_cache.json`、评分缓存 `score_cache.json`、评估专用模型配置 | [ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs) |
| **XmlRepository.cs** | `IXmlRepository` | XML 文件读写：安全的 XML 解析（禁用 DTD/外部实体，防 XXE）、条目模型 `LocalizationEntry`（含评估分数/改进建议） | [XmlRepository.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.cs) |
| **TranslationEvaluator.cs** | `ITranslationEvaluator` | 翻译质量评估（0-10 评分 + 改进建议）、多代理投票，支持评估专用模型 | [TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs) |
| **TranslationOrchestrator.cs** | — | 翻译编排器：协调翻译流程各步骤（分批、术语注入、进度） | [TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) |
| **ReviewExporter.cs** | — | 审校导出：审查状态 CSV、术语冲突/一致性检测结果 CSV 导出（`ConsistencyIssue` 数据模型） | [ReviewExporter.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ReviewExporter.cs) |
| **PluginLoader.cs** | — | 文件格式插件加载器 | [PluginLoader.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/PluginLoader.cs) |

---

## 6. 插件系统

| 文件 | 职责 | 路径 |
|------|------|------|
| **AndroidStringsPlugin.cs** | Android `strings.xml` 格式解析/导出 | [AndroidStringsPlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/AndroidStringsPlugin.cs) |
| **JsonI18nPlugin.cs** | JSON i18n 格式解析/导出 | [JsonI18nPlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/JsonI18nPlugin.cs) |
| **PoFilePlugin.cs** | Gettext `.po` 格式解析/导出 | [PoFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/PoFilePlugin.cs) |

---

## 7. 字典 / 术语表

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryManager.cs** | 术语表管理：术语增删改查、CSV/JSON 导入导出、冲突检测（`DetectConflicts` 支持进度回调）、倒排索引 | [GlossaryManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.cs) |
| **CsvHelper.cs** | CSV 文件读写工具 | [CsvHelper.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/CsvHelper.cs) |

---

## 8. 专家配置

| 文件 | 职责 | 路径 |
|------|------|------|
| **ExpertProfile.cs** | 专家配置数据模型 | [ExpertProfile.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfile.cs) |
| **ExpertProfileManager.cs** | 专家配置管理：增删改查、持久化 | [ExpertProfileManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfileManager.cs) |

---

## 9. 本地化

| 文件 | 职责 | 路径 |
|------|------|------|
| **LocalizationManager.cs** | 程序界面本地化管理（**UI 文案禁止硬编码，必须走这里**） | [LocalizationManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Localization/LocalizationManager.cs) |

---

## 10. 工具类与命令

| 文件 | 职责 | 路径 |
|------|------|------|
| **StringExtensions.cs** | 字符串扩展方法：`HasChineseChars()`、`GetCacheKey()` 等 | [StringExtensions.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/StringExtensions.cs) |
| **PromptTemplates.cs** | AI 提示词模板：翻译规则、格式要求（保证合法 JSON 输出）、术语替换规则 | [PromptTemplates.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/PromptTemplates.cs) |
| **RelayCommand.cs** | MVVM 命令基类（Commands 目录） | [RelayCommand.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Commands/RelayCommand.cs) |

---

## 11. 测试项目

> 使用 xUnit + Moq，13 个测试用例全部通过。

| 文件 | 测试数 | 测试内容 | 路径 |
|------|--------|----------|------|
| **ConfigServiceTests.cs** | 4 | 配置读写、加密存储 | [ConfigServiceTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/ConfigServiceTests.cs) |
| **StringExtensionsTests.cs** | 4 | `HasChineseChars()` 边界条件、`GetCacheKey()` 空值处理 | [StringExtensionsTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/StringExtensionsTests.cs) |
| **GlossaryManagerTests.cs** | 5 | 术语表 CRUD、CSV 读写 | [GlossaryManagerTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/GlossaryManagerTests.cs) |

---

## 12. 数据文件

> 程序运行时在 AppData 目录生成（非仓库内提交），`config.json` 与缓存文件存于用户 AppData。

| 文件 | 用途 | 说明 |
|------|------|------|
| **config.json** | 用户配置：AI 提供商、API Key（加密）、批次大小、评估专用模型 | AppData 目录 |
| **translation_cache.json** | 翻译缓存（原文 → 译文），大幅降低 API 调用成本 | AppData 目录 |
| **score_cache.json** | 评估分数缓存（按条目 Key 关联：分数 + 改进建议） | AppData 目录 |
| **glossary_terms.json** | 术语对照表持久化文件 | AppData 目录 |
| **expert_profiles.json** | 专家配置持久化文件 | AppData 目录 |

---

## 13. CI/CD

| 文件 | 说明 | 路径 |
|------|------|------|
| **ci.yml** | CI 工作流：push/PR 到 main/master 时 restore → build → test → publish (win-x64 self-contained) → upload artifact | [ci.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/ci.yml) |
| **release.yml** | 发布工作流：push `v*` 标签时构建 + 打包 zip + 创建 GitHub Release（prerelease） | [release.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/release.yml) |

---

## 14. 文档

| 文件 | 用途 | 读者 | 路径 |
|------|------|------|------|
| **README.md** | 项目简介、快速上手、支持的模型与功能一览 | 所有用户 | [README.md](file:///e:/translate/xml-ai-translator-main/README.md) |
| **PRODUCT_PLAN.md** | 产品路线图与决策记录（含 Non-Goals 与版本历史） | 产品/项目管理 | [PRODUCT_PLAN.md](file:///e:/translate/xml-ai-translator-main/PRODUCT_PLAN.md) |
| **DEVELOPMENT_LOG.md** | 开发日志（按日期记录） | 开发者 | [DEVELOPMENT_LOG.md](file:///e:/translate/xml-ai-translator-main/DEVELOPMENT_LOG.md) |
| **HANDOVER.md** | 项目交接文档（架构、构建、问题排查、已知问题） | 新接手的开发者 | [HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md) |
| **PROJECT_INDEX.md** | 项目文件索引（本文件）—— code review 与快速定位首选入口 | 开发者/协作者 | [PROJECT_INDEX.md](file:///e:/translate/xml-ai-translator-main/PROJECT_INDEX.md) |

---

## 架构总览

```
xml-ai-translator-main/
├── SimpleXmlEditor.sln                          ← 解决方案
├── .gitignore
├── .github/workflows/
│   ├── ci.yml                                   ← CI（push/PR → build+test+publish）
│   └── release.yml                              ← 发布（v* tag → Release + zip）
│
├── SimpleXmlEditor/                             ← 主项目
│   ├── App.xaml / App.xaml.cs                   ← 入口 + DI + 主题资源
│   ├── MainWindow.xaml                          ← 主界面布局
│   ├── MainWindow.xaml.cs                       ← 主窗口 partial 入口
│   ├── MainWindow.Events.cs                     ← 事件处理
│   ├── MainWindow.Grid.cs                       ← DataGrid 交互
│   ├── MainWindow.Helpers.cs                    ← UI 辅助
│   ├── MainWindow.Localization.cs               ← 界面本地化
│   ├── MainWindow.Theme.cs                      ← 主题切换
│   ├── GlossaryWindow.xaml / .cs                ← 术语表
│   ├── SettingsWindow.xaml / .cs                ← 设置
│   ├── EvaluationWindow.xaml / .cs              ← 评估/投票展示
│   ├── VotingReviewWindow.xaml / .cs            ← 投票候选对比（人工确认）
│   ├── InputDialog.xaml / .cs                   ← 输入对话框
│   ├── FileTypeDialog.xaml / .cs                ← 文件类型选择
│   ├── Commands/RelayCommand.cs                 ← MVVM 命令基类
│   ├── ViewModels/MainViewModel.cs              ← MVVM 核心状态
│   ├── Services/
│   │   ├── Interfaces.cs                        ← 所有接口
│   │   ├── AiTranslationService.cs              ← AI 翻译（动态模型列表）
│   │   ├── ConfigService.cs                     ← 配置/加密/缓存
│   │   ├── XmlRepository.cs                     ← XML 安全读写
│   │   ├── TranslationEvaluator.cs              ← 质量评估/投票
│   │   ├── TranslationOrchestrator.cs           ← 翻译编排
│   │   ├── ReviewExporter.cs                    ← 审校/冲突/一致性导出
│   │   └── PluginLoader.cs                      ← 插件加载
│   ├── Plugins/
│   │   ├── AndroidStringsPlugin.cs              ← Android strings.xml
│   │   ├── JsonI18nPlugin.cs                    ← JSON i18n
│   │   └── PoFilePlugin.cs                      ← Gettext PO
│   ├── Dictionary/
│   │   ├── GlossaryManager.cs                   ← 术语表逻辑
│   │   └── CsvHelper.cs                         ← CSV 工具
│   ├── ExpertProfiles/
│   │   ├── ExpertProfile.cs                     ← 数据模型
│   │   └── ExpertProfileManager.cs              ← 管理逻辑
│   ├── Localization/LocalizationManager.cs      ← 本地化（禁止硬编码文案）
│   ├── StringExtensions.cs                      ← 字符串工具
│   └── PromptTemplates.cs                       ← AI 提示词
│
├── SimpleXmlEditor.Tests/                       ← 测试项目 (13/13 ✅)
│   ├── ConfigServiceTests.cs
│   ├── StringExtensionsTests.cs
│   └── GlossaryManagerTests.cs
│
└── docs (README, PRODUCT_PLAN, DEVELOPMENT_LOG, HANDOVER, PROJECT_INDEX)
```
