# 项目交接文档 — XML AI Translator

> **最后更新**：2026-08-02  
> **项目状态**：架构稳固（MVVM 100%、审计清零、测试就绪、MainWindow 拆分完成、DataGrid 全面对齐 Excel、Excel 式选择模型、评估换厂商落地、评分持久化 + 自动保存）  
> **维护者**：Veloxcity、BestFly666  
> **技术栈**：C# / .NET 8.0 / WPF / HandyControl 3.5.1 / Newtonsoft.Json / Microsoft.Extensions.DI / xUnit / GitHub Actions  
> **最近变更**：术语表体验完善（冲突检测进度 + 布局优化）+ 冲突/一致性检测结果导出 CSV + 评分持久化缓存（score_cache.json）+ Excel 式 5 分钟自动保存

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
| 配置文件 | `StaticModels` / `ProviderRateLimits` / `ProviderConfig` 硬编码在类中；模型列表支持从厂商 `GET /models` 动态拉取（`FetchOpenAiCompatModelsAsync`），失败回退静态列表 |
| 模型动态获取 | `FetchOpenAiCompatModelsAsync` — OpenAI 兼容厂商在线拉取；`EnsureRateLimitsFromStatic` — 动态模型速率限制兜底（2026-08-01，DeepSeek 模型升级后新增） |

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
| 批量模式 | `EvaluateBatchAsync`（20 条/批）+ `VoteBatchAsync`（10 条/批），chunk 级 + 逐条级异常兜底，失败跳过不崩溃 |
| 独立评估模型 | 注入 `IConfigService`，配置 `EvaluationAiProvider`/`EvaluationModel`/`EncryptedEvaluationApiKey` 时惰性创建**评估专用 AiTranslationService 实例**（打破同源偏差）；留空回退翻译模型 |

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
| EvaluationScore | double | AI 评估分数（-1 = 未评估） |
| EvaluationScoreDisplay | string(计算) | 评分列显示文本（如 "8.5"，未评估为空） |
| EvaluationScoreColor | string(计算) | 评分颜色（≥8 绿 / ≥5 黄 / <5 红 / 未评估灰） |
| EvaluationImprovement | string | AI 改进建议（评分列 tooltip 显示） |
| `SetIsSelectedSilent(bool)` | 方法 | 静默设置选择状态，不触发 PropertyChanged（用于批量选择防卡顿） |

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
| `config.json` | `%LocalAppData%\SimpleXmlEditor\` | JSON | AI 提供商、模型、API Key、语言、批次大小、**评估模型配置**（`EvaluationAiProvider`/`EvaluationModel`/`EncryptedEvaluationApiKey`）等 |
| `translation_cache.json` | `%LocalAppData%\SimpleXmlEditor\` | JSON | `{ hash: translation }` 翻译缓存（每条 2 键：`Key` + `MD5(原文)`） |
| `translation_progress.json` | `%LocalAppData%\SimpleXmlEditor\` | JSON | 崩溃恢复临时文件（2026-08-05 起与主缓存统一，原在 bin 程序目录；QuickSave/翻译完成后删除） |
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

# 编译（推荐通过 .sln 编译所有项目含测试）
dotnet build

# 仅编译主项目
dotnet build SimpleXmlEditor/SimpleXmlEditor.csproj

# 运行
dotnet run --project SimpleXmlEditor/SimpleXmlEditor.csproj

# 运行测试
dotnet test

# 发布自包含单文件（无需安装运行时）
dotnet publish SimpleXmlEditor/SimpleXmlEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 6.3 依赖

- `Newtonsoft.Json`：JSON 序列化/反序列化
- `Microsoft.Extensions.DependencyInjection`：DI 容器
- `HandyControl 3.5.1`：UI 组件库（全局主题 + 控件样式）
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
## 7. 已知问题（2026-07-30 — 审计清零 + 运行时修复）

> **所有已识别问题已全部关闭。**

| 优先级 | 问题 | 状态 | 修复日期 |
|--------|------|------|----------|
| **P0** | 线程安全 | ✅ 已修复 | 早期（ConcurrentDictionary/ConcurrentQueue 已就位） |
| **P0** | `dotnet run` 无响应 — DI 容器无法解析 `TranslationOrchestrator`（Action<string> 未注册） | ✅ 已修复 | 2026-07-30 |
| **P0** | CI/CD 构建失败 — 缺少 `.sln` 文件，`dotnet restore` 无法定位项目 | ✅ 已修复 | 2026-07-30 |
| **P1** | 接口缺失（GlossaryManager/ExpertProfileManager/TranslationEvaluator） | ✅ 已修复 | 2026-07-30 |
| **P1** | MainWindow 重复代码（6 个方法） | ✅ 已修复 | 2026-07-30 |
| **P2** | HasChineseChars 重复 | ✅ 已修复 | 2026-07-30 |
| **P2** | 术语表 UI 英文状态显示 | ✅ 已修复 | 2026-07-30 |
| **P2** | HttpRequestMessage 未 Dispose | ✅ 已修复 | 2026-07-30 |
| **P3** | 空 catch 块 | ✅ 已验证 | 2026-07-30 |
| **P3** | 死代码 LoadConfig() | ✅ 已清理 | 2026-07-30 |
| **P3** | 手动编辑翻译无撤销（审计 #30） | ✅ 已修复（BeginningEdit 入栈） | 2026-08-01 |

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

### 8.10 全选/取消全选卡顿
- **现象**：大文件点击全选/取消全选时 UI 明显卡顿
- **原因**：`EntriesGrid.Items.Refresh()` 重建整个视图 + 复选框勾选联动整行选中产生 SelectionChanged 事件风暴
- **修复**（2026-08-01）：改用 `_suppressSelectionSync` 防抖，去掉 `Refresh()`，复选框状态靠 PropertyChanged 增量更新

### 8.11 冲突检测点击后卡死
- **现象**：术语管理器点击"冲突检测"后窗口完全无响应
- **原因**：`DetectConflicts` 在 UI 线程同步执行，O(条目数 × 术语数) × 正则匹配开销
- **修复**（2026-08-01）：包裹在 `Task.Run` 后台线程执行，`Dispatcher.BeginInvoke` 回 UI 线程显示结果

### 8.12 冲突检测结果窗口崩溃
- **现象**：冲突检测完成后弹出结果对话框时崩溃
- **原因**：GlossaryWindow 触发事件后 `Close()`，异步回调时 `dialog.Owner = this` 指向已关闭窗口
- **修复**（2026-08-01）：冲突结果显示职责转移到 MainWindow.`ShowConflictResults`，Owner 设为 MainWindow

### 8.13 DataGrid RowHeight="Auto" 导致启动崩溃
- **现象**：`dotnet run` 后窗口不弹出（静默崩溃）
- **原因**：`DataGrid.RowHeight` 属性类型是 `double`，不接受 `"Auto"`（那是 `Grid` 的行高语法）
- **修复**（2026-08-01）：删除 `RowHeight="Auto"`，DataGrid 默认即按内容自适应高度

### 8.14 架构约束：MainWindow 是纯前端
- **规则**：MainWindow.xaml.cs 及其 partial class 文件只承载 UI 职责（事件转发、生命周期、主题/本地化）
- **禁止**：在 MainWindow 中写入业务逻辑、算法、数据处理
- **正确位置**：业务逻辑放 `Services/`，编排逻辑放 `ViewModels/`
- **强制**：单文件不超过 400 行，超过继续拆分 partial class

### 8.15 翻译全部失败 [HTTP 400] 模型名错误
- **现象**：日志 `[HTTP 400] The supported API model names are deepseek-v4-pro or deepseek-v4-flash, but you passed deepseek-flash`
- **原因**：DeepSeek 2026-04-24 升级 API 模型名，`deepseek-flash`/`deepseek-pro` 停用
- **修复**（2026-08-01）：静态模型列表更新 + 新增 `FetchOpenAiCompatModelsAsync` 在线拉取模型；设置 → 刷新 → 选 `deepseek-v4-flash`

### 8.16 多条评估/投票崩溃
- **现象**：选择多条记录评估或投票时程序崩溃
- **原因**：
  1. `ToDictionary` 遇到重复译文抛 `ArgumentException`
  2. `EvaluateBatchAsync`/`VoteBatchAsync` 无异常兜底，HTTP 错误/超时直接崩溃
  3. HttpClient 超时 30s 太短（批量 prompt 长响应慢）
- **修复**（2026-08-01）：循环赋值替代 ToDictionary；chunk 级 + 逐条级 try-catch 降级；超时 30s → 120s

### 8.17 评估结果无输出窗口 / 弹窗崩溃
- **现象**：多条评估完成后弹窗崩溃；或评估完看不到结果
- **原因**：结果靠弹窗（EvaluationWindow）展示，多条数据时弹窗 UI 崩溃
- **修复**（2026-08-01）：DataGrid 新增**"评分"列**，评估/投票结果直接写入表格（分数颜色编码 + 点击列头排序 + tooltip 显示改进建议），彻底移除弹窗

### 8.18 投票"没反应" / 翻译出英文
- **现象**：多代理投票点击后长时间无反馈；投票出的译文是英文
- **原因 1**：候选生成逐条调 API 无进度日志，用户以为卡死
- **原因 2**：prompt 把 `targetLang` 错误标注在原文上（`Original (Chinese): ...`），AI 误以为原文是中文
- **修复**（2026-08-01）：候选生成逐条打进度日志；prompt 改 `Original (English): ...` + `Target language: Chinese` + "All candidates MUST be in {targetLang}"

### 8.19 手动编辑翻译后"没有可撤销的操作"
- **现象**：手动改了译文，点撤销提示无可撤销操作
- **原因**：撤销栈只在批量替换/批量翻译时入栈，手动编辑不入栈
- **修复**（2026-08-01）：DataGrid `BeginningEdit` 事件在开始编辑 Translation 列时先 push 当前值快照，手动编辑也可 Ctrl+Z 撤销，撤销后自动跳转定位到该行

### 8.20 全选/选中整列卡顿
- **现象**：大文件全选或选中整列严重卡顿；全选后再选整列更卡
- **根因链**（2026-08-01 五轮排查）：
  1. 逐行设置 `IsSelected` 触发 PropertyChanged 事件风暴
  2. `SelectAll()` 一次性选中全部 cell（10000×6=60000 个）——**Ctrl+A 触发 DataGrid 内置 SelectAll，MainWindow_KeyDown 拦不住**
  3. `SelectedCells` 滚动补选只增不减导致集合膨胀，`Clear()` 变慢
- **修复**：Excel 式逻辑选择模型（`_logicalSelectAll`/`_logicalSelectColumn` 标志 + 只高亮可见行 + 滚动 Clear+重选）+ `PreviewKeyDown` 拦截 Ctrl+A + 显式开启行虚拟化

### 8.21 冲突检测没有进度/日志，不知道进行到哪
- **现象**：点击"Detect Conflicts"后界面无反馈，大数据量下以为卡死
- **修复**（2026-08-02）：`DetectConflicts` 增加 `onProgress` 回调（按总数自适应步长，全程约 20 次上报），主窗口日志区实时显示"开始 → 进度 x/y → 完成并列出冲突数"，全部走本地化 key

### 8.22 评分关了程序就没了
- **现象**：评估/投票后关闭程序，重新打开文件评分列为空
- **修复**（2026-08-02）：评分持久化到 `score_cache.json`（`%LocalAppData%\SimpleXmlEditor\`），保存时机为单条/批量评估、投票、快速保存；加载 XML 后按 Key 恢复（仅恢复未评估条目）。评分**不写入 XML**

### 8.23 冲突/一致性检测结果怎么导出对照修改
- **说明**（2026-08-02）：冲突对话框新增"导出 CSV"按钮（`conflict_report_日期.csv`）；一致性扫描完成弹窗询问是否导出（`consistency_report_日期.csv`）。列结构见 ReviewExporter

### 8.24 自动保存是怎么回事
- **说明**（2026-08-02）：每 5 分钟自动执行 `QuickSave()`（同步翻译缓存 + 评分缓存 + 配置），**不直接写 XML**——源 XML 仍由你手动保存，防止自动覆盖源文件导致数据损坏；仅加载文件后才触发

### 8.25 删除译文后快速保存，重新打开又出现
- **现象**：删除某条译文 → Ctrl+S → 重启 → 译文列又恢复；且"缓存文件变来变去"
- **根因**（2026-08-05 五轮排查）：**双轨缓存文件位置不统一**——主缓存 `translation_cache.json` 在 AppData，崩溃恢复 `translation_progress.json` 在 bin 程序目录（Debug/Release 各一份）。加载 XML 时 `RestoreTranslationProgress` 从残留 progress 文件按原文 Value 恢复旧译文，**绕过主缓存的"删除键"状态** → 删除被"复活"；progress 只在翻译中断后残留，QuickSave 从不更新它 → 行为不一致
- **修复**（2026-08-05）：
  1. `ConfigService` 新增 `_progressPath` 统一到 AppData；构造函数自动删除 bin 遗留旧文件
  2. `SaveTranslationProgressAsync`/`RestoreTranslationProgress`/`DeleteProgressFile` 三处路径统一
  3. QuickSave 成功后删除 progress 文件（用户主动保存 = 主缓存已是最新快照）
- **注意**：删除一条译文缓存 -2（`Key` + `MD5(原文)` 双键）是正常设计，非 bug
- **文件**：`ConfigService.cs` / `ConfigService.Cache.cs` / `MainWindow.Events.File.cs`

### 8.26 翻译速度慢 / 大批次失败 / 3 路并发退化为串行
- **现象**：批次 >30 条时特别慢；3 路并发"感觉一批批处理"
- **根因 1**（2026-08-06）：分批按固定条目数（50），不估算输出 token → 中文 50 条输出超 max_tokens → 截断 → 拆半重试风暴（最多 7 次串行请求）
- **修复 1**：改为按估算输出 token 动态分批（3800 token/批），中文约 20-30 条/批，永不截断
- **根因 2**（2026-08-06）：`MainWindow.Handlers.cs` 全部事件订阅用 `Dispatcher.Invoke`（同步阻塞），后台线程等 UI 线程处理完才继续 → `batchSemaphore.Release()` 延迟 → 下一批干等
- **修复 2**：`Invoke` → `BeginInvoke`（异步），后台线程立即返回；仅 `ConfirmationRequested` 保留 `Invoke`
- **根因 3**（2026-08-06）：`SaveTranslationProgressAsync` 在 `finally { Release() }` 之前 await，序列化 25000 条阻塞信号量释放
- **修复 3**：移到 `Release()` 之后
- **无法控制**：DeepSeek API 端同时只处理 ~2 个请求（2500 RPM ≠ 并发生成数），客户端无法突破
- **文件**：`TranslationOrchestrator.cs` / `MainWindow.Handlers.cs` / `MainViewModel.Translation.cs`

### 8.27 本地模型方案（待推进）
- **需求**：21487 条全量翻译 ~13 小时，API 端并发生成限制为硬瓶颈
- **方案**：Python FastAPI + vLLM 独立服务，暴露 OpenAI 兼容接口，WPF 端零改动
- **前提**：需要独立 GPU（7B 需 6GB+，14B 需 12GB+）
- **状态**：待用户确认 GPU 配置后决定是否推进

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
11. **MainWindow 是纯前端**：新功能不得写入 MainWindow（含 partial class 文件），业务逻辑放 `Services/` 或 `ViewModels/`，单文件不超过 400 行
12. **DataGrid 交互**：单元格点击只选格子、行号点击选整行、列字母点击选整列、Ctrl+A 全选（均走 Excel 式逻辑选择模型，见第 18 条），全选/反选用 `_suppressSelectionSync` 防抖
13. **HandyControl 主题**：`PrimaryColor` 是 Color 类型而非 Brush，误写为 Brush 会导致启动崩溃
14. **DataGrid.RowHeight**：类型是 `double`，不接受 `"Auto"`，不设置即为内容自适应
15. **冲突检测**：必须在后台线程执行（`Task.Run`），结果显示通过 `Dispatcher.BeginInvoke` 回 UI 线程
16. **评估/投票同源偏差**：已落地 Phase 1（2026-08-01）——设置"评估模型"Tab 可配置独立厂商/模型，`TranslationEvaluator` 惰性创建评估专用服务实例；留空回退翻译模型
17. **DataGrid Ctrl+A**：DataGrid 内置 Ctrl+A 会 `SelectAll()` 全部 cell 导致卡死，且冒泡事件拦不住——必须在 DataGrid `PreviewKeyDown`（隧道）拦截，编辑单元格时（焦点在 TextBox）放行
18. **DataGrid 大范围选择**：禁止逐格 `SelectedCells.Add`（每个 cell 触发布局更新）——用"逻辑标志（全选/整列）+ 只高亮可见行 + `ScrollChanged` 滚动补选（先 Clear 再重选防积累）"的 Excel 式模型；业务读取走 `GetSelectedEntries()` 逻辑标志分支
19. **模型名不硬编码**：静态模型列表是"已知模型缓存"，务必保留厂商 `GET /models` 动态拉取 + 静态兜底（DeepSeek 2026-04 模型升级教训）
20. **批量异步编排必须兜底**：批量评估/投票（chunk → 逐条 → 跳过）每层 try-catch 降级；`ToDictionary` 遇重复键会崩溃，改用循环赋值
21. **API 超时**：批量评估 prompt 长，HttpClient 超时设 120s（30s 会频繁超时）
22. **缓存/进度文件统一在 AppData**（2026-08-05）：`translation_cache.json` 与 `translation_progress.json` 均存于 `%LocalAppData%\SimpleXmlEditor\`，禁止用 `AppDomain.CurrentDomain.BaseDirectory`（bin 目录随构建变化）；**QuickSave 后必须删除 progress 文件**——否则崩溃恢复文件会绕过主缓存的删除状态，导致"删除的译文重开复活"
23. **高频 UI 事件必须用 BeginInvoke**（2026-08-06）：后台线程通过 `Dispatcher.Invoke` 更新 UI 会阻塞后台线程；如果 Invoke 在 `batchSemaphore.Release()` 之前，有效并发度退化（3 路退化为串行）。`LogMessage`、`TranslationProgressChanged`、`StatusMessageChanged` 等高频事件必须用 `Dispatcher.BeginInvoke`；仅 `ConfirmationRequested`（需返回值）保留 `Invoke`
24. **分批必须按输出 token 预算**（2026-08-06）：按固定条目数分批无法预防输出截断——中文每条 token 数是英文的 ~5 倍，50 条中文可能超 max_tokens 而截断，触发拆半重试风暴。使用 `EstimateOutputTokens` 按 3800 token/批动态切批
25. **条目 Key 可注入 Prompt**（2026-08-06）：条目 Key（如 `TEXT_SPEECH_*`、`UNIT_*_DESCRIPTION`）含内容类型/场景线索，注入后帮助模型判断语境。格式 `1. [KEY] "原文"`，Key 经 `SanitizePromptText` 转义，规则明确禁止将 Key 混入译文
26. **进度保存不阻塞信号量**（2026-08-06）：`SaveTranslationProgressAsync` 必须在 `batchSemaphore.Release()` **之后**执行——先释放信号量让下一批启动，再保存进度。避免序列化大数据 + 写文件阻塞并发管线
27. **API RPM ≠ 并发生成数**（2026-08-06）：DeepSeek 2500 RPM 是每分钟允许发送的请求数，不等于服务器端同时生成的请求数（观察值 ≈ 2）。客户端并发度再高也无法突破 API 端的并发生成限制

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
│   ├── MainWindow.xaml/.cs           # 主界面（partial class，拆分为 6 个文件）
│   ├── MainWindow.Localization.cs    # ApplyLocalization / UpdateInfoLabels
│   ├── MainWindow.Theme.cs           # ApplyTheme（深色/浅色模式）
│   ├── MainWindow.Grid.cs            # DataGrid 交互：选中/列字母/行拖拽/批量勾选
│   ├── MainWindow.Helpers.cs         # AddLog / UpdateCacheInfo / ShowControlButtons
│   ├── MainWindow.Events.cs          # UI 事件处理：点击/筛选/菜单/快捷键/翻译命令
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
├── SimpleXmlEditor.sln                # 解决方案文件（包含两个项目）
├── DEVELOPMENT_LOG.md                # 开发日志
├── HANDOVER.md                       # 项目交接文档
├── PRODUCT_PLAN.md                   # 产品规划
├── PROJECT_INDEX.md                  # 项目文件索引
└── README.md                         # 项目说明
```
