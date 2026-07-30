# 项目交接文档 — XML AI Translator

> **最后更新**：2026-07-30  
> **项目状态**：架构稳固（MVVM 100%、审计清零、测试就绪）  
> **维护者**：Veloxcity  
> **技术栈**：C# / .NET 8.0 / WPF / Newtonsoft.Json / Microsoft.Extensions.DI / xUnit / GitHub Actions  
> **最近变更**：Phase 1 技术债务清零（接口补全、代码去重、资源泄漏修复）+ Phase 2 质量基础设施（DI 容器、单元测试 13/13、CI/CD）

---

## 1. 项目定位

**XML AI Translator** 是一款专为**游戏本地化**设计的桌面工具。核心功能：
- 加载 Excel Spreadsheet XML 本地化文件（`mastertextfile_english.xml` 等）
- 通过 AI（8 个提供商）批量翻译英文原文到目标语言
- 术语表管理、专家配置、翻译缓存、速率限制、崩溃恢复

---

## 2. 架构设计

### 2.1 分层架构

```
┌─────────────────────────────────────────────┐
│  UI 层 (WPF)                                │
│  MainWindow / GlossaryWindow / SettingsWindow│
│  InputDialog / FileTypeDialog               │
├─────────────────────────────────────────────┤
│  ViewModel 层                                │
│  MainViewModel (INotifyPropertyChanged)      │
│  由 DI 容器构造注入（7 个接口参数）           │
├─────────────────────────────────────────────┤
│  Service 层（全部接口化）                     │
│  IAiTranslationService ✓                    │
│  IConfigService ✓                           │
│  IXmlRepository ✓                           │
│  IGlossaryManager ✓       (NEW)             │
│  IExpertProfileManager ✓  (NEW)             │
│  ITranslationEvaluator ✓  (NEW)             │
│  TranslationOrchestrator (流程编排)          │
├─────────────────────────────────────────────┤
│  Domain 层                                   │
│  GlossaryManager (术语表)                    │
│  ExpertProfileManager (专家配置)             │
│  LocalizationManager (UI本地化)              │
├─────────────────────────────────────────────┤
│  Infrastructure                              │
│  DI Container (App.Services)                 │
│  GitHub Actions CI/CD                        │
│  xUnit Test Suite (13 tests, 0 failures)     │
└─────────────────────────────────────────────┘
```

### 2.2 DI 容器架构

```
App.xaml.cs (OnStartup)
  └── ServiceCollection
        ├── Singleton: IConfigService, IGlossaryManager, IExpertProfileManager
        ├── Singleton: IAiTranslationService, ITranslationEvaluator
        ├── Singleton: IXmlRepository, TranslationOrchestrator
        ├── Singleton: MainViewModel
        └── Transient: MainWindow
  └── ServiceProvider.BuildServiceProvider()
        └── GetRequiredService<MainWindow>().Show()
```

### 2.3 核心数据流

```
用户操作 → MainWindow(UI) → MainViewModel → TranslationOrchestrator
                                                   │
                    ┌──────────────────────────────┼──────────────────────────────┐
                    ▼                              ▼                              ▼
            GlossaryManager               ConfigService(Cache)          AiTranslationService
            (术语匹配)                     (缓存检查)                    (API调用)
                    │                              │                              │
                    └──────────────────────────────┴──────────────────────────────┘
                                                   │
                                                   ▼
                                           ParseResponse
                                           (JSON解析+三级回退)
                                                   │
                                                   ▼
                                 更新 entry.Translation → UI刷新
```

### 2.3 翻译批处理流程（TranslateBatchAsync）

```
foreach batch in CreateBatches():
  1. 术语匹配 → GlossaryManager.TryGetValue (exact match on Key or Value)
  2. 缓存检查 → ConfigService.Cache (MD5 hash on Value)
  3. Prompt 构建 → BuildPrompt(entries, customPrompt, expertContext, glossaryContext)
  4. API 调用 → AiTranslationService.TranslateBatchAsync(prompt)
     - Gemini: generateContent API
     - 其他: /chat/completions (OpenAI 兼容格式)
  5. 响应解析 → ParseResponse (JSON) → ParseFallback (三级回退)
  6. 缓存写入 → _configService.Cache[cacheKey] = translation
  7. 结果应用 → entry.Translation = batchResults[entry.Value]
```

---

## 3. 核心服务说明

### 3.1 AiTranslationService [IAiTranslationService]

| 职责 | API 通信、速率限制、费用计算 |
|------|------|
| 提供商 | GoogleGemini, DeepSeek, Doubao, Qianwen, Zhipu, Moonshot, Wenxin, Xunfei |
| API 格式 | Gemini 原生格式 + OpenAI 兼容格式 |
| 速率限制 | `ModelLimits` 字典，按模型独立配置 rpm/rpd/tpm |
| 费用估算 | `CalculateCost(inputChars, outputChars, modelName)` |
| 关键方法 | `TranslateBatchAsync`、`TranslateSingleAsync`、`FetchAvailableModelsAsync` |
| 线程安全 | `RecentRequests` 为 `ConcurrentQueue<DateTime>` ✅ |
| 资源管理 | `HttpRequestMessage` 使用 `using` 声明 ✅ |
| 配置文件 | `StaticModels` / `ProviderRateLimits` / `ProviderConfig` 硬编码在类中 |

### 3.2 ConfigService [IConfigService]

| 职责 | 配置读写、翻译缓存管理、崩溃恢复 |
|------|------|
| 配置文件 | `config.json` — API Key、模型、语言等设置 |
| 缓存文件 | `translation_cache.json` — 原文(Key + MD5) → 译文的映射 |
| 恢复文件 | `translation_progress.json` — 翻译中断时的增量保存 |
| 关键方法 | `GetCacheKey(text)` — MD5 哈希，空文本返回 null |
| 线程安全 | `Cache` 为 `ConcurrentDictionary<string, string>` ✅ |

### 3.3 TranslationOrchestrator

| 职责 | 翻译流程编排：分批 → 术语 → 缓存 → prompt → API → 解析 |
|------|------|
| 创建时间 | 2026-07-29（从 MainWindow 抽取） |
| 依赖 | 全部通过接口注入：`IAiTranslationService`、`IConfigService`、`IGlossaryManager`、`IExpertProfileManager` ✅ |
| 回调机制 | `OnCacheHit` / `OnGlossaryHit` / `OnApiCall` / `OnApiChars`（Action 委托） |

### 3.4 GlossaryManager [IGlossaryManager]

| 职责 | 统一术语表管理，支持词边界匹配和完整 CRUD |
|------|------|
| 存储文件 | `glossary_terms.json`（主）、兼容旧 `translation_dictionary.json` |
| 匹配策略 | 词边界匹配 → 最长匹配优先 → 大小写不敏感 |
| 倒排索引 | `_invertedIndex`（word → term keys），O(W×C) 匹配 |
| UI 窗口 | `GlossaryWindow.xaml.cs`（含 `TermEditDialog`、`ProfileSelectDialog`、`ConflictDialog`） |
| 本地化 | 状态筛选框显示中文（已确认/待审核/已拒绝） ✅ |

### 3.5 TranslationEvaluator [ITranslationEvaluator]

| 职责 | AI 翻译质量评估（单条评分）和多代理投票（最佳译文选择） |
|------|------|
| 评估模式 | 单条 `EvaluateAsync` → 返回 0-10 评分+解释+改进建议 |
| 投票模式 | `VoteAsync` → 多候选译文 × 3 代理（Fluency/Accuracy/Style）× 单次 API 调用 |

### 3.6 XmlRepository [IXmlRepository]

| 职责 | XML 文件读写，支持两种格式 |
|------|------|
| 格式 1 | `ExcelSpreadsheet` — 3 列 Excel XML（Key \| Original \| Translation） |
| 格式 2 | `LocalisationData` — 游戏原生的 `<Localisation Key="...">` 格式 |

---

## 4. 数据模型

### 4.1 LocalizationEntry

| 字段 | 类型 | 说明 |
|------|------|------|
| RowNumber | int | DataGrid 行号 |
| Key | string | 原始键（如 `TEXT_TOOLTIP_UPGRADE`） |
| Value | string | 英文原文 |
| Translation | string | 中文译文（用户可编辑） |
| IsSelected | bool | 复选框状态 |
| StatusIcon | string(计算) | "✅" 或 "❌"（是否有译文） |

### 4.2 GlossaryTerm

| 字段 | 类型 | 说明 |
|------|------|------|
| English | string | 英文术语 |
| Chinese | string | 中文译文 |
| Category | string | 分类（如专家配置名） |
| Status | string | pending / confirmed / rejected |
| Tags | string | 逗号分隔标签 |
| CreatedAt | DateTime | 创建时间 |
| UpdatedAt | DateTime | 最后更新时间 |

### 4.3 ExpertProfile

| 字段 | 类型 | 说明 |
|------|------|------|
| Name | string | 配置名称（如 "星球大战"） |
| Description | string | 简短描述 |
| Context | string | AI 思考指令 |
| Glossary | Dictionary<string,string> | 术语表（English → Chinese） |

---

## 5. 配置文件

| 文件 | 路径 | 格式 | 作用 |
|------|------|------|------|
| `config.json` | 程序目录 | JSON | AI 提供商、模型、API Key、语言、批次大小等 |
| `translation_cache.json` | 程序目录 | JSON | `{ hash: translation }` 翻译缓存 |
| `translation_progress.json` | 程序目录 | JSON | 崩溃恢复临时文件 |
| `glossary_terms.json` | `Environment.CurrentDirectory` | JSON | 术语表（数组格式） |
| `expert_profiles.json` | 程序目录 | JSON | 专家配置文件 |

---

## 6. 构建与运行

### 6.1 环境要求

- Windows 10/11
- .NET 8.0 SDK（开发）/ .NET 8.0 Runtime（运行）
- Visual Studio 2022 或 `dotnet` CLI

### 6.2 构建命令

```bash
# 进入项目目录
cd xml-ai-translator-main

# 编译
dotnet build SimpleXmlEditor/SimpleXmlEditor.csproj

# 运行
dotnet run --project SimpleXmlEditor/SimpleXmlEditor.csproj

# 发布自包含单文件（无需安装运行时）
dotnet publish SimpleXmlEditor/SimpleXmlEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 6.3 依赖

- `Newtonsoft.Json`：JSON 序列化/反序列化
- `Microsoft.Extensions.DependencyInjection`：DI 容器
- .NET 内置：WPF、System.Xml.Linq、System.Net.Http

### 6.4 测试

```bash
# 运行所有单元测试
dotnet test SimpleXmlEditor.Tests/SimpleXmlEditor.Tests.csproj

# 当前测试覆盖
# ConfigServiceTests: 4 个（GetCacheKey + Cache 类型验证）
# StringExtensionsTests: 4 个（HasChineseChars 边界测试）
# GlossaryManagerTests: 5 个（CRUD 操作验证）
# 总计: 13/13 ✅
```

### 6.5 CI/CD

- 配置文件：`.github/workflows/ci.yml`
- 触发：push / PR 到 main/master
- 流程：restore → build → test → publish (win-x64 self-contained) → upload artifact

---
## 7. 已知问题（2026-07-30 — 审计清零）

> **8 个已知问题已全部关闭。** Phase 1+2 完成后无新增问题。

| 优先级 | 问题 | 状态 | 修复日期 |
|--------|------|------|----------|
| **P0** | 线程安全 | ✅ 已修复 | 早期（ConcurrentDictionary/ConcurrentQueue 已就位） |
| **P1** | 接口缺失（GlossaryManager/ExpertProfileManager/TranslationEvaluator） | ✅ 已修复 | 2026-07-30 |
| **P1** | MainWindow 重复代码（6 个方法） | ✅ 已修复 | 2026-07-30 |
| **P2** | HasChineseChars 重复 | ✅ 已修复 | 2026-07-30 |
| **P2** | 术语表 UI 英文状态显示 | ✅ 已修复 | 2026-07-30 |
| **P2** | HttpRequestMessage 未 Dispose | ✅ 已修复 | 2026-07-30 |
| **P3** | 空 catch 块 | ✅ 已验证 | 2026-07-30 |
| **P3** | 死代码 LoadConfig() | ✅ 已清理 | 2026-07-30 |

---

## 8. 常见问题排查

### 8.1 术语表窗口打开崩溃
- **现象**：打开术语表窗口后程序崩溃 / 栈溢出
- **原因**：中文状态下 `PopulateFilterCombos()` 修改 ComboBox 选中项触发 `SelectionChanged` → 再次调用 `RefreshAll()` → `PopulateFilterCombos()` 死循环
- **修复**：已添加 `_suppressFilterEvents` 守卫（2026-07-29）
- **文件**：`GlossaryWindow.xaml.cs`

### 8.2 术语表添加/编辑/合并按钮无效
- **现象**：点击保存后对话框关闭但无任何效果
- **原因**：代码创建的 Window 子类未设置 `DialogResult`
- **修复**：已修复 `TermEditDialog.SaveAndClose()` 和 `ProfileSelectDialog`（2026-07-29）

### 8.3 翻译缓存"污染"
- **现象**：所有空值的原文都缓存了同一个错误译文
- **原因**：空文本的 `GetCacheKey` 返回了有效 key
- **修复**：`GetCacheKey(null/"")` 返回 `null`，缓存读写均跳过 null key

### 8.4 中文源文本被误翻译
- **现象**：已是中文的条目被"重译"成不通顺的文本
- **机制**：`BuildPrompt` 检测中文源（`HasChineseChars`），标记 `[EXISTING ZH]`，让 AI 做审核纠正而非重新翻译

### 8.5 快捷键
- `Ctrl+S`：快速保存（保存缓存和配置，不修改 XML 文件）
- `Ctrl+F`：打开查找栏
- `Ctrl+Z`：撤销批量替换
- `Esc`：关闭查找栏或清除筛选

### 8.6 清空缓存后译文仍加载
- **现象**：点击清空缓存按钮后重启，译文仍然出现
- **原因**：之前版本存在三个问题：
  1. `LastLoadedFilePath` 未持久化清空到配置文件
  2. 合并译文时错误覆盖了原文路径
  3. 启动时无条件加载默认 `stable_us.xml`
- **修复**（2026-07-29）：清空缓存后完整重置状态并保存配置，不再加载任何默认文件

### 8.7 导入译文后需重新加载原文
- **现象**：加载原文 XML → 加载译文 XML → 原文丢失
- **原因**：`LoadXml` 无论原文/译文都执行 `Entries.Clear()`
- **修复**（2026-07-29）：加载译文时按 Key 合并到已有原文条目，不清空

### 8.8 缓存统计数字不实时更新
- **现象**：导入译文或快速保存后，状态栏缓存数量不变化
- **原因**：多处数据修改后未调用 `UpdateCacheInfo()`
- **修复**（2026-07-29）：在译文合并、SaveXml、QuickSave、每批翻译完成、翻译结束等所有时机统一刷新

### 8.9 超大术语表（几万条）导致翻译卡顿
- **现象**：导入几万条术语后，每批翻译前长时间卡顿
- **原因**：旧的 `BuildGlossaryContext` 遍历所有术语 × 所有条目（O(N×M)），100k 术语时每批 500 万次操作
- **修复**（2026-07-29）：倒排索引方案
  - `GlossaryManager` 新增 `_invertedIndex`（word → term keys）和 `GetGlossaryContextTerms()`
  - 匹配从 O(G×E) 降到 O(W×C)，**1000 倍加速**
  - `MAX_GLOSSARY_CONTEXT_TERMS = 50`：每批最多注入 50 条术语，防止 prompt 超 Token 上限

---

## 9. UI 窗口速查

| 窗口 | 文件 | 触发方式 |
|------|------|----------|
| 主窗口 | `MainWindow.xaml` | 启动自动打开 |
| 设置 | `SettingsWindow.xaml` | ⚙️ 设置按钮 |
| 术语表 | `GlossaryWindow.xaml` | 📖 术语表按钮 |
| 文件类型选择 | `FileTypeDialog.xaml` | 📁 加载 → 选择文件后弹出 |
| 批量替换 | `InputDialog.xaml` | 🔄 批量替换按钮 |
| 统计 | MessageBox | 📊 统计按钮 |

---

## 10. 开发注意事项

1. **DI 容器**：所有服务通过 `App.Services` 获取，不要在 ViewModel/Window 中手动 new 服务类
2. **接口优先**：新增服务必须先定义接口，以 `IXxxManager`/`IXxxService` 命名，在 `Interfaces.cs` 中声明
3. **WPF DataGrid 数据刷新**：`CollectionViewSource.GetDefaultView().Refresh()` 比直接替换 `ItemsSource` 更高效
4. **XAML 命名空间**：Excel XML 使用 `urn:schemas-microsoft-com:office:spreadsheet`，解析时需指定命名空间
5. **AI Prompt 占位符**：`{LANGUAGE}` / `{CONTEXT}` / `{TEXTS}` / `{EXPERT_CONTEXT}` / `{GLOSSARY}` / `{MIXED_SOURCE_NOTE}` 均由 `BuildPrompt` 替换
6. **本地化**：所有 UI 文本通过 `LocalizationManager.GetString("Key")` 获取，新增文本需同时在 `InitializeTranslations()` 中添加中英文条目
7. **Dialog 模式**：XAML 创建的 Dialog 通过 XAML 绑定设置 DialogResult，代码创建的 Window 需手动设置
8. **DataGrid 内联编辑**：保存前务必调用 `EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true)`
9. **扩展方法**：通用字符串处理放 `StringExtensions.cs`
10. **测试驱动**：修复 Bug 先写复现测试，新功能先写验收测试

---
## 11. 项目结构

```
xml-ai-translator-main/
├── SimpleXmlEditor/                  # WPF 主项目
│   ├── Services/                     # 服务层（全部接口化）
│   │   ├── Interfaces.cs             # 6 个服务接口定义
│   │   ├── AiTranslationService.cs   # IAiTranslationService
│   │   ├── ConfigService.cs          # IConfigService
│   │   ├── TranslationEvaluator.cs   # ITranslationEvaluator
│   │   ├── TranslationOrchestrator.cs
│   │   └── XmlRepository.cs          # IXmlRepository
│   ├── ViewModels/
│   │   └── MainViewModel.cs          # UI 状态管理（INotifyPropertyChanged）
│   ├── Dictionary/
│   │   ├── CsvHelper.cs
│   │   └── GlossaryManager.cs        # IGlossaryManager
│   ├── ExpertProfiles/
│   │   ├── ExpertProfile.cs
│   │   └── ExpertProfileManager.cs   # IExpertProfileManager
│   ├── Localization/
│   │   └── LocalizationManager.cs
│   ├── MainWindow.xaml/.cs           # 主界面
│   ├── GlossaryWindow.xaml/.cs       # 术语表管理
│   ├── SettingsWindow.xaml/.cs       # 设置界面
│   ├── InputDialog.xaml/.cs
│   ├── FileTypeDialog.xaml/.cs
│   ├── StringExtensions.cs           # 公共扩展方法
│   ├── PromptTemplates.cs
│   ├── App.xaml/.cs                  # DI 容器入口
│   └── SimpleXmlEditor.csproj
├── SimpleXmlEditor.Tests/            # xUnit 测试项目
│   ├── ConfigServiceTests.cs         # 4 个测试
│   ├── StringExtensionsTests.cs      # 4 个测试
│   ├── GlossaryManagerTests.cs       # 5 个测试
│   └── SimpleXmlEditor.Tests.csproj
├── .github/workflows/
│   └── ci.yml                        # GitHub Actions CI/CD
├── DEVELOPMENT_LOG.md                # 开发日志
├── HANDOVER.md                       # 项目交接文档
├── PRODUCT_PLAN.md                   # 产品规划
└── README_zh.md                      # 中文 README
```
