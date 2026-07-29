# 开发日志 — XML AI Translator

> 项目仓库：`xml-ai-translator-main`  
> 作者：Veloxcity  
> 技术栈：C# / .NET 8.0 / WPF / Newtonsoft.Json  
> 目标平台：Windows 10/11

---

## 项目概述

**XML AI Translator** 是一款专为游戏本地化设计的 XML 批量翻译工具。核心定位是基于 AI（Google Gemini、OpenAI、Claude 等）对 Excel Spreadsheet 格式的 XML 本地化文件进行批量翻译，通过智能分批、翻译缓存、速率限制等机制大幅降低 API 调用成本（90%+），同时提供 Material Design 风格的现代化桌面界面。

---

## 2026-07-29（续）

### 功能简化：删除"翻译前N行"

- **背景**："翻译选中"和"翻译前 N 行"两个功能高度重叠，增加界面复杂度
- **方案**：删除"翻译前 N 行"（`TranslatePartialBtn`、`PartialCountTxt`、`FirstLabel`、`RowsLabel`），保留"翻译选中"（支持强制刷新）+ "翻译全部"
- **影响范围**：`MainWindow.xaml`（UI 元素）、`MainWindow.xaml.cs`（事件处理 + 本地化绑定 + ShowControlButtons）、`LocalizationManager`（移除 `FirstLabel`/`RowsLabel`/`EnterValidNumber` 6 条 key）

### 译文合并优化

- **问题**：加载原文 XML 后再加载译文 XML，原文被清空只剩译文
- **原因**：`LoadXml()` 无论原文/译文都执行 `Entries.Clear()`
- **修复**：`LoadXml` 新增 merge 分支——当 `isTranslationFile=true` 且已有条目时，按 Key 合并译文到已有条目（不清空）：
  - 构建 Key → Entry 的查找字典（`Dictionary.TryAdd` 处理重复 Key）
  - 匹配成功 → 写入 `existing.Translation` + 缓存
  - 日志显示合并结果："译文已合并 - 2850/3000 条匹配"
- **持久化**：合并路径不覆盖 `LastLoadedFilePath`（保持指向原文）

### 清空缓存回归初始状态

- **问题**：清空缓存后 UI 仍显示已加载条目，重启又自动加载文件
- **根因链**：
  1. 清空缓存未清除 `Entries` 和统计计数器
  2. `LastLoadedFilePath` 未持久化清空（配置文件中仍存有路径）
  3. 启动时 `else { LoadXml(); }` 无条件加载 `stable_us.xml`
- **修复**（三轮迭代）：
  1. 清除 `Entries`、`CacheHits/ApiCalls/GlossaryHits`、筛选框、状态栏
  2. 同步清除 `_configService.Config.LastLoadedFilePath` 并 `SaveConfig()`
  3. 删除启动时的 `else { LoadXml(); }` 分支——无历史路径时完全空白启动

### 自动加载原文修复

- **问题**：首次加载原文后重启不自动加载
- **原因**：`LoadXml` 只设置了 `_viewModel.LastLoadedFilePath`，未持久化到 `config.json`
- **修复**：在正常加载路径中立即调 `SaveConfig()` 持久化

### 缓存统计显示修复

- **问题**：缓存的显示数字输入导入后不实时更新，重启后才刷新
- **根因**：以下时机缺少 `UpdateCacheInfo()` / `UpdateGlossaryInfo()` 调用：
  - 译文合并完成
  - `SaveXml`（另存为）完成
  - `QuickSave`（快速保存）完成
  - 批量翻译每批完成
  - 翻译流程最终 `finally` 块
- **修复**：以上 5 个位置全部补充刷新调用

### 本地化内容更新

- **问题**：设置页面的快速提示和自定义提示词帮助文本过时——引用只支持 Gemini、60+ 语言等旧信息
- **更新内容**：
  - `QuickTipsContent`：反映 8 个提供商、30 种语言、批量并发、术语表注入、AI 评估投票
  - `CustomPromptSyntaxHelp`：变量说明从长段落改为结构化列表
  - `TipAPIKey/TipModels/TipLanguages`：同步更新
  - `ProfileGlossaryHelp`：修复英文示例（"Jedi = Jedi" → 无意义示例）
- **本地化遗漏修复**：`ProfileEditorTitle` 在 `ApplyLocalization()` 中遗漏

### 目标语言名称本地化

- **问题**：设置中目标语言下拉框显示英文原名（如 "Türkçe"、"日本語"），不随界面语言变化
- **修复**：
  - `LocalizationManager` 中为 30 种目标语言添加中英文 `Lang_*` 映射
  - `SettingsWindow.xaml.cs` 新增 `RebuildTargetLanguageComboBox()` 方法，动态生成选项："🇹🇷 土耳其语 (Türkçe)"
  - `SettingsWindow.xaml` 移除硬编码的 30 个 `ComboBoxItem`

### 文档全面更新

- `README_zh.md`：修正过时的 AI 提供商（从 OpenAI/Claude 更新为 8 个实际提供商）、删除已废弃功能、更新模型列表和翻译流程
- `HANDOVER.md`：补充译文合并、清空缓存、缓存统计修复的 FAQ；已知问题表新增"状态"列
- `DEVELOPMENT_LOG.md`：补充本日记录

---

### 术语表系统重构：从"对照表"升级为"术语表"

- **背景**：原有两套独立的术语系统——`GlossaryManager`（对照表）和 `ExpertProfile.Glossary`（专家配置术语）——维护成本高且功能受限
- **统一方案**：将两套系统合并为统一的 `GlossaryManager`，支持完整元数据（分类、标签、状态、时间戳）：
  - `GlossaryTerm` 新增字段：`Category`、`Status`（pending/confirmed/rejected）、`Tags`、`CreatedAt`、`UpdatedAt`
  - 从专家配置合并术语时自动使用配置名作为分类
  - 向后兼容旧版 `translation_dictionary.json` 格式
- **术语管理面板**：专用 `GlossaryWindow` 窗口，支持：
  - 新增 / 编辑 / 删除术语（`TermEditDialog` 内联对话框）
  - 搜索过滤（按 English / Chinese / Tags / Category）
  - 分类和状态筛选（`CategoryFilter`、`StatusFilter` 下拉框）
  - CSV / JSON 导入导出（`ImportCsv` / `ImportJson` / `ExportCsv` / `ExportJson`）
  - 从专家配置合并术语（`MergeFromProfile` → `ProfileSelectDialog`）
  - 术语冲突检测（`DetectConflicts` → `ConflictDialog`）
  - 状态颜色编码（绿色 = 已确认，橙色 = 待审核，红色 = 已拒绝）
- **新增本地化键**：40+ 条术语表相关的中英文本地化字符串

### 翻译流程重构：TranslationOrchestrator 服务

- **问题**：`MainWindow.xaml.cs` 中散布大量翻译业务逻辑（prompt 构建、API 调用、响应解析、缓存/术语集成）
- **方案**：创建 `TranslationOrchestrator` 服务，集中管理翻译流程：
  - `CreateBatches()` — 基于 token 估算的智能分批
  - `TranslateBatchAsync()` — 核心翻译方法，串联"术语匹配 → 缓存检查 → prompt 构建 → API 调用 → 响应解析 → 缓存写入"完整流程
  - `BuildPrompt()` — 支持专家上下文、术语上下文、中文源检测
  - `BuildGlossaryContext()` — 从术语表中提取当前批次相关术语注入 prompt
  - `ParseResponse()` / `ParseFallbackResponse()` — 三级回退解析策略
- **效果**：MainWindow 代码减少约 230 行，翻译逻辑从 UI 层完全解耦

### 线程安全修复

- **问题**：`_glossaryHits`、`_cacheHits`、`_apiCalls` 三个计数器在异步翻译任务中被并发修改
- **修复**：在 `MainViewModel` 中使用 `Interlocked.Increment()` 实现线程安全的计数方法：
  - `IncrementGlossaryHits()` → `Interlocked.Increment(ref _glossaryHits)`
  - `IncrementCacheHits()` → `Interlocked.Increment(ref _cacheHits)`
  - `IncrementApiCalls()` → `Interlocked.Increment(ref _apiCalls)`
- `TranslationOrchestrator` 通过 `Action<int>` 委托回调节点，调用 ViewModel 的线程安全方法

### 术语表窗口崩溃修复

- **问题**：`GlossaryWindow` 打开即崩溃（栈溢出），原因是 `PopulateFilterCombos()` 中修改 `CategoryFilter` 和 `StatusFilter` 时触发 `SelectionChanged` 事件导致无限递归
- **修复**：添加 `_suppressFilterEvents` 守卫标志：
  ```csharp
  _suppressFilterEvents = true;
  try { PopulateFilterCombos(); }
  finally { _suppressFilterEvents = false; }
  ```

### 对话框 DialogResult 修复

- **问题**：`TermEditDialog` 和 `ProfileSelectDialog` 未正确设置 `DialogResult`，导致 Add / Edit / Merge 按钮点击后静默失败
- **修复**：在保存/合并按钮的 Click 事件中添加 `DialogResult = true` / `DialogResult = false` 并调用 `Close()`

### 事件订阅泄漏修复

- **问题**：`SettingsWindow`、`InputDialog`、`FileTypeDialog` 订阅 `LocalizationManager.LanguageChanged` 但从未取消订阅，导致窗口关闭后内存泄漏
- **修复**：在各窗口的 `Closed` 事件中添加取消订阅逻辑：
  ```csharp
  Closed += (_, _) => LocalizationManager.LanguageChanged -= ApplyLocalization;
  ```

### 代码去重

- **问题**：`GetCacheKey` 在 `MainWindow`、`AiTranslationService`、`ConfigService` 中各有独立实现
- **修复**：统一为 `ConfigService.GetCacheKey()` 单一实现
- `SyncEntriesToCache()` 也已统一到 `ConfigService` 中

### 项目审计

- 由 @架构设计师 + @代码审查员 对当前项目进行全面审计
- 识别出 **10 个问题**（2 个严重、3 个高、3 个中、2 个低）：
  - **严重**：`GlossaryManager` / `ExpertProfileManager` / `TranslationEvaluator` 缺少接口
  - **严重**：MainWindow 与 ViewModel/Service 存在大量重复业务逻辑
  - **高**：多线程不安全的 `Dictionary` Cache 和 `Queue` RecentRequests
  - **高**：`HasChineseChars` / `SaveTranslationProgress` / `RestoreTranslationProgress` 多处重复
  - **中**：术语表状态筛选显示原始英文值、HttpRequestMessage 未 Dispose、空 catch 块
- 结论：架构演进方向正确（MVVM + 接口注入），但过渡期残影需要清理

### 文档更新

- 更新 `DEVELOPMENT_LOG.md` 补充当日开发记录
- 创建 `HANDOVER.md`（项目交接文档），涵盖架构、服务说明、数据流、配置、已知问题、构建流程


## 2026-07-28

### 批量翻译解析修复

- **问题**：AI 返回不规范的 JSON（如 `{index` 与译文内容混排），导致批量翻译结果错行
- **修复**：
  - `ParseBatchTranslationResponse` 改造为三级回退策略：JSON 提取 → 正则匹配 → 逐行过滤
  - 新增 `ParseFallbackResponse` 方法，逐一处理各类异常格式
  - 第三级回退中过滤掉含 `{`、`}`、`index` 的无意义行
- **提示词强化**：`PromptTemplates.cs` 添加两条规则——禁止 JSON 外输出、强制双引号转义

### 设置弹窗优化

- **问题**：每次打开设置弹 API 提醒，不填 API 不能保存
- **修复**：移除 `AiProviderComboBox_SelectionChanged` 的强制提示和 `OkButton_Click` 的 API/Model 强制校验

### 项目对比分析：PolyTranslate vs XML AI Translator

与开源项目 **PolyTranslate**（Python/CustomTkinter，666 测试/93% 覆盖率，GitHub Actions CI/CD）进行横向对比。

#### 技术栈对比

| 维度 | PolyTranslate | XML AI Translator（我方） |
|------|---------------|--------------------------|
| 语言 | Python 3.10+ | C# .NET 8.0 |
| GUI 框架 | CustomTkinter | WPF |
| 架构模式 | 插件化 + 工作流 | MVVM + 服务层 + 依赖注入 |
| 测试覆盖 | 666 个测试，93% 覆盖率 | 无 |
| CI/CD | GitHub Actions（lint/test/release） | 无 |

#### PolyTranslate 的优势（我方缺失）

| 功能 | 说明 |
|------|------|
| **9 个翻译服务** | DeepL、Google、Yandex（免费）、OpenAI、Claude、Groq、OpenRouter、ChatGPT Proxy、LocalAI；其中 3 个免费无需 API Key |
| **11 种文件格式** | TXT、PDF、DOCX、PPTX、XLSX、CSV、HTML、Markdown、Ren'Py、SRT 字幕、ASS/SSA 字幕 |
| **CLI 模式** | 完整命令行界面，支持管道 I/O、JSON 输出、脚本化自动化 |
| **AI 翻译评估** | 自动评分（0-10）、详细解释、AI 改进建议 |
| **多代理投票** | 多个 AI 服务对同一译文投票，加权共识选出最优结果 |
| **导出格式** | DOCX/PDF/XLIFF 带格式导出；TMX 标准交换格式兼容 CAT 工具 |
| **流式翻译** | LLM 输出实时预览，大文本体验更流畅 |
| **插件系统** | Python entry points，无需修改主代码即可扩展翻译服务 |
| **Ren'Py 上下文** | 游戏感知翻译，自动提取角色/场景上下文 |
| **分块去重** | 并行模式下相同文本段每服务只翻译一次 |

#### 我方优势（PolyTranslate 缺失）

| 功能 | 说明 |
|------|------|
| **批量翻译优化** | 专为 XML 键值对结构设计，单次 API 调用处理 5-20 条，成本降低 90%+ |
| **专家配置文件** | 针对不同游戏/项目的术语规则和风格指南注入 |
| **翻译进度保持** | 意外退出后可恢复翻译进度，不丢失已完成工作 |
| **智能速率限制** | 各模型独立限制参数，自动计算最优请求间隔 |
| **WPF UI 体验** | Material Design 风格，流畅动画，实时进度控制（暂停/恢复/停止） |
| **部署便利性** | 自包含单 EXE 发布，无需安装 Python 运行时 |
| **实时活动日志** | 终端风格带时间戳日志，方便排查问题 |
| **XML 本地化专业** | 专为游戏 Excel Spreadsheet XML 格式优化 |

#### 结论

- 两个项目定位不同：PolyTranslate 是通用文档翻译工具，我方是游戏 XML 本地化专业工具
- **如果我方需要通用能力（CLI/多格式/插件）** → 可参考 PolyTranslate 的设计
- **如果 PolyTranslate 需要游戏本地化能力** → 可参考我方的批量翻译和专家配置
- **建议**：保持差异化定位，深耕游戏本地化细分领域；参考对方补充测试覆盖和 CLI 能力

---

## 2026-07-27

### 架构重构：MVVM 模式 + 服务接口

- **创建 `Interfaces.cs`**：定义 `IAiTranslationService`、`IXmlRepository`、`IConfigService` 三个核心接口
- **服务类实现接口**：`AiTranslationService`、`XmlRepository`、`ConfigService` 分别实现对应接口
- **MainViewModel 创建**：实现 `INotifyPropertyChanged`，将业务状态（条目集合、缓存命中、API 调用、翻译状态等 30+ 字段）迁移至 ViewModel
- **MainWindow 解耦**：改为依赖接口 `IAiTranslationService`/`IXmlRepository`/`IConfigService`，而非具体实现
- **翻译逻辑委托**：`TranslateSingleAsync` 纳入 `AiTranslationService`，删除 MainWindow 中 5 个冗余翻译方法

### 架构重构：God Class 拆分（前期）

- 从 MainWindow.xaml.cs 提取三个核心服务，减小约 800 行：
  - `AiTranslationService.cs` — AI API 通信、速率限制、费用计算
  - `XmlRepository.cs` — XML 文件读写
  - `ConfigService.cs` — 配置管理、缓存持久化
- 抽取工具类：`CsvHelper.cs`、`PromptTemplates.cs`

### 编译与语言 Bug 修复

- **编译失败**：`InitializeComponent()` 执行时控件事件触发，但 `_viewModel` 未初始化导致 `NullReferenceException`。在 `ExpertProfileCombo_SelectionChanged` 和 `BatchSizeTxt_TextChanged` 添加 null 防护
- **界面变成英文**：重构后 `MainViewModel.LoadConfig()` 未设置 `LocalizationManager.CurrentLanguage`，修复后中文界面正常显示

---

## 2026-07-26

### 中文本地化完善

- 清理项目中的残留硬编码英文字符串
- 删除俄语、日语等多余 UI 语言资源，仅保留中英文
- "快速保存"（Quick Save）等遗漏按钮汉化修复
- 多轮本地化审查：`LocalizationManager.InitializeTranslations()` 确保所有 UI 文本不遗漏

### UI 修复

- **筛选框对齐**：将"共 X 条"移到"翻译数据"旁，清除筛选按钮移到左边
- **删除多余控件**：清理冗余的翻译选项按钮
- **列分隔线修复**：键、原文、译文之间恢复竖线分隔
- **按钮颜色恢复**：回退部分过度改动的 UI 样式
- **缓存对照表图标去重**：修复图标重复绑定问题
- **删除无用的 Checkbox**：点击即崩溃的方框控件移除

### 设置窗口问题修复

- 模型配置随供应商切换不更新 — 传递正确的 `selectedProvider` 参数
- Token 时间与费用计算错位 — 绑定当前选择模型的限制参数

---

## 2026-07-25

### 翻译文件管理重构

- **文件夹结构标准化**：`4.0` 和 `5.0_ai` 翻译目录按照 mod 规范（`Data/Xml/`、`Data/Text/xml/`）重组
- **字体设置**：
  - 尝试 SimHei（黑体）后因显示问题回退 Microsoft YaHei（微软雅黑）
  - 单位介绍、星球介绍的文本背景框宽度调整（Max_Text_Width ~700+）
  - 与原始 mod 文件逐项对比，确保加粗样式匹配
- **`tihuan` 目录删除**：统一使用 `5.0_ai` 作为翻译输出目录
- 修复 4.0 文件夹中个别文件字体设置遗漏的问题

### Arial 字体调整

- 对比 `1770851727/Data/Xml` 原始文件确认字体相关设置
- 翻译文本统一调整为 Arial 字体（匹配原 mod 风格）
- 确认两个 mod（3689306867 和 3767659376）的字体兼容性

---

## 2026-07-24

### UI 全面重新设计

- 委托 UX 架构师 + UI 设计师 + Persona 走查专家联合评审
- 采用 Material Design 风格：卡片式布局、柔和的色彩搭配
- 活动日志改为明亮荧光绿色（终端风格）
- 整体视觉避免纯白刺眼，增加适度的色彩层次

### 回退与调整

- 初次大改效果不佳，全部回退
- 基于回退后的代码进行渐进式调整，保持功能稳定的同时改进视觉

---

## 项目早期（2026-07-23 及之前）

### 核心功能开发
- **初始版本**：Google Gemini 单一提供商支持
- **多提供商扩展**：新增 OpenAI、Claude、OpenAI 兼容格式 API
- **批量翻译引擎**：基于 Token 限制的智能分批策略，每次 API 调用处理 5-20 条
- **翻译缓存系统**：基于原文哈希的缓存，避免重复翻译
- **速率限制**：各模型独立限制参数（次/分钟、次/天、Token/分钟），自动计算最优请求间隔
- **费用估算**：根据模型定价和字符数实时计算 API 费用
- **专家配置文件**：可自定义的领域翻译规则和风格指南
- **术语表管理**：游戏术语词典，确保翻译一致性

### 数据管理
- **XML 导入/导出**：支持 Excel Spreadsheet XML 格式读写
- **翻译进度保持**：意外退出后自动恢复进度
- **翻译备份**：自动生成备份文件

---

## 当前架构一览

```
SimpleXmlEditor/
├── Services/                       # 服务层
│   ├── AiTranslationService.cs     # IAiTranslationService ✓ — AI 翻译核心（8 个提供商）
│   ├── ConfigService.cs            # IConfigService ✓ — 配置与缓存管理
│   ├── Interfaces.cs               # 服务接口定义（3 个接口）
│   ├── TranslationEvaluator.cs     # ✗ 无接口 — AI 翻译质量评估与多代理投票
│   ├── TranslationOrchestrator.cs  # ✗ 无接口 — 翻译流程编排（prompt/API/cache/glossary）
│   └── XmlRepository.cs            # IXmlRepository ✓ — XML 数据访问
├── ViewModels/
│   └── MainViewModel.cs            # 主窗口 ViewModel（INotifyPropertyChanged，30+ 字段）
├── Localization/
│   └── LocalizationManager.cs      # 中英文 UI 本地化（200+ 键值对）
├── Dictionary/
│   ├── CsvHelper.cs                # CSV 文件解析/转义工具
│   └── GlossaryManager.cs          # ✗ 无接口 — 统一术语表管理（CRUD/导入导出/冲突检测）
├── ExpertProfiles/
│   ├── ExpertProfile.cs            # 专家配置数据模型
│   └── ExpertProfileManager.cs     # ✗ 无接口 — 专家配置生命周期管理
├── MainWindow.xaml/.cs             # 主界面（含残留业务逻辑待迁移）
├── GlossaryWindow.xaml/.cs         # 术语表管理窗口（含内联对话框类）
├── SettingsWindow.xaml/.cs         # 设置界面（含专家配置编辑器）
├── InputDialog.xaml/.cs            # 通用双输入对话框
├── FileTypeDialog.xaml/.cs         # 文件类型选择对话框
├── PromptTemplates.cs              # AI 提示词模板
├── App.xaml/.cs                    # 应用入口
└── SimpleXmlEditor.csproj          # .NET 8.0 WPF 项目文件
```

注：✓ 表示已实现接口，✗ 表示缺少接口（见审计报告问题 #1）

---

## 待完成事项

### 高优先级（P0-P1）
- [ ] **线程安全**：`ConfigService.Cache` 改用 `ConcurrentDictionary`，`AiTranslationService.RecentRequests` 改用 `ConcurrentQueue`（审计 #3）
- [ ] **接口补全**：为 `GlossaryManager`、`ExpertProfileManager`、`TranslationEvaluator` 抽取接口（审计 #1）
- [ ] **消除重复**：删除 MainWindow 中的 `LoadConfig`/`SaveConfig`/`SaveTranslationProgress`/`RestoreTranslationProgress`/`SyncEntriesToCache`，统一走 ViewModel/ConfigService（审计 #2）

### 中优先级（P2）
- [ ] **代码去重**：提取 `HasChineseChars` 为公共扩展方法（审计 #4）
- [ ] **术语表本地化**：状态筛选框显示本地化文本（审计 #6）
- [ ] **资源泄漏**：`HttpRequestMessage` 添加 `using` 或 `Dispose`（审计 #7）

### 低优先级（P3）
- [ ] 空 catch 块添加错误日志（审计 #8）
- [ ] 删除 MainWindow 中未使用的 `LoadConfig()` 方法（审计 #9）
- [ ] SettingsWindow AI provider 刷新逻辑去重（审计 #10）

### 功能规划
- [ ] 单元测试 / 集成测试覆盖
- [ ] 更多 XML 格式支持（如 XLIFF）
- [ ] CLI 命令行模式
- [ ] GitHub Actions CI/CD 流水线
- [ ] macOS 跨平台支持探索

---

## 经验教训

1. **架构先行**：早期"上帝类"（God Class）导致后期维护成本剧增，MVVM + 服务层提前规划可避免
2. **渐进式重构**：大幅改动的风险高，采用接口抽象 + 逐步迁移更安全
3. **语言资源管理**：硬编码字符串是长期维护隐患，应从项目初期就使用 LocalizationManager
4. **AI 返回不稳定**：提示词约束不是 100% 可靠，解析逻辑必须有健壮的回退策略
5. **编译排查**：.NET WPF 项目中，`InitializeComponent()` 执行时的 null 引用是常见的陷阱，需加防护
6. **渐进式重构的边界**：`TranslationOrchestrator` 虽然抽取了翻译流程，但 MainWindow 仍保留了 LoadConfig/SaveConfig 等副本——重构必须追踪到所有调用点，不能只完成任务的一半
7. **对话框 DialogResult 陷阱**：WPF 中以代码创建的子窗口（非 XAML）需要显式设置 `DialogResult = true/false`，否则 `ShowDialog()` 返回 null，导致调用方误判
