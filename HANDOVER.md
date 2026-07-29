# 项目交接文档 — XML AI Translator

> **最后更新**：2026-07-29  
> **项目状态**：功能可用，架构演进中（MVVM 过渡期）  
> **维护者**：Veloxcity  
> **技术栈**：C# / .NET 8.0 / WPF / Newtonsoft.Json  
> **最近变更**：译文合并优化、清空缓存回初始状态、删除"翻译前N行"、本地化完善、文档更新

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
├─────────────────────────────────────────────┤
│  Service 层                                  │
│  IAiTranslationService ✓                    │
│  IConfigService ✓                           │
│  IXmlRepository ✓                           │
│  TranslationOrchestrator (流程编排)          │
│  TranslationEvaluator (质量评估)             │
├─────────────────────────────────────────────┤
│  Domain 层                                   │
│  GlossaryManager (术语表)                    │
│  ExpertProfileManager (专家配置)             │
│  LocalizationManager (UI本地化)              │
└─────────────────────────────────────────────┘
```

### 2.2 核心数据流

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
| 注意事项 | `RecentRequests` 是 `Queue<DateTime>`，非线程安全，当前审计建议改为 `ConcurrentQueue` |
| 配置文件 | `StaticModels` / `ProviderRateLimits` / `ProviderConfig` 硬编码在类中 |

### 3.2 ConfigService [IConfigService]

| 职责 | 配置读写、翻译缓存管理、崩溃恢复 |
|------|------|
| 配置文件 | `config.json` — API Key、模型、语言等设置 |
| 缓存文件 | `translation_cache.json` — 原文(Key + MD5) → 译文的映射 |
| 恢复文件 | `translation_progress.json` — 翻译中断时的增量保存 |
| 关键方法 | `GetCacheKey(text)` — MD5 哈希，空文本返回 null |
| 注意事项 | `Cache` 是普通 `Dictionary`，审计建议改为 `ConcurrentDictionary` |

### 3.3 TranslationOrchestrator

| 职责 | 翻译流程编排：分批 → 术语 → 缓存 → prompt → API → 解析 |
|------|------|
| 创建时间 | 2026-07-29（从 MainWindow 抽取） |
| 回调机制 | `OnCacheHit` / `OnGlossaryHit` / `OnApiCall` / `OnApiChars`（Action 委托） |
| 注意事项 | 直接依赖 `GlossaryManager` 和 `ExpertProfileManager` 具体类（无接口） |

### 3.4 GlossaryManager

| 职责 | 统一术语表管理，支持词边界匹配和完整 CRUD |
|------|------|
| 存储文件 | `glossary_terms.json`（主）、兼容旧 `translation_dictionary.json` |
| 匹配策略 | 词边界匹配 → 最长匹配优先 → 大小写不敏感 |
| Regex 缓存 | 静态 `_regexCache`，每个术语一个 `Regex` 对象 |
| UI 窗口 | `GlossaryWindow.xaml.cs`（含 `TermEditDialog`、`ProfileSelectDialog`、`ConflictDialog`） |

### 3.5 TranslationEvaluator

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
- .NET 内置：WPF、System.Xml.Linq、System.Net.Http

---

## 7. 已知问题（2026-07-29 终审）

| 优先级 | 问题 | 描述 | 状态 |
|--------|------|------|------|
| **P0** | 线程安全 | `ConfigService.Cache` (Dictionary) 和 `AiTranslationService.RecentRequests` (Queue) 多线程不安全 | 待修复 |
| **P1** | 缺少接口 | `GlossaryManager`、`ExpertProfileManager`、`TranslationEvaluator` 无接口，阻碍单元测试 | 待修复 |
| **P1** | 代码重复 | MainWindow 中 LoadConfig/SaveConfig/SaveTranslationProgress/RestoreTranslationProgress 与 ViewModel/Service 重复 | 待修复 |
| **P2** | 代码重复 | `HasChineseChars` 在 MainWindow 和 TranslationOrchestrator 各有一份 | 待修复 |
| **P2** | 术语表 UI | 状态筛选框显示英文原始值（confirmed/pending/rejected），非本地化文本 | 待修复 |
| **P2** | 资源管理 | `HttpRequestMessage` 在循环中未 Dispose | 待修复 |
| **P3** | 错误处理 | 多处空 catch 块静默丢弃异常 | 低优先级 |
| **P3** | 死代码 | MainWindow 中的实例方法 `LoadConfig()` 已不被调用 | 待清理 |

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

1. **WPF DataGrid 数据刷新**：`CollectionViewSource.GetDefaultView().Refresh()` 比直接替换 `ItemsSource` 更高效
2. **XAML 命名空间**：Excel XML 使用 `urn:schemas-microsoft-com:office:spreadsheet`，解析时需指定命名空间
3. **AI Prompt 占位符**：`{LANGUAGE}` / `{CONTEXT}` / `{TEXTS}` / `{EXPERT_CONTEXT}` / `{GLOSSARY}` / `{MIXED_SOURCE_NOTE}` 均由 `BuildPrompt` 替换
4. **本地化**：所有 UI 文本通过 `LocalizationManager.GetString("Key")` 获取，新增文本需同时在 `InitializeTranslations()` 中添加中英文条目
5. **Dialog 模式**：XAML 创建的 Dialog 通过 XAML 绑定设置 DialogResult，代码创建的 Window 需手动设置
6. **DataGrid 内联编辑**：保存前务必调用 `EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true)`
