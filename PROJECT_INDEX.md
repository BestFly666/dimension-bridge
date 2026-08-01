# 项目文件索引 — XML AI Translator

> **最后更新**：2026-07-30
> **项目名称**：SimpleXmlEditor（XML AI Translator）
> **技术栈**：C# / .NET 8.0 / WPF / Newtonsoft.Json / Microsoft.Extensions.DI / xUnit

---

## 目录

- [1. 项目配置与构建](#1-项目配置与构建)
- [2. 核心入口](#2-核心入口)
- [3. UI 层 — 窗口](#3-ui-层--窗口)
- [4. ViewModel 层](#4-viewmodel-层)
- [5. 服务层（Service）](#5-服务层service)
- [6. 字典 / 术语表](#6-字典--术语表)
- [7. 专家配置](#7-专家配置)
- [8. 本地化](#8-本地化)
- [9. 工具类](#9-工具类)
- [10. 测试项目](#10-测试项目)
- [11. 数据文件](#11-数据文件)
- [12. CI/CD](#12-cicd)
- [13. 文档](#13-文档)

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

| 文件 | 行数 | 职责 | 路径 |
|------|------|------|------|
| **App.xaml** | — | WPF 应用程序资源定义 | [App.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml) |
| **App.xaml.cs** | — | 应用程序入口、DI 容器初始化、服务注册 | [App.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml.cs) |

---

## 3. UI 层 — 窗口

每个窗口包含 `.xaml`（布局）和 `.xaml.cs`（代码后置）两个文件。

### 3.1 主窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainWindow.xaml** | 主界面布局：DataGrid、工具栏、筛选栏、状态栏 | [MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml) |
| **MainWindow.xaml.cs** | 主窗口事件处理、用户交互逻辑 | [MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml.cs) |

### 3.2 设置窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **SettingsWindow.xaml** | 设置界面布局：AI 提供商、API Key、翻译参数 | [SettingsWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SettingsWindow.xaml) |
| **SettingsWindow.xaml.cs** | 设置读写、API Key 加密存储逻辑 | [SettingsWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SettingsWindow.xaml.cs) |

### 3.3 术语表窗口

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryWindow.xaml** | 术语表管理界面：术语对照表、筛选、编辑 | [GlossaryWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml) |
| **GlossaryWindow.xaml.cs** | 术语表增删改查、CSV 导入导出、筛选逻辑 | [GlossaryWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml.cs) |

### 3.4 对话框

| 文件 | 职责 | 路径 |
|------|------|------|
| **InputDialog.xaml / .cs** | 通用输入对话框（如输入 API Key、名称等） | [InputDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/InputDialog.xaml) |
| **FileTypeDialog.xaml / .cs** | 文件类型选择对话框（选择要加载的 XML 格式） | [FileTypeDialog](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/FileTypeDialog.xaml) |

---

## 4. ViewModel 层

| 文件 | 职责 | 路径 |
|------|------|------|
| **MainViewModel.cs** | 核心业务状态管理：条目列表、翻译流程、缓存同步、排序、筛选 | [MainViewModel.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.cs) |

---

## 5. 服务层（Service）

> 所有服务均面向接口编程，通过 DI 容器注入。

### 5.1 接口定义

| 文件 | 职责 | 路径 |
|------|------|------|
| **Interfaces.cs** | 所有服务接口定义：`IAiTranslationService`、`IXmlRepository`、`IConfigService`、`IGlossaryManager`、`IExpertProfileManager`、`ITranslationEvaluator` | [Interfaces.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/Interfaces.cs) |

### 5.2 服务实现

| 文件 | 实现接口 | 职责 | 路径 |
|------|----------|------|------|
| **AiTranslationService.cs** | `IAiTranslationService` | AI 翻译核心：8 个 AI 提供商调用、批量翻译、速率限制、缓存读写、响应解析（含 JSON 回退策略） | [AiTranslationService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.cs) |
| **ConfigService.cs** | `IConfigService` | 配置管理：API Key 加密存储（Windows DPAPI）、用户偏好持久化 | [ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs) |
| **XmlRepository.cs** | `IXmlRepository` | XML 文件读写：安全的 XML 解析（禁用 DTD/外部实体）、条目导入导出 | [XmlRepository.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.cs) |
| **TranslationEvaluator.cs** | `ITranslationEvaluator` | 翻译质量评估（预留，Phase 3 扩展） | [TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs) |
| **TranslationOrchestrator.cs** | — | 翻译编排器：协调翻译流程各步骤 | [TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) |

---

## 6. 字典 / 术语表

| 文件 | 职责 | 路径 |
|------|------|------|
| **GlossaryManager.cs** | 术语表管理：术语增删改查、CSV 导入导出、翻译时术语替换（`TryApplyDictionary`） | [GlossaryManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.cs) |
| **CsvHelper.cs** | CSV 文件读写工具 | [CsvHelper.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/CsvHelper.cs) |

---

## 7. 专家配置

| 文件 | 职责 | 路径 |
|------|------|------|
| **ExpertProfile.cs** | 专家配置数据模型 | [ExpertProfile.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfile.cs) |
| **ExpertProfileManager.cs** | 专家配置管理：增删改查、持久化 | [ExpertProfileManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfileManager.cs) |

---

## 8. 本地化

| 文件 | 职责 | 路径 |
|------|------|------|
| **LocalizationManager.cs** | 程序界面本地化管理 | [LocalizationManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Localization/LocalizationManager.cs) |

---

## 9. 工具类

| 文件 | 职责 | 路径 |
|------|------|------|
| **StringExtensions.cs** | 字符串扩展方法：`HasChineseChars()`、`GetCacheKey()` 等 | [StringExtensions.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/StringExtensions.cs) |
| **PromptTemplates.cs** | AI 提示词模板：包含翻译规则、格式要求、术语替换规则 | [PromptTemplates.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/PromptTemplates.cs) |

---

## 10. 测试项目

> 使用 xUnit + Moq，13 个测试用例全部通过。

| 文件 | 测试数 | 测试内容 | 路径 |
|------|--------|----------|------|
| **ConfigServiceTests.cs** | 4 | 配置读写、加密存储 | [ConfigServiceTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/ConfigServiceTests.cs) |
| **StringExtensionsTests.cs** | 4 | `HasChineseChars()` 边界条件、`GetCacheKey()` 空值处理 | [StringExtensionsTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/StringExtensionsTests.cs) |
| **GlossaryManagerTests.cs** | 5 | 术语表 CRUD、CSV 读写 | [GlossaryManagerTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/GlossaryManagerTests.cs) |

---

## 11. 数据文件

> 程序运行时读取/生成的数据文件，在 `bin/` 目录下运行时会自动创建。

| 文件 | 用途 | 路径 |
|------|------|------|
| **config.json** | 用户配置：AI 提供商选择、批次大小、上次打开文件 | [config.json](file:///e:/translate/xml-ai-translator-main/config.json) |
| **translation_dictionary.json** | 翻译缓存（MD5 → 译文），大幅降低 API 调用成本 | [translation_dictionary.json](file:///e:/translate/xml-ai-translator-main/translation_dictionary.json) |
| **glossary_terms.json** | 术语对照表持久化文件 | [glossary_terms.json](file:///e:/translate/xml-ai-translator-main/glossary_terms.json) |
| **expert_profiles.json** | 专家配置持久化文件 | [expert_profiles.json](file:///e:/translate/xml-ai-translator-main/expert_profiles.json) |
| **stable_us.xml** | 游戏本地化 XML 示例文件 | [stable_us.xml](file:///e:/translate/xml-ai-translator-main/stable_us.xml) |

---

## 12. CI/CD

| 文件 | 说明 | 路径 |
|------|------|------|
| **ci.yml** | GitHub Actions 工作流：restore → build → test → publish (win-x64 self-contained) → upload artifact | [ci.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/ci.yml) |

---

## 13. 文档

| 文件 | 用途 | 读者 | 路径 |
|------|------|------|------|
| **README.md** | 项目简介、快速上手 | 所有用户 | [README.md](file:///e:/translate/xml-ai-translator-main/README.md) |
| **PRODUCT_PLAN.md** | 产品四阶段路线图 | 产品/项目管理 | [PRODUCT_PLAN.md](file:///e:/translate/xml-ai-translator-main/PRODUCT_PLAN.md) |
| **DEVELOPMENT_LOG.md** | 开发日志（按日期记录） | 开发者 | [DEVELOPMENT_LOG.md](file:///e:/translate/xml-ai-translator-main/DEVELOPMENT_LOG.md) |
| **HANDOVER.md** | 项目交接文档（架构、构建、问题排查） | 新接手的开发者 | [HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md) |
| **PROJECT_INDEX.md** | 项目文件索引（本文件） | 开发者/协作者 | [PROJECT_INDEX.md](file:///e:/translate/xml-ai-translator-main/PROJECT_INDEX.md) |

---

## 架构总览

```
xml-ai-translator-main/
├── SimpleXmlEditor.sln                          ← 解决方案
├── .gitignore
├── .github/workflows/ci.yml                     ← CI/CD
│
├── SimpleXmlEditor/                             ← 主项目
│   ├── App.xaml / .cs                           ← 入口 + DI
│   ├── MainWindow.xaml / .cs                    ← 主界面
│   ├── GlossaryWindow.xaml / .cs                ← 术语表
│   ├── SettingsWindow.xaml / .cs                ← 设置
│   ├── InputDialog.xaml / .cs                   ← 输入对话框
│   ├── FileTypeDialog.xaml / .cs                ← 文件类型选择
│   ├── ViewModels/MainViewModel.cs              ← MVVM 核心状态
│   ├── Services/
│   │   ├── Interfaces.cs                        ← 所有接口
│   │   ├── AiTranslationService.cs              ← AI 翻译
│   │   ├── ConfigService.cs                     ← 配置/加密
│   │   ├── XmlRepository.cs                     ← XML 安全读写
│   │   ├── TranslationEvaluator.cs              ← 质量评估
│   │   └── TranslationOrchestrator.cs           ← 翻译编排
│   ├── Dictionary/
│   │   ├── GlossaryManager.cs                   ← 术语表逻辑
│   │   └── CsvHelper.cs                         ← CSV 工具
│   ├── ExpertProfiles/
│   │   ├── ExpertProfile.cs                     ← 数据模型
│   │   └── ExpertProfileManager.cs              ← 管理逻辑
│   ├── Localization/LocalizationManager.cs      ← 本地化
│   ├── StringExtensions.cs                      ← 字符串工具
│   └── PromptTemplates.cs                       ← AI 提示词
│
├── SimpleXmlEditor.Tests/                       ← 测试项目 (13/13 ✅)
│   ├── ConfigServiceTests.cs
│   ├── StringExtensionsTests.cs
│   └── GlossaryManagerTests.cs
│
├── data files (config.json, glossary_terms.json, etc.)
└── docs (README, PRODUCT_PLAN, DEVELOPMENT_LOG, HANDOVER, PROJECT_INDEX)
```
