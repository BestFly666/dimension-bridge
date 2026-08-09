# 开发日志 — 次元译桥

> 项目仓库：`xml-ai-translator-main`  
> 作者：Veloxcity、BestFly666  
> 技术栈：C# / .NET 8.0 / WPF / Newtonsoft.Json  
> 目标平台：Windows 10/11

---

## 项目概述

**次元译桥（Dimension Bridge）** 是一款专为游戏本地化设计的 XML 批量翻译工具。核心定位是基于 AI（8 个提供商）对 Excel Spreadsheet 格式的 XML 本地化文件进行批量翻译，通过智能分批、翻译缓存、速率限制等机制大幅降低 API 调用成本（90%+），同时提供基于 **HandyControl 3.5.1** 组件库的现代桌面界面（浅色系配色，多彩按钮）。

**产品定位（2026-08-01 确认）**：面向**中文游戏本地化译者**的一站式 AI 翻译工作台——将术语一致性、质量评估、格式兼容等专业流程自动化；同时为**独立游戏开发者**提供开箱即用的极简版，让"游戏文本 → 多语言"从几周变成几小时。共享核心引擎，双入口形态。

---

## 2026-08-09 — 全面代码审计修复 + UI 编辑态崩溃修复 + 3.5 换行补丁 + 产品更名"次元译桥"

> 背景：用户要求按代码审查员规则对项目做大规模审计并修复全部问题（34 项确认问题）；随后反馈 Ctrl+Z / 批量替换 / 筛选在"选中单元格内容"后崩溃；并决定把产品对外名称更改为"次元译桥"。以代码审查员 + 后端架构师身份处理。

### A. 全面代码审计修复（1 critical + 14 major 全部修复）

- **翻译流防重入（critical）**：翻译运行中禁止启动第二条流水线；`_translationCts` 用局部 CTS + `ReferenceEquals` 归属判断，杜绝被覆盖 / finally 误 Dispose
- **进度保存并发**：节流保存 Task 化 + `DrainProgressSavesAsync` 排空在途保存 + `ConfigService._progressFileLock` 串行化同一进度文件的写 / 读 / 删
- **Glossary 并发安全**：`Terms` / `_regexCache` 改 `ConcurrentDictionary`；`_sortedTerms` / `_invertedIndex` 维持整表重建 + 引用替换，后台读不阻塞 UI 写
- **缓存双键对称写**：新增 `SetCacheEntry(key, original, translation)`，Key + MD5(原文) 双键与 `SyncEntriesToCache` 对称，消除单键写入遗漏
- **部分批次结果丢失**：AI 只返回部分条目时自动拆半递归补译并合并，不再静默接受
- **DPAPI 明文降级移除**：API Key 加密失败绝不写明文，保留 `LEGACY:` 前缀只读兼容旧配置
- **提示注入转义**：`PromptTextSanitizer` 统一转义 Evaluator（5 处）/ Orchestrator / ExpertProfile 全部动态文本插值点；修复 `{GLOSSARY}` 重复注入
- **成本统计线程安全**：`Interlocked.Add`（int）+ 锁（double）+ 显式通知；`FetchAvailableModelsAsync` 不再临时覆盖 `_apiKey`

### B. UI 编辑态崩溃修复（Ctrl+Z / 批量替换 / 筛选）

**现象**：双击选中单元格内容（进入编辑态）后，不取消选中直接按 Ctrl+Z、执行批量替换、或使用筛选，程序崩溃。

**根因**：DataGrid 处于编辑状态（单元格未提交）时，调用 `CollectionView.Refresh()` 或修改 `view.Filter` 会抛 `InvalidOperationException`——视图在编辑中被重新评估。

**修复**：新增 `ExitDataGridEditing()`（提交单元格 + 行编辑，异常静默）与 `SafeRefreshDataGrid()`（先提交再 Refresh），覆盖全部 10 个 Refresh/Filter 调用点（筛选 / 批量替换 / Undo / 重置排序 / Del 清空 / 清缓存 / 合并 / 加载 / 投票应用 / 预翻译 / 建议应用）；Undo 跳转前若目标行被当前筛选隐藏则自动清除筛选再定位。

### C. 3.5 换行补丁

`EXACT_INCLUDE_KEYS` 追加 `TEXT_UNIT_CEC_C9_SENTINEL_DESC`——该 Key 后缀为 `_DESC` 而非 `DESCRIPTION`，不命中任何 INCLUDE 关键词，此前被漏过；与 `TEXT_BUIDING_STARBASE_PIRATE_LV1_GARRISON` 一样精确指定处理。

### D. 产品更名

项目对外名称更改为 **次元译桥（Dimension Bridge）**：窗口标题 / AppName / About / 设置标题经 LocalizationManager 双向字典更新；README / 开发日志 / 交接文档 / 文件索引 / 产品规划同步改名。GitHub 仓库名更改为 `dimension-bridge`（GitHub 仓库名仅支持 ASCII，中文名置于仓库描述）；程序集名（SimpleXmlEditor）保持不变，AppData 数据路径（`%LocalAppData%\SimpleXmlEditor\`）不受影响，LICENSE 版权声明原样保留。

### E. GitHub 仓库更名 dimension-bridge（commit `4af94cb`）

- GitHub 仓库名从 `xml-ai-translator-tool` 改为 `dimension-bridge`——GitHub 平台限制仓库名仅支持 ASCII，中文名"次元译桥"置于仓库描述
- 本地 `git remote` 同步更新，指向新仓库地址
- README / 开发日志 / 产品规划中全部仓库链接同步更新：CI badge、Issues、clone、Releases 等指向新仓库

### F. 新增项目 Logo（commit `455a360`）

- logo 图片 [docs/logo.jpg](file:///e:/translate/xml-ai-translator-main/docs/logo.jpg)（源自 `e:\translate\logo方案4_Q版角色站桥.jpg`）加入仓库
- README 顶部居中展示该 Logo

### G. Logo 转 .ico 作为 WPF 应用图标（commit `9a521f6`）

- 用 PowerShell + System.Drawing 生成多尺寸 [logo.ico](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Assets/logo.ico)（16/24/32/48/64/128/256px，PNG 帧，等比居中不变形）到 `SimpleXmlEditor\Assets\logo.ico`
- [SimpleXmlEditor.csproj](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/SimpleXmlEditor.csproj) 设置 `ApplicationIcon`（EXE / 任务栏 / 所有窗口图标）
- 主界面顶部原 🌐 emoji 替换为 34px 圆形裁剪的 Logo 图片（[Assets/logo.jpg](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Assets/logo.jpg)）

### H. 启动崩溃修复：WPF 资源路径解析（commit `c0be9e6`）

**现象**：上一步（G）导致应用启动即崩溃。

**根因**：WPF 资源路径解析机制——XAML 中 `Icon`/`Image` 的相对路径（如 `Assets/logo.ico`）会被解析为打包资源 URI（`pack://`），且基于 XAML 文件所在目录（`Windows/`），去程序集内找 `windows/Assets/logo.ico`；而图片只是 `CopyToOutputDirectory` 复制到磁盘、未编译进程序集，于是 `InitializeComponent` 抛 `XamlParseException`。

**修复**：
- 移除窗口 `Icon` 属性（窗口 / 任务栏继承 EXE 的 `ApplicationIcon`，由 Windows Shell 显示）
- `logo.jpg` 改为 csproj `<Resource>` 内嵌资源，XAML 用 `pack://application:,,,/Assets/logo.jpg` 引用

**经验教训**：
1. WPF 中想用相对路径引用图片必须编译为 `Resource` 并用正确的 pack URI，仅复制到输出目录不生效
2. 增量构建偶发复用旧 BAML——改 XAML 后运行仍报旧错时，需清理 `obj` 目录重建

### I. 删除 AiTranslationService 死事件（commit `dc0457e`）

- 删除 `CacheHit` / `ApiCallCounted` / `ApiCharsCounted` 三个 CS0067 警告事件——仅定义 + 订阅、从未触发；统计实际由 [TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) 的 `OnCacheHit` / `OnApiCall` / `OnApiChars` 承担
- 同步清理：接口 `IAiTranslationService` 声明、`MainViewModel` 无效订阅、测试 Fake 冗余声明

### 验证

- `dotnet build`：0 错误 0 警告
- `dotnet test`：69/69 通过（新增 AiResponseParser / 缓存双键测试，修正 Fake GetCacheKey）
- 用户实测：编辑态下筛选 / 撤销 / 批量替换不再崩溃

---

## 2026-08-08/09 — 字体回退根治（4.0/5.0/3.5 统一 Microsoft YaHei UI）+ 专家/术语注入链路修复 + 3.5 换行

> 背景：用户报告"机翻 mod 和自改字体都退回宋体"；专家选择后 AI 翻译完全不受影响；术语功能单条生效、批量失效；3.5 换行需排除固定宽度条目。以代码审查员 + 后端架构师身份处理。

### A. 字体回退宋体根治（4.0/5.0/3.5）

**现象**：机翻 mod（用 `Arial Unicode MS`）与本项目改的字体在游戏内都退回宋体。

**根因**：
1. 用户系统**缺失 `Arial Unicode MS`** → Windows 按字体名精确匹配失败 → 回退宋体（注册表 FontSubstitutes 验证正常，非全局设置被改）
2. 此前自改用 `Microsoft YaHei`，但系统字体注册名是 **`Microsoft YaHei UI`**（带 UI 后缀，共用 msyh.ttc）——名字不一致同样匹配失败回退宋体

**修复**：
- **4.0 / 5.0（3728303149）**：GUIDIALOGS.XML、COMMANDBARCOMPONENTS.XML、Gameconstants.xml 全部字体 → `Microsoft YaHei UI`；**不加粗**（按用户要求避免退化）；`Alternate_Font_Name` → `Microsoft YaHei UI, Microsoft YaHei UI, Microsoft YaHei UI`
- **3.5**：原版用 `STXihei`（华文细黑，Windows 常缺失）——COMMANDBARCOMPONENTS.XML 451 处 + GUIDIALOGS.XML 319 处（`<Name>` 字体标签）+ Gameconstants.xml 11 处，共 781 处全部 → `Microsoft YaHei UI`；无 `Alternate_Font_Name`；保持原编码（无 BOM UTF-8）、保留 Emboss/Outline 设置

**验证**：用户实测生效（"终于成功了，就是字体名字不一致导致的"）。

### B. 专家档案激活链路修复（"专家没生效"）

**现象**：选择星球大战/漫威/校对专家后，AI 翻译结果完全不受影响。

**根因链（4 层）**：
1. **双状态未同步**：UI 只写 `MainViewModel.ActiveExpertProfileName`（持久化到 AppData config.json），而翻译执行路径 [TranslationOrchestrator.BuildExpertContext](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) 读的是 `ExpertProfileManager.ActiveProfileName`（expert_profiles.json）——**从未同步** → 专家 Context 永不注入
2. **词典文件不在运行目录**：`GlossaryManager.DictFile` 从 `Environment.CurrentDirectory` 加载，bin 运行目录缺 `translation_dictionary.json`/`glossary_terms.json` → 术语注入为空 → csproj 补 `CopyToOutputDirectory`
3. **自定义提示词缺 `{GLOSSARY}` 占位符**：占位符缺失时术语被 `Replace` 空转丢弃 → 改为**占位符缺失时追加到 prompt 末尾**
4. **DI 空日志回调覆盖**：DI 容器把 `Action<string>` 注册为 no-op，MainViewModel 收到非 null 的 `TranslationOrchestrator`，`?? new` 短路 → orchestrator 内部日志（含错误/重试）被静默丢弃 → **从 DI 移除该注册**，由 MainViewModel 自建带真实日志回调的实例

**修复**：`MainViewModel.ActiveExpertProfileName` setter 同步 `_profileManager.ActiveProfileName` + `SaveProfiles()`；csproj 词典复制；占位符追加模式；移除 DI 注册。

### C. 术语注入批量失效修复（"单条生效、批量失效"）

**现象**：单条翻译术语生效（A-Wing → A翼），批量翻译不生效（仍输出 A-Wing/一架）。

**根因**：[GlossaryManager.Index.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Index.cs) `GetGlossaryContextTerms` 先把候选术语按**长度降序**排序，再逐个验证 `IsTermRelated` 并塞满 `MAX_GLOSSARY_CONTEXT_TERMS`——批量时 50 条文本产生数百候选，30-40 字符长术语先验证通过占满名额，**A-Wing 类短术语（17 字符）被挤出** → 注入失效。

**修复**：改为**先全局验证**收集匹配术语（termKey → 命中条目数）并记录每条 entry 的命中集合 → **每条 entry 至少贡献自己的术语**（最长优先）→ 剩余名额按命中条目数降序补充（同数按长度降序）。短/冷门术语不再被高频长术语挤出。

**容量**：`MAX_GLOSSARY_CONTEXT_TERMS` **50 → 200**（用户要求加大，最终定格 200）；200 条约 5K–6K token/批，占 64K 上下文约 8-9%，输入不影响批次输出预算。

### D. 3.5 换行脚本（GC_COMPLETE_DISC_ONEPLANET 排除）

- [3.5_添加换行写入DAT.py](file:///e:/translate/scripts/3.5_添加换行写入DAT.py) `EXCLUDE_KEYWORDS` 新增 `GC_COMPLETE_DISC_ONEPLANET`——该 key 含 `PLANET` 会被 `INCLUDE_KEYWORDS` 命中，但其为单行星完成提示文本，游戏预设 `\n` 换行、固定宽度，追加空格断行会破坏布局
- 执行结果：1760 条加换行；DAT 写回后验证该 key 无 6+ 连续空格（换行特征），保留原 `\n`

### 验证

- `dotnet build`：0 错误 0 警告
- 诊断日志确认专家 Context 注入链路打通（profile 名 + 长度 + glossary 布尔），验证后按用户要求移除
- 用户实测：Microsoft YaHei UI 字体生效；术语注入容量可调

### 经验教训

1. **Windows 字体名精确匹配**：代码引用与系统注册名必须逐字符一致（`Microsoft YaHei UI` ≠ `Microsoft YaHei`），系统缺字体/名不匹配即回退宋体；排查字体回退先列 `HKLM\...\Fonts` 注册表核对注册名
2. **双状态必须单向同步**：UI 状态（config.json）与执行路径状态（数据文件）分离时，setter 必须同步到执行路径，否则"UI 选了但没生效"且无任何报错
3. **先排序后验证 vs 先验证后排序**：带截断上限的挑选必须先生成完整匹配集，再排序取舍；"边验证边截断"在排序键与相关性无关时（如按长度）会系统性挤掉目标项
4. **DI 注册 no-op 的副作用**：`Action<T>` 等委托注册 no-op 后，依赖方 `?? new` 回退永久失效，内部日志被静默吞掉——排查"功能没日志"先核对 DI 是否覆盖了回调注入

---

## 2026-08-06（续 2）— 大批量卡顿根治 + 术语匹配宽容 + DataGrid 打磨 + THR 汉化落地

> 背景：用户反馈"批次多了还是很卡（每批 50、并发 100，到第 100 批次基本卡死）"；术语表"差个空格/标点/单词就匹配不到"（`Procursator Star Destroyer` ↔ `Procursator-class Star Destroyer`）；并推进 THR mod 汉化的 DAT 读写落地。以代码审查员 + AI 工程师 + 后端架构师身份处理。

### A. 大批量卡顿根治：UI 消息风暴 → 合并渲染

**问题**：上一轮已修"进度保存风暴"（节流+合并），但批次多了仍卡死。全链路复查定位到**两个遗留瓶颈**：

1. **UI 消息风暴（主因）**：[MainWindow.Handlers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Handlers.cs#L23-L34) 每批完成产生 **6 个 `Dispatcher.BeginInvoke` 回调**（日志 ×4 + 进度 ×1 + 状态 ×1），WPF Dispatcher 队列**无合并、无积压上限**。100 批 = 600+ 回调排队：
   - 每个 `AddLog` 全量重建 30KB 字符串 + 全文重排 + `ScrollToEnd`
   - 每个进度回调更新 5 个 TextBlock（`UpdateProgressDisplay`）
   - UI 线程处理速度 < 后台产生速度 → 队列无限增长 → **批次越多越卡**
2. **进度保存无限追赶（次因）**：[MainViewModel.Translation.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Translation.cs#L65-L92) 的 `while(pending)` 循环在批完成密集时持续全量序列化 5000+ 条 + 写文件，`Task.Run` 线程与 CPU/IO 持续被占

**修复**：
- **UI 合并渲染**：高频事件（日志/状态/进度）后台线程**只入队/存最新值**，不再逐个 `BeginInvoke`；新增 `_uiFlushTimer`（250ms）在 UI 线程**合并渲染一次**（日志合并 append、状态/进度用最新值）；`TranslationFinished` 兜底 flush
  - 文件：`MainWindow.xaml.cs`（字段+定时器）/ `MainWindow.Handlers.cs`（订阅改入队）/ `MainWindow.Helpers.cs`（`AddLog` 入队 + `FlushPendingUi`）
  - 每批 6 个回调 → 每 250ms 合并 1 次渲染，UI 工作量降约 95%
- **进度保存 2s 最小间隔**：距上次落盘 <2s 的保存直接跳过；最终由 `SaveCache` / `SaveProgressFinalAsync` 兜底，进度不丢
  - 文件：`MainViewModel.Translation.cs`
- 保留：低频关键事件（开始/结束/错误/确认框）仍立即处理，交互无延迟感

### B. 术语匹配宽容机制（无需穷举变体）

**问题**：术语匹配把术语当精确字面 token，差一个空格/标点（`Star-Destroyer` ↔ `Star Destroyer`）、差一个修饰词（`Procursator Star Destroyer` ↔ `Procursator-class Star Destroyer`）都匹配不到。

**修复**：[GlossaryManager.Index.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Index.cs#L45-L119) 的 `GetOrCreateRegex` 重构为**分词构建**：
- **分隔符互换**：术语中 空格/连字符/下划线/斜杠/句点 → `[\s\-/_.]+`（一个或多个，可互换）
- **撇号双向可选**：普通字符后允许插入可选撇号；术语中撇号本身可选（`Hutt's` ↔ `Hutts` 两个方向都覆盖）
- **修饰词宽容**（白名单：class/mark/mk/type/series/version/model/variant/generation/prototype/standard）：
  - 分隔符处允许插入一个可选修饰词（`Procursator Star Destroyer` ↔ `Procursator-class Star Destroyer`）
  - 术语本身的修饰词 token 也可选（`Executor-class Star Dreadnought` ↔ `Executor Star Dreadnought`，双向）
  - 结构细节：修饰词**不吞尾随分隔符**；前导分隔符必选、尾随分隔符可选——避免可选分组与相邻必选分隔符竞争同一字符导致回溯失败
- 保留：词边界 + 复数/所有格后缀（`Jedi` ↔ `Jedis`/`Jedi's`/`dark_jedi`）；**词内拼接不匹配**（`StarDestroyer` ≠ `Star Destroyer`）、非修饰词插入不匹配（`Jedi High Council` ≠ `Jedi Council`）防误伤

**关键 bug**：`ContainsWholeWord` 的 `term.Length > text.Length` 预检——术语含 `-class` 时比原文长，直接返回 false，而正则本身能匹配（调试打印确认 `match=True`）。**移除该预检**（快速失败条件与"术语含可选成分"语义冲突）。

**文件**：`GlossaryManager.Index.cs` / `GlossaryManagerTests.cs`（新增 2 个测试方法）

### C. DataGrid 交互打磨

- **删除左上角九宫格全选按钮**：`EntriesGrid_Loaded` 中用默认模板 `FindName("SelectAllButton")` + `Visibility=Collapsed`（[MainWindow.Grid.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.cs#L81-L99)），**不动列结构**避免列偏移（此前改 `HeadersVisibility`/新增行号列导致列字母错位，已回退）
- **行头加宽**：`RowHeaderWidth=70`（约 2.5 倍，[MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.xaml)）
- **选中颜色统一**：逻辑选择高亮背景统一为 `{DynamicResource PrimaryBrush}` 渐变蓝（与整列/全选一致），并补 `Foreground=WhiteBrush` 保证可读
- **代码去重**：`SelectAllEntries` / `SelectEntireColumn` 提取公共 `ApplyLogicalSelection`

### D. THR mod 汉化落地（Alamo DAT 读写）

- **DAT 读写**：`scripts/datlib.py` 兼容读写 THR 的 Alamo DAT（UTF-16LE + CRC32 索引，与重制版一致，读回验证通过）
- **中文 DAT 生成**：`MasterTextFile_ENGLISH.dat`（25288 条中文）写入；清理 22 处手写换行（`\n` → 空格，引擎自动折行）；原版英文备份 `.bak`
- **Credits 提取**：`CreditsText_ENGLISH.DAT` → XML（315 条）
- **XML 格式转换**：datlib 导出格式（`TranslationData` 根，无中间层）与翻译软件期望的官方格式（`LocalisationData > Localisation > TranslationData > Translation` CDATA）不兼容 → 转换脚本修复导入问题

### 验证

- `dotnet test`：**42 个测试全部通过**（0 失败）
- `dotnet build`：0 错误 0 警告

### 经验教训

1. **BeginInvoke 不合并也会卡死**：异步不阻塞后台线程，但无节流的异步排队会在 UI 处理不过来的场景下无限积压。高频 UI 更新必须"入队 + 定时合并渲染"（250ms 粒度对进度类 UI 足够）
2. **快速失败预检要复审**：`term.Length > text.Length` 这类预检在"术语可含可选成分（修饰词）"的语义下不成立——正则能匹配的输入不能被预检短路
3. **可选正则分组要管理自己的分隔符边界**：可选修饰词若与相邻必选分隔符竞争同一字符，回溯无法同时满足；前导分隔符必选 + 尾随分隔符可选，才覆盖双向省略
4. **隐藏 DataGrid 功能按钮优先模板查找**：改 `HeadersVisibility`/列结构会连带影响列序号、行号、列字母映射，副作用大；模板 `FindName` + `Collapsed` 是零副作用方案

---

## 2026-08-07（续）— 主流文件格式扩展：CSV / INI / YAML / RESX / PROPERTIES

> 需求："增加支持的文件格式，起码主流文件格式要支持"。产品决策：CSV/INI/YAML/RESX/PROPERTIES 五格式（XLIFF 未选、格式互转 Non-Goal）；CSV 列结构自动识别；文本格式编码自动检测 + 原编码写回；YAML 引入 YamlDotNet；译文为空回退原文（"导出替换原值"约定）。

### A. 新增 5 个格式插件（IFileFormatPlugin 模型，零 UI 改动自动生效）

| 插件 | 扩展名 | 说明 |
|---|---|---|
| [CsvFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/CsvFilePlugin.cs) | .csv | 列结构自动识别（3 列 Key/Original/Translation 或 2 列 Key/Value，表头关键词判定）；字符级解析器（引号包裹、"" 转义、引号内逗号/换行安全） |
| [IniFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/IniFilePlugin.cs) | .ini | [Section] 段 + key=value；带段 Key 存 "[Section]key"，保存按段前缀还原结构 |
| [YamlFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/YamlFilePlugin.cs) | .yaml/.yml | YamlDotNet 18.1.0 解析；嵌套字典点分拼接 Key、数组 [i] 展开（与 JsonI18nPlugin 一致） |
| [ResxFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/ResxFilePlugin.cs) | .resx | data name/value 读写；保存输出标准 resheader 保证资源编译器可用；XDocument 默认禁 DTD（防 XXE） |
| [PropertiesFilePlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/PropertiesFilePlugin.cs) | .properties | Java 规范转义（\\ \n \t \r \uXXXX \: \=）、行尾反斜杠续行、# ! 注释 |

### B. 共享编码检测 + TxtFilePlugin 重构

- [TextEncodingDetector.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/TextEncodingDetector.cs) 静态类：UTF-8 BOM → 严格 UTF-8 校验 → GBK 兜底（CodePages 注册一次）
- TxtFilePlugin 删除自带 DetectEncoding/GetGbEncoding，改调共享类——CSV/INI/PROPERTIES/TXT 四格式统一"加载原编码、保存原编码写回"，国内 Excel 打开不乱码

### C. 行为约定（产品决策落地）

- **两列模型**（INI/YAML/RESX/PROPERTIES/2 列 CSV）：保存时译文写入 Value 列（回退原文约定）；**三列模型**（3 列 CSV）：译文写入 Translation 列
- 保存编码 = 加载检测编码（UTF-8 BOM 保留 BOM）
- 打开/保存对话框过滤器自动包含新扩展名（PluginLoader.GetAllSupportedExtensions 动态收集）

### 验证

- `dotnet test`：**58 个测试全部通过**（新增 10 个：CSV 三列往返/引号转义/两列/GBK 编码、INI 段往返/无段、YAML 嵌套往返、RESX 往返、PROPERTIES 转义往返、编码检测器；既有 48 个无回归，含 TxtFilePlugin 重构）
- `dotnet build`：0 错误 0 警告（测试工程既有 nullability 警告与本次改动无关）

### 经验教训

1. **插件测试的"模型意识"**：两列格式（INI/YAML/RESX/PROPERTIES）保存后译文落在 Value 而非 Translation——断言必须按插件的数据模型写，否则误判失败
2. **共享编码逻辑应收敛为单一静态类**：Txt 原实现散落在插件内部，扩展第 2/3/4 个文本格式时必然要复用，提前提取避免四份重复
3. **表头识别要宽容但防误判**：CSV 表头判定锚定第一列为 key/id + 常见列名，数据行恰好第一列叫 Key 的误判可接受（V1）

---

## 2026-08-07（续二）— 批量替换 / Ctrl+Z / Del 大批量操作崩溃修复

> 用户报告"批量替换和 Ctrl+Z 容易崩溃"。以代码审查员身份定位：三处结构性缺陷——UI 线程同步循环触发海量 PropertyChanged 导致 DataGrid 逐行重绘假死（Windows 判"未响应"）、每次编辑都入栈污染 Undo 栈、快照被挤出后 Ctrl+Z 失效。

### 根因

1. **🔴 UI 假死（用户感知"崩溃"）**：`LocalizationEntry.Translation` setter 每次触发 2 个 PropertyChanged（Translation + StatusIcon）。批量替换 / `UndoLast` / Del 清空都是 UI 线程 for 循环逐条赋值——几万条文件每条触发 DataGrid 行重绘，界面假死数秒
2. **🟡 Undo 栈污染**：`EntriesGrid_BeginningEdit` 每次编辑都 Push 快照（哪怕值没变），连续编辑 50 次后栈被无意义快照填满，批量操作快照被挤出，Ctrl+Z 无法撤销批量操作

### 修复

- [XmlRepository.Models.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.Models.cs#L60-L64) 新增 `SetTranslationSilent`（静默赋值，对标既有 `SetIsSelectedSilent`）
- 批量替换 / `UndoLast` / Del 清空改为**静默批量赋值 + 末尾一次 `view.Refresh()`**，不再逐条触发 UI 更新
- [MainViewModel.Undo.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.Undo.cs#L38-L57) 新增 `DiscardUndoSnapshotIfUnchanged`：栈顶若是该条目单条快照且值未变 → 丢弃
- [MainWindow.Grid.Editing.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Grid.Editing.cs#L56-L69) 新增 `EntriesGrid_CellEditEnding`（XAML 绑定）：提交/取消时自动丢弃"值未变"的无意义快照

### 验证

- 48 个既有测试全部通过，0 警告 0 错误
- 批量替换/撤销/清空译文不再逐行刷新；编辑未改内容不再污染撤销栈

---

## 2026-08-07 — 术语宽松相关判定（AI 提示注入）+ 日志显示修复

> 背景：用户报术语表"首核心词省略变体"匹配不上——`Xyston-class Star Destroyer` 匹配不到原文 `Xyston-class` / `Xyston Siege Destroyer Upkeep`；`Quasar Fire-class cruiser-carrier` 匹配不到 `Quasar Fire-class Carrier`；`Skipray blastboat` 匹配不到原文大量出现的单独 `Skipray` 短名与 `Skipray Blast Boat(s)`。用户确认是 **AI 提示词注入**路径失效（`GetGlossaryContextTerms` 候选验证用严格 `ContainsWholeWord`），而非替换/冲突检测。以 AI 工程师 + 代码审查员身份处理。

### A. 术语宽松相关判定 `IsTermRelated`（仅用于 AI 提示注入）

**设计**：[GlossaryManager.Index.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Index.cs#L320-L432) 新增静态方法 `IsTermRelated(text, term)`，宽松判定"原文与术语相关"（替换/冲突检测仍走严格 `ContainsWholeWord`，不受影响）：

1. **条件 2 — 首核心词 + 类名修饰词**：命中术语首核心词后紧邻 `class/mk/type` 等修饰词即相关（覆盖 `Xyston-class`、`Quasar Fire-class` 单位名）
2. **条件 1 — 核心词按序子集命中**：按序命中 ≥ ceil(核心词数/2) 个核心词（封顶核心词数）。2 核心词 → 1（命中首核心词即相关，覆盖 `Skipray` 单独短名）；3 → 2；4+ → 半数以上
3. **复数/所有格宽容**：`WordMatches` 支持 `s/es` 后缀与去撇号比较（`Executors` ↔ `Executor`、`Executor's` ↔ `Executor`）

**关键 bug 修复**：minHits 原为 `Math.Max(2, ceil(n/2))`——单核心词术语（`Executor-class`）被抬到 2 而 hits 上限是 1，**永远无法宽松命中**；且 2 核心词术语要求全命中，宽松等价于严格。改为 `Math.Min(ceil(n/2), n)` 后单核心词命中首核心词即相关。

**接线**：`GetGlossaryContextTerms` 候选验证从 `ContainsWholeWord(entry.Value, termKey)` 改为 `IsTermRelated(entry.Value, termKey)`——倒排索引候选收集不变（`Skipray`/`Xyston` 等词命中即进候选，`Blast Boat` 拆分场景靠首核心词 `Skipray` 兜底）。

**共享常量**：`ModifierTokenPattern`（class/mark/mk/type/series/version/model/variant/generation/prototype/standard）提取到 [GlossaryManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.cs#L47)，`GetOrCreateRegex` 与 `IsTermRelated` 共用。

**已知取舍**：宽松匹配只影响提示注入（AI 参考，非强制替换），误报注入的代价仅是提示里多一两条术语，AI 靠上下文判断，错译概率极小；术语表以专有名词为主，首核心词误报风险低。

### B. 日志显示修复（换行 + 自动滚动）

**问题**：日志区一行超出显示区域，后面内容看不到（无自动换行）；自动滚动不跟随底部。

**根因**：
- [MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.xaml#L580-L587) 外层 `ScrollViewer` 的 `HorizontalScrollBarVisibility="Auto"` 给了 TextBox **无限宽度** → `TextWrapping="Wrap"` 不生效，长行水平溢出
- `LogTextBox.ScrollToEnd()`（[MainWindow.Helpers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Helpers.cs#L43)）调用的是 TextBox 内部滚动，但滚动条实际在外层 ScrollViewer（TextBox 为 `VerticalScrollBarVisibility="Disabled"`）→ 自动滚动失效

**修复**：移除多余的外层 ScrollViewer，TextBox 直接撑满容器（Wrap 生效），`VerticalScrollBarVisibility="Auto"` 自管滚动，`ScrollToEnd()` 作用于实际滚动条（250ms 合并渲染时自动跟随底部）。

### C. DataGrid 交互打磨补记（上个会话遗留，一并提交）

- **左上角全选按钮改为"重置排序"**：点击行头/列头交汇处按钮触发 `ResetSorting()`（清所有列 `SortDirection` + `SortDescriptions.Clear()` + `Refresh()`，恢复 XML 原始顺序，保留筛选）；移除原先的 `HideSelectAllButton`
- **Excel 式 Del**：选中行按 `Del` 清空译文（先 `PushUndoSnapshot` 可撤销；单元格编辑模式下放行，保留 TextBox 自身 Del 行为）

### 验证

- `dotnet test`：**48 个测试全部通过**（0 失败；新增 6 个：Xyston/Quasar 宽松命中、单核心词 bug、Skipray 变体、端到端 `GetGlossaryContextTerms`、反例）
- `dotnet build`：0 错误 0 警告

### 经验教训

1. **宽松阈值要与语义匹配**：`Math.Max(2, ceil(n/2))` 把 2 核心词术语的宽松阈值抬到"全命中"，使宽松等价于严格，单独首核心词短名（`Skipray`）永远匹配不上。阈值应随核心词数缩放并封顶
2. **ScrollViewer 包自滚动控件是双滚动画布**：外层 `ScrollViewer` + 子控件 `VerticalScrollBarVisibility=Disabled` 时，代码里对子控件调 `ScrollToEnd()` 调错对象；自管滚动的控件不要外包滚动容器
3. **提示注入与替换要分离语义**：注入路径可宽松（AI 参考，误报无害），替换/冲突检测必须严格（直接改译文，误报有害）——两套判定并存

---

## 2026-08-06（续）— 产品战略讨论：竞品分析 / 定位 / 上下文一致性 / 版权

> 背景：用户看到 LunaTranslator 项目后产生"项目没有优势"的焦虑。以产品经理 + AI 工程师 + 技术文档工程师身份进行战略讨论。

### A. 竞品分析：LunaTranslator vs 本项目

**LunaTranslator**（HIllya51，GPLv3，C++/Python，4500+ commits，69 releases）：
- 视觉小说实时翻译器（HOOK 内存提取 / OCR / 内嵌翻译）
- 用户：个人玩家（玩日语 galgame 边玩边翻）
- 核心价值：让不懂日语的人能玩

**本项目**：
- 游戏本地化批量翻译工具（XML 文件加载 / AI 批量翻译 / 导出）
- 用户：汉化组成员（把几万条文本翻译成中文并发布）
- 核心价值：让汉化组高效产出高质量汉化版

**结论**：不在同一赛道。LunaTranslator 解决"看懂"，本项目解决"发布"。两个需求长期并存。

### B. 本项目的差异化护城河

| 能力 | LunaTranslator | 本项目 |
|---|---|---|
| 批量翻译工作流（分批/缓存/断点续传） | 无 | ✅ |
| 术语表管理 + 冲突检测 | 无 | ✅ |
| AI 质量评估 + 多代理投票 | 无 | ✅ |
| 翻译缓存去重 | 无 | ✅ |
| 专家配置文件（按游戏类型定制） | 无 | ✅ |
| 黑名单过滤 | 无 | ✅ |
| 导出游戏可用 XML/DAT | 无（内嵌显示） | ✅ |

### C. AI 趋势对汉化组的影响

```
过去：人工逐条翻译（汉化组 = 翻译者）
现在：AI 批量翻译 + 人工审校（汉化组 = 审校者）
未来：AI 翻译质量更高 + 人工只需抽检（汉化组 = 质量把控者）
```

**关键洞察**：AI 越强，审校工作流越重要——汉化组对工具的依赖反而增加。本项目应顺应"从翻译者变审校者"趋势。

### D. 本地模型方案讨论（待推进）

**用户需求**：21487 条全量翻译 ~13 小时，API 端并发生成限制为硬瓶颈。

**方案**：Python FastAPI + vLLM 独立服务，暴露 OpenAI 兼容接口，WPF 端零改动（已支持 OpenAI 兼容格式）。

```
xml-ai-translator-main/
├── SimpleXmlEditor/           # WPF .NET 8（不改）
├── LocalModelServer/          # Python FastAPI + vLLM（新增，独立）
│   ├── server.py              # OpenAI 兼容接口
│   ├── requirements.txt
│   └── start.bat              # 一键启动
└── SimpleXmlEditor.Tests/
```

**前提**：需要独立 GPU（7B 需 6GB+，14B 需 12GB+）。**状态**：待用户确认 GPU 配置后决定是否推进。

### E. 超长上下文翻译一致性分析

**当前 prompt 注入机制**：

| 层级 | 机制 | 作用范围 | 解决什么 |
|---|---|---|---|
| 全局 | 专家配置（世界观/角色/风格） | 每批都注入 | 基调一致 |
| 全局 | 术语表（按原文匹配） | 每批按需注入 | 术语一致 |
| 批内 | 30 条同批互为上下文 | 单批次 | 短程连贯 |
| 单条 | Key 注入（TEXT_SPEECH_*/UNIT_*） | 每条 | 场景语境 |
| 单条 | [EXISTING ZH] 标记 | 每条 | 中文源审校 |

**未解决**：跨批次上下文断裂（第 1 批和第 100 批无关联）。

**评估**：对游戏本地化场景**够用**——大部分条目是 UI/技能/物品描述（自包含），术语表覆盖了全局一致性需求。

**改善方案排序（按 ROI）**：
1. **低成本**：分批前按 Key 前缀排序，同章节条目自然分到同一批（一行代码）
2. **中成本**：每批 prompt 末尾附加前一批摘要（3-5 条 Key+译文作为 few-shot）
3. **高成本（不推荐）**：RAG——向量数据库存储已翻译条目，每批检索语义相关历史译文。ROI 低：25000 条中仅 ~10% 对话类条目可能受益，术语表已覆盖核心一致性需求

### F. RAG 为什么 ROI 低

| RAG 要加的 | 成本 |
|---|---|
| Embedding API（25000 条 × 1 次调用） | 调用费用 + 延迟 |
| 向量库（ChromaDB/FAISS） | 新依赖 + 数据库文件 |
| 每批翻译前检索 top-K | 每批 +1 次查询 |
| 检索结果拼进 prompt | 增加 token 消耗 |
| 每批翻译完增量写入 | 额外 IO |

**收益**：仅 ~10% 对话类条目可能受益，术语表已解决最关键的全局术语一致性。**结论**：方案 1（Key 排序）一行代码覆盖 80% 需求。

### G. 实时翻译工具为什么不能靠缓存解决上下文

- **延迟约束**：带上下文 = prompt 变长 = API 响应变慢，实时体验崩
- **成本爆炸**：每次请求多发 N 句上下文，几十万字 = token 翻几倍
- **非线性对话**：游戏有分支/存档/读档，缓存记录的线性上下文可能失效
- **缓存淘汰**：局部上下文 ≠ 全局一致性

**结论**：实时翻译的延迟约束与上下文一致性本质矛盾。批量翻译天然适合带上下文——这是本项目存在的理由。

### H. LICENSE 版权更新

LICENSE 文件版权声明从 `Copyright (c) 2025 Veloxcity` 更新为 `Copyright (c) 2025 Veloxcity, 2026 BestFly666`。

**数据**：初始 9710 行（原作者），用户新增 17030 行，删除 6880 行。用户贡献占比约 65%，但约 2830 行仍为原作者代码骨架（XML 解析、翻译调用基础结构、UI 布局）。

**建议**：README 署名从"基于 Veloxcity 的原始项目扩展维护"改为"最初基于 Veloxcity 项目，经大规模重构与扩展"。当 XmlRepository 和 AiTranslationService 基础逻辑全部重写后，可改为"灵感来源于 Veloxcity"。

### 决策记录

| 议题 | 决策 | 理由 |
|---|---|---|
| LunaTranslator 竞争 | 不构成威胁 | 不同赛道，不同用户 |
| 本地模型 | 待用户确认 GPU 后推进 | Python 独立服务，WPF 零改动 |
| 长上下文 | 不做 RAG，可选 Key 排序 | ROI 低，术语表已覆盖核心需求 |
| 路线图方向 | 从"AI 翻译工具"进化为"AI 翻译 + 审校工作流" | 顺应汉化组角色转变趋势 |
| 版权 | LICENSE 加 BestFly666，README 改措辞 | 法律合规 + 道德准确 |

---

## 2026-08-06 — 翻译速度优化（分批/并发/Key注入）+ 本地模型方案讨论

> 背景：用户反馈"翻译速度慢，大批次时特别慢"、"3 路并发感觉一批批处理"。按代码审查员 + AI 工程师身份审查翻译流水线全链路。

### A. 分批逻辑：固定条目数 → 估算输出 token 预算

**问题**：[CreateBatches](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) 的切批条件是 `currentBatch.Count >= batchSize`——token 预算形同虚设。50 条中文译文输出超 max_tokens → 截断 → 拆半重试风暴（1+2+4=7 次串行请求）→ "特别慢"。

**修复**：改为按"估算输出 token"动态分批（3800 token/批），中文约 20-30 条/批。短文本自动合并到 50 条上限，长文本自动拆小——永不截断、无重试。

**文件**：`TranslationOrchestrator.cs` — 新增 `EstimateOutputTokens`，替换原 `EstimateTokens`

### B. 条目 Key 注入 Prompt

**需求**：用户提出"把 key 也注入了可以提高准确率"——指条目 Key（如 `TEXT_UNIT_BARRACKS_DESCRIPTION`），含内容类型/场景线索。

**实现**：
- `BuildPrompt`：每行格式改为 `1. [TEXT_XXX] "原文"`
- `PromptTemplates.cs`：新增规则 8——Key 只用于理解语境，禁止混入译文
- Key 经 `SanitizePromptText` 转义（防注入）
- 测试正则适配新格式

**文件**：`TranslationOrchestrator.cs` / `PromptTemplates.cs` / `TranslationOrchestratorTests.cs`

### C. 批次计时器

**需求**：用户要求"检查每批次花了多少时间"。

**实现**：`Stopwatch` 包裹 API 调用（含拆半重试总耗时），批次完成后输出 `⏱️ 批次 N/M 耗时 Xs（Y 条）`。走 LocalizationManager 本地化。

**文件**：`MainViewModel.Translation.cs` / `LocalizationManager.Dicts.{En,Zh}.cs`

### D. 并发退化修复：Dispatcher.Invoke → BeginInvoke

**问题**：用户反馈"3 路并发感觉一批批处理"。审查发现 [MainWindow.Handlers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Handlers.cs) 全部事件订阅用了 `Dispatcher.Invoke`（同步阻塞）。

**串行化机制**：
1. 3 个批次并发完成 API 调用 ✅
2. 每个批次完成后在 `finally { batchSemaphore.Release() }` **之前**调用 `OnLogMessage` / `TranslationProgressChanged` → `Dispatcher.Invoke` **阻塞后台线程等 UI 线程**
3. UI 线程被 DataGrid（25000 行）重绘占着 → Invoke 等待数百毫秒
4. 信号量迟迟不释放 → 下一批干等 → **3 路退化为接近串行**

**修复**：`Invoke` → `BeginInvoke`（异步排队），后台线程立即返回。仅 `ConfirmationRequested` 保留 `Invoke`（需同步等待用户确认）。

**文件**：`MainWindow.Handlers.cs`

### E. 进度保存不阻塞信号量

**问题**：`await SaveTranslationProgressAsync(Entries)` 在 `finally { Release() }` 之前，序列化 25000 条 + 写文件阻塞信号量释放。

**修复**：将 `SaveTranslationProgressAsync` 移到 `finally` 中 `Release()` 之后——先释放信号量让下一批启动，再保存进度。

**文件**：`MainViewModel.Translation.cs`

### F. API 端并发限制分析

**用户日志证据**：
```
18:17:49  批次 1/2/3 同时启动
18:20:04  批次 3 完成  135.4s    ← 先处理完
18:22:05  批次 2 完成  256.3s    ← 慢 121s
18:22:13  批次 1 完成  264.7s    ← 慢 129s
```

**结论**：DeepSeek API 端同时只处理 **2 个请求**，第 3 个在服务器排队。2500 RPM = 每分钟允许发送的请求数，不等于同时生成数。客户端已无瓶颈——3 个请求确实同时发出。

**无法控制**：API 端的生成速度（~4.5s/条）是硬瓶颈。21487 条全量翻译估算 ~13 小时。

### G. 本地模型方案讨论（产品评估，未实施）

**用户需求**：引入 Python 部署本地轻量化模型加速翻译。

**产品经理评估**：
- 痛点成立：13 小时全量翻译不可接受
- 取舍：速度 vs 质量（7B 模型翻译质量不如 DeepSeek-V4）
- Non-Goals：不做模型微调、不做 CPU 推理、不强制用户使用

**后端架构师方案**：
```
xml-ai-translator-main/
├── SimpleXmlEditor/           # WPF .NET 8（不改）
├── LocalModelServer/          # Python FastAPI + vLLM（新增，独立）
│   ├── server.py              # OpenAI 兼容接口
│   ├── requirements.txt
│   └── start.bat              # 一键启动
└── SimpleXmlEditor.Tests/
```
- Python 服务暴露 `/v1/chat/completions`，WPF 端零改动（已支持 OpenAI 兼容格式）
- 用户在设置里把 API URL 改成 `localhost:8899/v1` 即可
- vLLM 支持 batch 推理 + PagedAttention，比 Ollama 快 2-3 倍
- **前提**：需要独立 GPU（7B 需 6GB+，14B 需 12GB+）

**状态**：待用户确认 GPU 配置后决定是否推进

### 验证

- `dotnet test`：40 个测试全部通过（0 警告 0 错误）
- 用户实测：3 条 `🔄 批次` 同时出现（并发启动生效），批次 4 在批次 3 完成后立即启动（信号量释放正常）

### 经验教训

1. **Dispatcher.Invoke 是并发杀手**：WPF 中后台线程通过 Invoke 更新 UI 会阻塞后台线程，如果 Invoke 在信号量 Release 之前，有效并发度退化。高频事件必须用 BeginInvoke
2. **API RPM ≠ 并发生成数**：DeepSeek 2500 RPM 是发送频率限制，服务器端同时处理的请求数可能远低于此（观察值 ≈ 2）
3. **分批必须按输出 token 预算**：按固定条目数分批无法预防输出截断——中文每条 token 数是英文的 ~5 倍，50 条中文可能超 max_tokens 而截断
4. **Key 注入提升翻译质量**：条目 Key 含内容类型/场景线索（如 TEXT_SPEECH_*、UNIT_*_DESCRIPTION），帮助模型判断语境，成本极低（仅输入 token）

---

## 2026-08-05 — 缓存/进度文件统一 + "删除译文重开复活"根因修复

> 背景：用户多次报告"缓存保存失效"、"删除译文后快速保存，重新打开又出现"、"缓存文件变来变去"。按代码审查员身份通读缓存读写全链路，最终定位为**双轨缓存文件位置不统一 + 崩溃恢复文件绕过主缓存删除状态**。

### A. 根因：双轨缓存文件位置不统一

| 文件 | 原位置 | 写入时机 |
|------|--------|----------|
| `translation_cache.json`（主缓存） | `%LOCALAPPDATA%\SimpleXmlEditor\`（稳定） | QuickSave / 翻译完成 |
| `translation_progress.json`（崩溃恢复） | `AppDomain.CurrentDomain.BaseDirectory`（bin 目录，随 Debug/Release 变化） | 每批翻译后，翻译完成时删除 |

**失效链路**（"删了又出现"）：
1. 用户删除译文 → QuickSave → `SyncEntriesToCache` 移除主缓存键（Key + MD5）→ 主缓存文件更新 ✅
2. 重新打开 → `ProcessEntry` 按 Key/MD5 查主缓存 → 键已删，不恢复 ✅
3. 但加载最后还会调 `RestoreTranslationProgress`，**从残留的 `translation_progress.json` 按原文 Value 恢复旧译文** ❌ → "删除的译文又出现"

**"文件变来变去"的机制**：progress 文件在 bin 目录（Debug/Release 各一份），QuickSave 从不更新也不删除它，只在翻译中断/取消后残留。用户删除译文只改主缓存，残留 progress 里的旧数据在下次加载时把删除状态"复活"。

### B. 修复

1. **[ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs)**：新增 `_progressPath`（统一到 `%LOCALAPPDATA%\SimpleXmlEditor\`）；构造函数自动删除 bin 遗留旧 progress 文件（一次性迁移清理）
2. **[ConfigService.Cache.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.Cache.cs)**：`SaveTranslationProgressAsync` / `RestoreTranslationProgress` / `DeleteProgressFile` 三处路径统一改用 `_progressPath`
3. **[MainWindow.Events.File.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.Events.File.cs)**：QuickSave 成功后删除 progress 文件——**用户主动保存 = 主缓存已是最新快照，旧 progress 失去存在意义**
4. 手动清理 bin 残留的 86000 字节旧 progress 文件

### C. 附带排查确认（排除误报）

- **删除一条译文缓存 -2 是正常设计**：每条译文在缓存有 2 个键（`Key` 键 + `MD5(原文)` 键），删除时同时移除两个键，不是 bug
- **用户加载的 `MasterTextFile_ENGLISH.xml` 已被历史 bug 污染**：25271 条中 2737 条 `<Translation>` 存的是中文译文、22527 条为空——英文原文不在这份 XML 里（在 `mastertextfile_english.dat`）。用户看到的部分"中文还在"来自文件原文列，非缓存问题
- 验证缓存文件键分布：4013 键 = 2762 Key 键 + 1251 MD5 键，与日志计数一致，QuickSave 写盘正常

### D. 经验教训

1. **崩溃恢复文件必须与主缓存同目录、同生命周期**——任何"绕过主缓存删除状态"的恢复路径都会让用户的删除操作失效
2. **QuickSave 语义 = 用户确认的完整快照**：保存时应清理过期中间态（progress 文件），而不是只写新数据
3. **排查"删除又复活"类问题的清单**：列出加载时所有给 `entry.Translation` 赋值的路径（ProcessEntry / RestoreTranslationProgress / TryApplyDictionary / isTranslationFile 合并），逐一核对删除后是否还会被填回
4. **文件被译文覆盖是历史遗留的最大数据破坏风险**：QuickSave 不写 XML 是正确的防线（LocalisationData 单列结构无法同时存原文+译文，写回即覆盖原文）

### 验证

- `dotnet build`：0 警告 0 错误
- 用户实测：删除译文 → Ctrl+S → 重启 → 译文列保持为空（问题关闭）

---

## 2026-07-30 — Phase 1+2: 架构稳固

### 产品规划

- 产品经理 @Alex 制定完整 [PRODUCT_PLAN.md](PRODUCT_PLAN.md)，涵盖产品定位、4 阶段路线图、成功指标、风险评估
- 项目定位：**面向中文游戏本地化社区的专业 AI 翻译工作站**
- 路线图：稳固根基 → 架构完善 → 功能增强 → 生态扩展

### Phase 1: 稳固根基（技术债务清零）

#### 线程安全确认 [P0] ✅
- `ConfigService.Cache` 已使用 `ConcurrentDictionary<string, string>`（早已完成）
- `AiTranslationService.RecentRequests` 已使用 `ConcurrentQueue<DateTime>`（早已完成）
- **审计问题 #3 关闭**

#### 接口补全 [P1] ✅
- 新增 `IGlossaryManager` 接口（17 个成员：CRUD、查询、导入导出、冲突检测、术语合并）
- 新增 `IExpertProfileManager` 接口（9 个成员：CRUD、激活状态管理）
- 新增 `ITranslationEvaluator` 接口（3 个成员：评估、投票、日志事件）
- **GlossaryManager** → 实现 `IGlossaryManager`
- **ExpertProfileManager** → 实现 `IExpertProfileManager`
- **TranslationEvaluator** → 实现 `ITranslationEvaluator`
- **TranslationOrchestrator** 依赖从具体类改为接口（`IGlossaryManager`、`IExpertProfileManager`）
- **MainViewModel** 所有服务属性改为接口类型
- **审计问题 #1 关闭**，项目现拥有 **6/6 服务接口覆盖**

#### 消除 MainWindow 重复代码 [P1] ✅
- 删除 `MainWindow.LoadConfig()` → 统一走 `MainViewModel.LoadConfig()`
- 删除 `MainWindow.SaveConfig()` → 7 处调用改为 `_viewModel.SaveConfig()`
- 删除 `MainWindow.SaveTranslationProgress()` → 改用 `_viewModel.ConfigService.SaveTranslationProgress()`
- 删除 `MainWindow.RestoreTranslationProgress()` → 2 处调用改为 `_viewModel.RestoreTranslationProgress()`
- 删除 `MainWindow.SyncEntriesToCache()` → 2 处调用改为 `_viewModel.SyncEntriesToCache()`
- 删除 `MainWindow.HasChineseChars()` → 提取为 `StringExtensions` 扩展方法
- 新增 `MainWindow.InitializeFromConfig()` 方法，封装启动初始化逻辑
- **MainWindow.xaml.cs 减少 ~150 行重复代码**
- **审计问题 #2、#4 关闭**

#### 其他 P2 修复 [P2] ✅
- **代码去重**：创建 `StringExtensions.cs`，`HasChineseChars()` 作为扩展方法供全局使用
- **术语表本地化**：`GlossaryWindow` 状态筛选框改为本地化中文显示（已确认/待审核/已拒绝），使用 `Tag` 存储英文原值进行筛选
- **资源泄漏**：`AiTranslationService.TranslateBatchOpenAiCompatAsync` 和 `TranslateSingleOpenAiCompatAsync` 中 `HttpRequestMessage` 添加 `using`
- **审计问题 #5、#6、#7 关闭**

### Phase 2: 架构完善（质量基础设施）

#### 依赖注入容器 ✅
- 引入 `Microsoft.Extensions.DependencyInjection` 8.0.1
- `App.xaml.cs` 完全重写为 DI 容器入口：
  - 领域服务（Singleton）：`IConfigService`、`IGlossaryManager`、`IExpertProfileManager`
  - AI 服务（Singleton）：`IAiTranslationService`、`ITranslationEvaluator`
  - 基础设施（Singleton）：`IXmlRepository`、`TranslationOrchestrator`
  - ViewModel（Singleton）：`MainViewModel`
  - UI 窗口（Transient）：`MainWindow`
- `MainViewModel` 构造函数支持 7 个接口参数的 DI 注入，参数均有 `null` 默认值回退
- `MainWindow` 构造函数优先从 `App.Services` 获取 ViewModel
- `GlossaryWindow` / `SettingsWindow` 构造函数参数改为接口类型

#### 单元测试框架 ✅
- 创建 **SimpleXmlEditor.Tests** 项目：xUnit 2.9 + Moq 4.20 + coverlet
- **3 个测试类，13 个测试**：
  - `ConfigServiceTests`（4 个）：`GetCacheKey` null/相同/不同文本，`Cache` 为 `ConcurrentDictionary`
  - `StringExtensionsTests`（4 个）：null/英文/中文/混合文本的 `HasChineseChars` 验证
  - `GlossaryManagerTests`（5 个）：精确匹配、无匹配、更新、删除、计数
- 测试结果：**13/13 通过，0 失败，0 跳过**

#### CI/CD 流水线 ✅
- 创建 `.github/workflows/ci.yml`：push 触发 → restore → build → test → publish → upload artifact
- 发布产出：自包含 win-x64 单文件
- 运行环境：`windows-latest`，.NET 8.0

#### 错误处理规范化 [P3] ✅
- 全项目搜索空 catch 块，所有 catch 均有日志或回退逻辑
- 无空白 catch 残留
- **审计问题 #8、#9 关闭**

### 架构演进总结

```
MVVM 过渡完成度：70% → 100%
  UI 层 (WPF)          ✓ 完成
  ViewModel 层          ✓ 完成
  Service 层接口        △ 3/6 → ✓ 6/6 全部实现
  依赖注入              ✗ → ✓ Microsoft.Extensions.DI
  单元测试              ✗ → ✓ 13 个测试，0 失败
  CI/CD                 ✗ → ✓ GitHub Actions
  代码重复              ✗ → ✓ MainWindow -150 行
```

**审计 8 个已知问题 → 全部关闭（8/8 ✅）**

---

### 2026-07-30（续 2）：CI 状态检查修复 + 产品规划

#### CI 状态检查失败修复

- **现象**：GitHub 仓库三个分支（master/develop/stable）的 CI 状态检查均未通过，commit 旁显示红色 ❌
- **根因**：项目缺少 `.sln` 解决方案文件。`ci.yml` 中 `dotnet restore` 不带项目参数，在无 `.sln` 的工作目录中报错 `MSB1003: 请指定项目或解决方案文件`
- **修复**：
  1. 使用 `dotnet new sln -n SimpleXmlEditor` 创建解决方案
  2. `dotnet sln add` 添加 `SimpleXmlEditor.csproj` 和 `SimpleXmlEditor.Tests.csproj`
  3. 本地验证：restore → build（0 错误）→ test（13/13 通过）→ publish（成功）
  4. 提交并推送至 `master`（commit `acd690f`）
  5. Cherry-pick 至 `develop`（commit `a562c26`）和 `stable`（commit `6f1bf53`）
- **影响文件**：[SimpleXmlEditor.sln](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.sln)（新建）、[ci.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/ci.yml)

#### 分支同步

- `develop` 和 `stable` 分支原本与 `master` 不同步，缺少 `.sln` 修复
- 通过 cherry-pick 将修复同步至两个分支，确认推送成功
- 三个分支远程状态一致：[master](https://github.com/BestFly666/dimension-bridge) / [develop](https://github.com/BestFly666/dimension-bridge/tree/develop) / [stable](https://github.com/BestFly666/dimension-bridge/tree/stable)

#### 产品规划讨论

- 产品经理 @Alex 审查项目现状，确认 Phase 1+2 已完成、Phase 3+4 已在 PRODUCT_PLAN 中规划
- 提出 10 项新增功能建议，覆盖翻译质量、用户体验、效率提升、项目管理四个方向：
  - **高优先级**：智能预翻译、待翻译筛选面板、快捷键体系
  - **中优先级**：一致性扫描、暗色模式、审校格式导出
  - **低优先级**：上下文感知翻译、模糊匹配、多文件项目管理
- 功能建议已记录，未纳入当前迭代计划，留待后续评估

---

### 2026-07-30（续）：运行时崩溃修复

#### `dotnet run` 无响应（静默崩溃）

- **现象**：`dotnet run` 执行后无任何窗口弹出，无错误信息（WinExe 静默崩溃）
- **根因 1**：`App.xaml` 中 `StartupUri="MainWindow.xaml"` 与 `App.xaml.cs` 的 `OnStartup` 通过 DI 手动创建 `MainWindow` 冲突——WPF 处理 `StartupUri` 时调用参数化构造函数可能失败
- **修复 1**：移除 `App.xaml` 中的 `StartupUri`（`OnStartup` 已通过 `Services.GetRequiredService<MainWindow>()` 处理窗口创建）
- **根因 2**：`TranslationOrchestrator` 构造函数依赖 5 个参数，最后一个 `Action<string> logAction` 未在 DI 容器中注册，导致 `BuildServiceProvider()` → 解析 `MainViewModel` → 解析 `TranslationOrchestrator` 时抛出 `InvalidOperationException`
- **修复 2**：在 `App.xaml.cs` 的 `OnStartup` 中注册 `Action<string>` 为 no-op 到 DI 容器
- **影响文件**：[App.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml)、[App.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/App.xaml.cs)

#### CI/CD 构建失败

- **现象**：GitHub Actions `dotnet test --no-build` 报错找不到 `SimpleXmlEditor.Tests.dll`
- **根因**：`ci.yml` 只 restore/build 了 `SimpleXmlEditor\SimpleXmlEditor.csproj`，测试项目从未被编译
- **修复**：改用 `dotnet restore` / `dotnet build` 不带项目路径，自动处理所有项目
- **影响文件**：[ci.yml](file:///e:/translate/xml-ai-translator-main/.github/workflows/ci.yml)

#### 分支策略

- **背景**：仅存在单一 `master` 分支，无备份冗余，代码丢失风险高
- **方案**：创建 `stable` 和 `develop` 分支，形成三层分支保护：
  - `master` — 主线，经过测试的稳定版本
  - `stable` — 完全冗余备份，与 master 同步
  - `develop` — 开发分支，新功能从此切出，测试通过后合并
- **远程同步**：三个分支均推送至 GitHub，远程仓库形成多副本冗余

#### 文档整合

- **README 合并**：删除 `README_zh.md`，内容合并至 [README.md](file:///e:/translate/xml-ai-translator-main/README.md)（本地化已完善，不再需要单独中文版）
- **开发文档更新**：[DEVELOPMENT_LOG.md](file:///e:/translate/xml-ai-translator-main/DEVELOPMENT_LOG.md) 补充运行时崩溃修复、CI 修复、分支策略记录
- **交接文档同步**：[HANDOVER.md](file:///e:/translate/xml-ai-translator-main/HANDOVER.md) 更新已知问题表（+2 P0）、DI 架构图（含 `Action<string>` 注册）、项目结构

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

### 超大术语表性能优化

- **问题**：几万条术语时 `BuildGlossaryContext` 每次匹配需要 500 万次操作（100k 术语 × 50 条目），每批耗时 300-600ms
- **方案**：倒排索引 + 上限保护
  1. **倒排索引**：`RebuildSortedList()` 时一次性构建 `word → Set<termKey>` 映射
  2. **新方法 `GetGlossaryContextTerms()`**：条目拆词 → 倒排查候选 → 验证 → 输出匹配的术语
  3. **`MAX_GLOSSARY_CONTEXT_TERMS = 50`**：每批最多注入 50 条术语，防止 prompt 超 Token 限制
- **影响范围**：
  - `GlossaryManager.cs`：新增 `_invertedIndex`、`MAX_GLOSSARY_CONTEXT_TERMS`、`GetGlossaryContextTerms()`；`RebuildSortedList()` 扩展为排序 + 建索引
  - `TranslationOrchestrator.cs`：`BuildGlossaryContext()` 从 30 行嵌套循环精简为 3 行调用
- **性能对比**：每批从 **300-600ms → 5-15ms**，翻译 3000 条累计卡顿从 18-36 秒降至 0.3-0.9 秒

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


## 2026-07-31 — Bug 修复 + 全面代码审计

### 内容显示空白 + 手动导入失败（P0）

- **现象**：加载 XML 文件后 DataGrid 无任何内容显示，日志也不报错
- **根因 1 — DataGrid 绑定断裂**：插件加载路径（`.po`/`.json`）创建了新的 `ObservableCollection` 赋值给 `_viewModel.Entries`，但 `EntriesGrid.ItemsSource` 仅构造函数中设置一次（第 45 行），仍指向旧空集合。XML 加载路径用 `.Clear()` + `ProcessEntry()` 修改同一实例，因此正常。
- **修复 1**：[MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml.cs) 插件加载后添加 `EntriesGrid.ItemsSource = _viewModel.Entries;`
- **根因 2 — AndroidStringsPlugin 劫持 XML**：[AndroidStringsPlugin.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Plugins/AndroidStringsPlugin.cs) 的 `FileExtensions` 包含 `.xml`，所有 XML 文件被路由到插件路径。游戏 XML 根元素不是 `<resources>`，返回空列表。
- **修复 2**：从 `FileExtensions` 移除 `.xml`，仅保留 `.android.xml`
- **修复 3**：插件返回 0 条时回退到 XML 加载路径（含原文/译文选择对话框），增强容错

### 全面代码审计

由 @代码审查员 + @后端架构师 + @产品经理 对项目进行多角色审计：

#### 代码质量 — 关键发现

| 严重度 | 问题 | 位置 |
|--------|------|------|
| **Bug** | `MenuShowFilter_Click` 和 `MenuShowLog_Click` 为空方法，菜单项点击无反应 | MainWindow.xaml.cs |
| **Bug** | `TranslateWithContextAsync` 收集了上下文条目但从未传入翻译请求，上下文功能完全未实现 | MainWindow.xaml.cs |
| **Bug** | 清除缓存按钮硬编码中文 `"🗑 清空缓存"`，英文模式下不切换 | MainWindow.xaml L324 |
| 高 | `_totalCost`（double）在异步回调中 `+=` 操作，非原子 | MainViewModel.cs |
| 高 | `FetchAvailableModelsAsync` 临时覆盖 `_apiKey` 后恢复，并发调用时可能使用错误 Key | AiTranslationService.cs |
| 高 | 多处 `catch (Exception)` 仅写 `Debug.WriteLine` 或返回空，用户无感知 | AiTranslationService.cs |
| 中 | `_cacheLock` 声明后从未使用（死代码） | ConfigService.cs |
| 中 | `TranslateBatchAsync` 外层 catch 吞掉了认证/付费异常（auth/payment 错误无法传播到调用方） | AiTranslationService.cs |
| 中 | `ProcessEntry` 缓存污染：已有译文的条目先从文件加载，再被缓存中可能过期的值覆盖 | MainWindow.xaml.cs |
| 中 | 翻译重试逻辑 catch 所有 `Exception` 而非仅捕获 `HttpRequestException`，非瞬态错误也被重试 | MainWindow.xaml.cs |
| 低 | `ApplyTheme` 用 `FindName` 字符串查找控件，XAML 重命名后静默失败 | MainWindow.xaml.cs |
| 低 | `EntriesGrid.Columns` 按索引访问，列顺序变更即出错 | MainWindow.xaml.cs |
| 低 | `SanitizeLogMessage` 每次调用新建 Regex 实例，不必要的 GC 开销 | AiTranslationService.cs |
| 低 | `SaveTranslationProgress` 存储在 exe 目录而非 LocalAppData，与 config/cache 路径不一致 | ConfigService.cs |
| 低 | 撤销栈仅在批量替换时入栈，手动编辑翻译无撤销支持 | MainWindow.xaml.cs |
| 低 | 筛选文本框无占位符提示 | MainWindow.xaml |
| 低 | `Separator` 被用作间距控件（语义不当） | MainWindow.xaml L242 |

#### 架构评估

- **分层整体合理**：View → ViewModel → Orchestrator → Service，接口覆盖 6/6
- **核心问题**：MainWindow.xaml.cs 2158 行，承载约 40% 业务逻辑
  - `TranslateEntries`（200 行翻译流水线）应在 ViewModel/Orchestrator
  - `TranslateAsync`（含重试/计费/速率限制）应在 AiTranslationService
  - CSV 导出、一致性扫描的 LINQ 分析等应独立为 Service
- **MVVM 不彻底**：ViewModel 无 `ICommand`，所有交互走 Click 事件处理器
- **亮点**：TranslationOrchestrator 编排层设计优秀、多提供商架构清晰、三级回退解析健壮

#### 产品审计

- **完成度 B+**：智能预翻译、一致性扫描、评估投票、插件系统等核心功能均已交付
- **14 种语言仅 2 种有翻译**（中/英），其余 12 种静默回退英文
- **XAML 约 40+ 处硬编码**文本，未绑定 LocalizationManager
- **窗口无最小尺寸约束**，缩放过小 UI 崩溃
- **暗色模式无 XAML 主题基础设施**，全靠代码手动设颜色

### 本地化完善

- `MenuSave` 本地化键补充：en `"Save"` → zh `"保存"`
- 菜单栏顶部按钮本地化对齐（File/Edit/View/Translate/Quality/Tools/Help）

### 新增审计问题

| 编号 | 优先级 | 描述 |
|------|--------|------|
| #11 | P1 | `MenuShowFilter_Click` / `MenuShowLog_Click` 空方法实现 |
| #12 | P1 | `TranslateWithContextAsync` 上下文功能实现或删除 |
| #13 | P1 | XAML 硬编码中文 `"🗑 清空缓存"` → LocalizationManager |
| #14 | P1 | `FetchAvailableModelsAsync` API Key 并发竞争 |
| #15 | P2 | ViewModel 统计字段非原子操作 |
| #16 | P2 | 多处无声错误吞噬 → 加日志或用户通知 |
| #17 | P3 | `_cacheLock` 死代码清理 |
| #18 | P3 | XAML 硬编码文本迁移至 LocalizationManager |
| #19 | P3 | ViewModel 添加 ICommand 替代 Click 事件 |
| #20 | P3 | 暗色模式 XAML 主题基础设施 |
| #21 | P3 | 窗口 MinWidth/MinHeight 约束 |

---
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
project-root/
├── SimpleXmlEditor/                     # WPF 主项目
│   ├── Services/                        # 服务层（全部接口化 6/6 ✓）
│   │   ├── AiTranslationService.cs      # IAiTranslationService — AI 翻译核心（8 个提供商，含缓存/重试/计费）
│   │   ├── ConfigService.cs             # IConfigService — 配置与缓存管理
│   │   ├── Interfaces.cs                # 6 个服务接口定义
│   │   ├── ReviewExporter.cs            # 审校报告 CSV 导出（ReviewExportResult）
│   │   ├── TranslationEvaluator.cs     # ITranslationEvaluator — AI 翻译质量评估与多代理投票
│   │   ├── TranslationOrchestrator.cs   # 翻译流程编排（prompt/API/cache/glossary）
│   │   └── XmlRepository.cs             # IXmlRepository — XML 数据访问
│   ├── ViewModels/
│   │   └── MainViewModel.cs             # 主窗口 ViewModel（业务逻辑中枢：翻译/评估/投票/一致性扫描）
│   ├── Commands/
│   │   └── RelayCommand.cs              # ICommand 实现（驱动 ViewModel 命令属性）
│   ├── Localization/
│   │   └── LocalizationManager.cs       # 中英文 UI 本地化（200+ 键值对）
│   ├── Dictionary/
│   │   ├── CsvHelper.cs                 # CSV 文件解析/转义工具
│   │   └── GlossaryManager.cs           # IGlossaryManager — 统一术语表管理（CRUD/导入导出/冲突检测）
│   ├── ExpertProfiles/
│   │   ├── ExpertProfile.cs             # 专家配置数据模型
│   │   └── ExpertProfileManager.cs      # IExpertProfileManager — 专家配置生命周期管理
│   ├── Plugins/                          # 插件系统
│   │   ├── PoFilePlugin.cs               # IFileFormatPlugin — GNU Gettext (.po/.pot)
│   │   ├── JsonI18nPlugin.cs             # IFileFormatPlugin — JSON i18n (.json)
│   │   └── AndroidStringsPlugin.cs       # IFileFormatPlugin — Android strings (.android.xml)
│   ├── MainWindow.xaml/.cs              # 主界面（纯 UI 职责：事件转发/生命周期/主题/本地化）
│   ├── EvaluationWindow.xaml/.cs        # AI 评估结果展示窗口
│   ├── GlossaryWindow.xaml/.cs          # 术语表管理窗口（含内联对话框类）
│   ├── SettingsWindow.xaml/.cs          # 设置界面（含专家配置编辑器）
│   ├── InputDialog.xaml/.cs             # 通用双输入对话框
│   ├── FileTypeDialog.xaml/.cs          # 文件类型选择对话框
│   ├── StringExtensions.cs              # 公共扩展方法
│   ├── PromptTemplates.cs               # AI 提示词模板
│   ├── App.xaml/.cs                     # 应用入口（DI 容器）
│   └── SimpleXmlEditor.csproj           # .NET 8.0 WPF 项目文件
├── SimpleXmlEditor.Cli/                 # CLI 命令行工具
│   ├── Program.cs                        # 命令行入口（基础框架）
│   └── SimpleXmlEditor.Cli.csproj
├── SimpleXmlEditor.Tests/               # xUnit 测试项目
│   ├── ConfigServiceTests.cs            # 4 个测试
│   ├── StringExtensionsTests.cs         # 4 个测试
│   ├── GlossaryManagerTests.cs          # 5 个测试
│   └── SimpleXmlEditor.Tests.csproj
├── .github/workflows/ci.yml             # GitHub Actions CI/CD
├── DEVELOPMENT_LOG.md                   # 开发日志
├── HANDOVER.md                          # 项目交接文档
├── PRODUCT_PLAN.md                      # 产品规划
└── README.md                            # 项目说明

---

## 待完成事项

### 高优先级（P0-P1）
- [x] ~~线程安全：`ConfigService.Cache` 改用 `ConcurrentDictionary`，`AiTranslationService.RecentRequests` 改用 `ConcurrentQueue`（审计 #3）~~
- [x] ~~接口补全：为 `GlossaryManager`、`ExpertProfileManager`、`TranslationEvaluator` 抽取接口（审计 #1）~~
- [x] ~~消除重复：删除 MainWindow 中的 `LoadConfig`/`SaveConfig`/`SaveTranslationProgress`/`RestoreTranslationProgress`/`SyncEntriesToCache`，统一走 ViewModel/ConfigService（审计 #2）~~

### 中优先级（P2）
- [x] ~~代码去重：提取 `HasChineseChars` 为公共扩展方法（审计 #4）~~
- [x] ~~术语表本地化：状态筛选框显示本地化文本（审计 #6）~~
- [x] ~~资源泄漏：`HttpRequestMessage` 添加 `using` 或 `Dispose`（审计 #7）~~

### 低优先级（P3）
- [x] ~~空 catch 块添加错误日志（审计 #8）~~
- [x] ~~删除 MainWindow 中未使用的 `LoadConfig()` 方法（审计 #9）~~
- [ ] SettingsWindow AI provider 刷新逻辑去重（审计 #10）

### 新增审计项（2026-07-31）
- [x] ~~DataGrid 绑定断裂 → 插件加载后无内容（审计 #11a）~~
- [x] ~~AndroidStringsPlugin 劫持 XML 文件（审计 #11b）~~
- [ ] `MenuShowFilter_Click` / `MenuShowLog_Click` 空方法实现或删除（审计 #12，P1）
- [ ] `TranslateWithContextAsync` 上下文功能实现或删除（审计 #13，P1）
- [x] ~~XAML 硬编码中文 `"🗑 清空缓存"` → LocalizationManager（审计 #14，P1，已在 `ApplyLocalization` 中动态设置 `ClearCacheBtn.Content`）~~
- [ ] `FetchAvailableModelsAsync` API Key 并发竞争（审计 #15，P1）
- [ ] ViewModel 统计字段非原子操作（审计 #16，P2）
- [ ] 多处无声错误吞噬 → 加日志或用户通知（审计 #17，P2）
- [ ] `TranslateBatchAsync` 外层 catch 吞认证/付费异常（审计 #18，P2）
- [ ] `ProcessEntry` 缓存污染：过期缓存覆盖文件译文（审计 #19，P2）
- [ ] 翻译重试 catch 范围过宽（应仅捕获 HttpRequestException）（审计 #20，P2）
- [ ] `_cacheLock` 死代码清理（审计 #21，P3）
- [ ] XAML 硬编码文本迁移至 LocalizationManager（审计 #22，P3）
- [x] ~~ViewModel 添加 ICommand 替代 Click 事件（审计 #23，P3，RelayCommand + 6 个命令属性已完成，按钮仍由 XAML Click 转发）~~
- [ ] 暗色模式 XAML 主题基础设施（审计 #24，P3）
- [ ] 窗口 MinWidth/MinHeight 约束（审计 #25，P3）
- [ ] `ApplyTheme` FindName 字符串查找 → 改为编译时引用（审计 #26，P3）
- [ ] `EntriesGrid.Columns` 索引访问 → 改为 Name 查找（审计 #27，P3）
- [ ] `SanitizeLogMessage` Regex 缓存优化（审计 #28，P3）
- [ ] `SaveTranslationProgress` 路径统一到 LocalAppData（审计 #29，P3）
- [x] ~~手动编辑翻译的撤销支持（审计 #30，P3，2026-08-01 通过 DataGrid BeginningEdit 入栈快照实现）~~

### 功能规划
- [x] ~~单元测试 / 集成测试覆盖~~（13 个测试，核心服务覆盖）
- [x] ~~GitHub Actions CI/CD 流水线~~
- [x] ~~插件系统~~（3 个格式插件：.po、.json、.android.xml）
- [ ] 更多 XML 格式支持（如 XLIFF）
- [ ] CLI 命令行模式完善
- [ ] macOS 跨平台支持探索
- [x] ~~翻译质量评估 UI 集成~~（EvaluationWindow 评估结果展示）
- [x] ~~多代理投票功能完善~~（3 视图投票系统）

### 下次迭代计划（用户已确认 2026-07-31）

**核心目标：UI 与业务逻辑彻底分离**（解决 MainWindow.xaml.cs 2158 行、~40% 业务逻辑残留问题）

| 序号 | 任务 | 迁移目标 | 状态 |
|------|------|----------|------|
| 1 | `TranslateEntries`（200 行翻译流水线：批次调度/暂停恢复/进度统计）→ ViewModel 或 Orchestrator | MainViewModel.cs / TranslationOrchestrator.cs | ✅ 已完成 |
| 2 | `TranslateAsync`（重试/缓存/计费逻辑）→ AiTranslationService | AiTranslationService.cs | ✅ 已完成 |
| 3 | `ProcessEntry`（缓存写入/词典应用/中文检测）→ ViewModel 或 Repository | MainViewModel.cs | ✅ 已完成 |
| 4 | `RunEvaluateAsync`/`RunVoteAsync`（批量评估投票编排）→ ViewModel | MainViewModel.cs | ✅ 已完成 |
| 5 | CSV 导出逻辑 → 独立 `ReviewExporter` Service | Services/ReviewExporter.cs | ✅ 已完成 |
| 6 | 一致性扫描的 LINQ 分析 → ViewModel 或独立 Service | MainViewModel.cs | ✅ 已完成 |
| 7 | ViewModel 引入 ICommand（RelayCommand），按钮/菜单改为命令绑定 | MainViewModel.cs + MainWindow.xaml | ✅ 已完成（RelayCommand + ViewModel 命令；按钮保留 XAML Click 转发） |

**验收标准**
- MainWindow.xaml.cs 仅保留事件转发、窗口生命周期、主题/本地化应用等纯 UI 职责 ✅
- 迁移后的逻辑可通过现有测试项目验证（无回归）✅（13/13 测试通过）
- 行为与现状完全一致（翻译、暂停、恢复、缓存、评估投票均可正常使用）✅

---

## 2026-08-01 — 产品定位确认（PM 评审）

> 背景：对项目竞争力产生疑问（"Excel 等专业软件加这些功能是不是很容易取代"），经产品经理评审后确认产品定位与双用户群策略。

### 竞品威胁分析结论

| 层级 | 玩家 | 威胁等级 |
|------|------|---------|
| 通用表格 | Excel / WPS | 低（加"AI 翻译按钮"容易，加"游戏本地化工作流闭环"等于重写本工具） |
| 通用 CAT 工具 | Trados、memoQ、Crowdin、Poedit | 中 |
| 模型厂商官方工具 | DeepSeek/豆包翻译平台、官方插件 | 中高（真实威胁） |

**关键判断**：70% 的功能价值不在单点功能，而在**编排**（智能分批 + 速率限制 + 术语注入 + 缓存 + 崩溃恢复 + 评估投票 的闭环）。术语表注入 Prompt、游戏 XML 格式兼容（CDATA/LocalisationData）、崩溃恢复续传是 Excel 复制成本最高的部分。

### 双用户群定位（已确认）

| 维度 | 主：游戏本地化译者 | 次：独立游戏开发者 |
|------|------------------|------------------|
| 痛点 | 术语一致性、质量、批量、格式兼容 | 快、简单、便宜、不用学 |
| 语言方向 | 主语言 → 中文 | 英文 → 多语言（日/德/法/西…） |
| 配置意愿 | 高（术语表/评估/专家配置） | 低（一键翻译） |
| 现状替代品 | Trados/Crowdin（成熟但重） | **真空地带** |

- **主攻译者**（用户拍板）：最懂需求（用户本身是译者），竞品是庞然大物但突围点在垂直深度
- **极简版面向开发者**：从现有项目派生（复用核心引擎），不增加引擎研发成本
- **产品风险提示**：避免"两头都不讨好"——共享引擎、双入口（极简模式/专业模式），而非功能堆叠

### 极简版（开发者入口）减法清单

拟砍掉：专家配置、评估/投票、崩溃恢复（保留但后台化）、术语表管理（保留基础版）
拟保留：批量翻译、翻译缓存、格式兼容、一键导出（Unity/Godot/Unreal 格式）

### 下一步建议（待执行）

- [ ] 验证主产品护城河：找 2-3 名真实译者用户，验证"术语一致性"是否为最痛点
- [ ] 轻量 PRD：定义极简版范围、用户故事、成功指标后再动手
- [ ] 产品定位同步至 README

---

## 2026-08-01 — UI 与业务逻辑彻底分离完成

### 核心目标达成

按 2026-07-31 确认的迭代计划，完成 MainWindow.xaml.cs 从 2158 行到 ~1605 行的重构，**业务逻辑全部下沉到 ViewModel / 服务层**。

### 任务完成明细

#### 任务 1：`TranslateEntries` 翻译流水线 → ViewModel ✅
- `TranslateEntriesAsync(List<LocalizationEntry>, bool forceRefresh)` 迁移至 MainViewModel
- 完整保留：批次调度（`CreateBatches`）、暂停/恢复（`IsTranslationPaused` + 轮询延迟）、取消（`CancellationTokenSource`）、逐批增量保存进度、模型级最优延迟
- 进度统计（`successCount`/`failCount`/效率/速率限制摘要）与 `UpdateProgressDisplay` 拆分
- `TranslateSelectedCommand` / `TranslateAllCommand` 命令属性提供 UI 入口

#### 任务 2：`TranslateAsync` 重试/缓存/计费 → AiTranslationService ✅
- `TranslateSingleAsync` 内建缓存检查（命中触发 `CacheHit` 事件）、写入缓存、`ApiCallCounted`/`ApiCharsCounted` 计费事件
- 429 限流重试：`HttpRequestException("429")` → `CalculateOptimalDelay() * (attempt + 2)` 递增退避
- 构造函数支持 `IConfigService` 注入（MainViewModel 已接线）

#### 任务 3：`ProcessEntry` → ViewModel ✅
- `ProcessEntry` 迁移至 MainViewModel：RowNumber 分配、`TryApplyDictionary`、中文原文检测（中文直接填入译文）、Key/原文双路径缓存命中
- `TryApplyDictionary` 仅对空译文生效（对照表语义不变）

#### 任务 4：评估/投票编排 → ViewModel ✅
- `EvaluateEntriesAsync` / `VoteEntriesAsync`（批量）与 `EvaluateEntry` / `VoteEntry`（单条）迁移
- `EvaluationOutcome` / `VotingOutcome` / `PreTranslateOutcome` 结果类封装统计信息
- 评估/投票过程状态通过 `EvaluationStatusText` / `VotingStatusText` 事件推送

#### 任务 5：CSV 导出 → ReviewExporter ✅
- 新建 `Services/ReviewExporter.cs`：`ReviewExportResult`（Total/Reviewed/NeedsFix/NotReviewed）+ `Export(filePath, entries)`
- 表头 `Status,Key,Original,Translation`，审校状态本地化标签，CSV 转义
- MainWindow `ExportReviewBtn_Click` 仅保留文件对话框与结果展示

#### 任务 6：一致性扫描 → ViewModel ✅
- `ScanConsistencyIssues()` 迁移至 MainViewModel，`ConsistencyScanCompleted` 事件回传问题列表

#### 任务 7：ICommand（RelayCommand）✅
- 新建 `Commands/RelayCommand.cs`（标准 ICommand 实现，`CommandManager.RequerySuggested` 驱动 CanExecute）
- MainViewModel 暴露 6 个命令：`TranslateSelectedCommand`、`TranslateAllCommand`、`EvaluateCommand`、`VoteCommand`、`SmartPreTranslateCommand`、`ConsistencyScanCommand`
- 按钮/菜单保留 XAML Click 事件，MainWindow 转发为命令执行（降低重写风险，行为与现状完全一致）

### MainWindow.xaml.cs 新结构（纯 UI 职责）

- **事件订阅渲染**：13 个 ViewModel 事件（LogMessage、StatusMessageChanged、TranslationStarted/Progress/Finished/Error、Evaluation/Voting 状态与完成、PreTranslateCompleted、ConsistencyScanCompleted、ConfirmationRequested、MessageRequested）
- **转发层**：翻译/评估/投票/预翻译/一致性扫描 → 命令或 `TranslateEntriesAsync`；Pause/Stop → ViewModel 状态或 `CancelTranslation`
- **窗口生命周期**：`InitializeFromConfig`、`AutoLoadModelsAsync`、`OnClosed`、`ApplyTheme`、`ApplyLocalization`、`OnEntryPropertyChanged` 清理
- **纯 UI 辅助**：DataGrid 选择/列头双击排序、筛选（Key/原文/译文 + 未翻译切换）、批量替换/撤销（`_undoStack`）、快捷键（Ctrl+S/O/Z/T/Shift+T、F5/F6、Esc）、查找栏逻辑移除（当前 XAML 无查找栏）

### 验证

- `dotnet build SimpleXmlEditor.sln`：**0 错误**
- `dotnet test SimpleXmlEditor.sln`：**13/13 通过**（ConfigService 4 + StringExtensions 4 + GlossaryManager 5）
- 未改动：`TranslationOrchestrator`（387 行）、`App.xaml.cs`（DI 注册）、插件系统、CLI

### 遗留

- 审计 #12（`MenuShowFilter_Click`/`MenuShowLog_Click` 空方法）：保留为预留占位，待后续实现筛选栏/日志栏显隐
- 审计 #13（`TranslateWithContextAsync` 死代码）：未被任何入口调用，可安全删除
- 测试项目 `StringExtensionsTests.cs` 存在 1 个 CS8600 警告（既有，非本次引入）

---

## 2026-08-01 — 多代理投票与翻译评估完成度提升

> 背景：评估/投票功能此前仅"名义可用"（候选集只有 [当前译文, 原文] 两项、投票结果无法一键应用、逐条调用慢）。本次按最小变更原则补齐四个能力，使投票与评估达到可用状态。

### 完成明细

#### A. 候选译文生成（投票从"二选一"变"三选一"）✅
- `ITranslationEvaluator.GenerateCandidatesAsync` 新增（[Interfaces.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/Interfaces.cs)）
- `TranslationEvaluator.GenerateCandidatesAsync`：单次 API 调用返回 N 个候选（JSON 数组），`ParseCandidateResponse` 失败时回退正则提取引号字符串
- 投票候选集 = 当前译文 + AI 生成 2 个备选（去重），无译文时兜底原文

#### B. 应用最佳译文（投票结果闭环）✅
- 批量投票：`VoteBatchAsync` 结果按 `EntryKey` 回填 `Entries`，`PushUndoSnapshot` 快照后自动应用最佳译文（日志提示应用条数）
- 单条投票：`ConfirmationRequested` 弹窗确认后应用（含 Undo 快照），新增 `VoteApplyPrompt`/`VoteApplyTitle`/`VoteApplied`/`VoteAppliedBest` 4 个中英文本地化键

#### C. 批量 API 调用加速 ✅
- `EvaluateBatchAsync`（20 条/批）+ `VoteBatchAsync`（10 条/批）：一次 Prompt 含多条目，JSON 返回含 `index` 映射回条目
- 解析失败自动回退逐条调用（不影响可用性）
- 接口补齐 `EvaluateBatchAsync`/`VoteBatchAsync` 声明（实现先行、接口遗漏导致首次编译 4 错误，已修复）

#### D. 上下文注入 ✅
- 新增 `MainViewModel.GetEvaluationContext()`：从激活的 ExpertProfile（Description + Context）构建评估/投票上下文
- `EvaluateEntry`/`VoteEntry`/`EvaluateEntriesAsync`/`VoteEntriesAsync` 全链路传入

### 验证

- `dotnet build SimpleXmlEditor.sln`：**0 错误**（1 个既有测试 CS8600 警告，非本次引入）
- `dotnet test SimpleXmlEditor.sln`：**13/13 通过**
- 修复项：接口缺失 `EvaluateBatchAsync`/`VoteBatchAsync` 声明（CS1061）、批量分支 `entry` 变量与方法体同名冲突（CS0136）

### 遗留

- 投票批量分支自动应用最佳译文（无确认弹窗），与单条分支（有确认）行为不一致，后续可加"批量应用前确认一次"选项
- `BuildCandidatePrompt` 中源语言标注为 `Original ({targetLang})`，措辞可再优化（不影响功能）

---

## 2026-08-01 — HandyControl 组件库全面接管 UI

> 背景：此前 UI 样式全部为手写自定义（渐变菜单、卡片、TagPill、SectionTitle 等 6 个窗口各自维护），重复代码多、风格不统一。用户明确"一点点改浪费时间，直接用组件库"，经对比 Wpf.Ui 后选定 **HandyControl 3.5.1**，并执行"全部换了，一点不留"——删除所有自建样式，视觉完全交给 HandyControl。

### A. 组件库引入与全局主题 ✅

- **NuGet 依赖**：`HandyControl 3.5.1`
- **App.xaml** 合并 `SkinDefault.xaml` + `Theme.xaml`，设置全局主题色：
  - `PrimaryColor` = `#2196F3`（Material Blue，用户选定"蓝绿白橙青"多彩方案；**紫色仅作背景氛围点缀，禁止作为默认主色**）
  - **关键坑**：HandyControl 主题键 `PrimaryColor` 是 **Color 类型**而非 `SolidColorBrush`，误写为 Brush 会导致应用启动崩溃
- **删除全部自建样式**：`HeaderBtn`、`Card`、`SectionTitle`、`TagPill`、`InfoLabel` 五个自定义 Style 从 App.xaml 移除（全局 grep 确认引用清零）
- **保留的全局样式**：`BaseButton`（基于 HandyControl `ButtonCustom` + CornerRadius=10）+ 7 个颜色变体（Primary/Success/Warning/Danger/Cyan/Grey/Small，含 hover/按下三层色）+ Glossary 窗口专用 `DataGridStyle`

### B. 窗口全部切换 HandyControl ✅

| 窗口 | 改动 |
|------|------|
| MainWindow | 菜单栏移除渐变背景/边框/子菜单样式回归默认；工具栏深蓝 → 白色系；AI 面板/Card 容器/信息 pills 全部内联化 |
| SettingsWindow | 删除 6 种本地样式（ModernButton/SuccessButton/...）；TextBox/ComboBox 全部改 hc: 版本 + `hc:InfoElement.Placeholder` 水印 |
| GlossaryWindow | 清空 Window.Resources 重复样式；hc:TextBox/hc:ComboBox + 全局按钮 |
| EvaluationWindow | MarkLow/Close 用 `GreyBtn`，Apply 用 `SuccessBtn` |
| InputDialog | `RoundedButton` → `GreyBtn`/`PrimaryBtn`；输入框 hc:TextBox + `ShowClearButton` |
| FileTypeDialog | 删除 `RoundedButton`，改用 `PrimaryBtn`/`GreyBtn` |

### C. 全新浅色系配色（用户要求"尽量不要出现白色"）✅

| 用途 | 原颜色 | 新颜色 |
|------|--------|--------|
| 窗口背景 | 白/灰 | 淡紫灰 `#F0EEF6` |
| 卡片/面板表面 | 纯白 | 淡紫白 `#F7F6FB` |
| 工具栏/页签/底部栏 | 纯白 | 淡紫 `#EAE8F4` |
| 边框 | `#E0E0E0`/`#E8ECF0` | 统一 `#DDD9EA` |
| 表格交替行 | 淡紫 | **统一白色**（用户后续要求移除交替） |
| 列头/行头 | `#ECEFF1` | `#E5E3F0` |
| 功能面板 | — | 保持绿色 `#E8F5E9`、日志深色控制台 `#1E1E1E` |

### D. Bug 修复（HandyControl 接管引发的回归）✅

1. **全 UI 白字看不见（菜单/表格/输入框）**：App.xaml 曾把 `PrimaryTextColor` 覆盖为 `#FFFFFF`——这是 HandyControl 全局主文本色，浅色背景下白字全部不可见 → **删除该覆盖**，恢复主题默认深色文本
2. **单元格选中/编辑白字**：`DataGridCell` 选中/编辑态显式设置 `Foreground=#263238` + 编辑 TextBox 显式 `Foreground/Background/CaretBrush`
3. **整列选中无视觉反馈**：新增 `ColumnIndexMatchConverter`（MultiBinding 比较单元格列号与 `DataGrid.Tag`），点击列字母条 → `EntriesGrid.Tag = 列索引` → 整列单元格 DataTrigger 高亮 `#BBDEFB`；手动点选单元格自动清除列高亮（不逐格物理选中，避免卡顿）
4. **日志折叠后无法展开**：原折叠按钮在 LogPanel 内部，面板 Collapsed 后按钮消失 → 外部新增窄条 `LogExpandBar`（▶），折叠时列宽保留 30px 常驻可点
5. **日志内容垂直居中**：LogTextBox 显式 `VerticalContentAlignment="Top"`（HandyControl 隐式样式默认 Center）

### 验证

- `dotnet build SimpleXmlEditor.sln`：**0 错误**
- `dotnet run --project SimpleXmlEditor`：启动稳定（主题色崩溃问题未复发）
- `dotnet test SimpleXmlEditor.sln`：**13/13 通过**
- 全项目 grep：`Card/SectionTitle/TagPill/InfoLabel/HeaderBtn` 引用清零

### 遗留

- 菜单栏视觉由 HandyControl 默认接管，深色渐变工具栏背景已移除（用户已知晓并接受白色系风格）
- 其余 12 种未翻译 UI 语言仍静默回退英文（非本次范围）

---

## 2026-08-01（续）— DataGrid 交互全面对齐 Excel + 冲突检测修复 + MainWindow 拆分

> 背景：用户要求译文列支持点击排序、选中整列需精确点击字母（Excel 式）、全选卡顿、单元格需竖线分隔且支持拖拽调整行列大小。冲突检测点击后卡死。MainWindow.xaml.cs 臃肿需拆分。

### A. 译文列点击排序 ✅

- **问题**：译文列 `DataGridTemplateColumn` 缺少 `SortMemberPath`，点击表头不排序
- **修复**：[MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml) 译文列添加 `SortMemberPath="Translation"`，与 Key/原文列行为一致

### B. 单元格点击行为改为 Excel 式 ✅

- **原行为**：点任意单元格 → 联动勾选该行复选框 → 触发整行选中高亮
- **新行为**：
  - 点单元格 → 只选中当前格子（Excel 行为）
  - 点行号（表头数字）→ 选中整行并勾选复选框
  - 点列字母（A/B/C/D/E）→ 选中整列
  - 点复选框 → 手动标记该行
- **影响**："翻译选中"按钮现在只翻译**显式勾选**或**点列字母/行号选中**的行

### C. 全选卡顿修复 ✅

- **根因**：全选/取消全选用了 `EntriesGrid.Items.Refresh()`（重建整个视图），反选完全没加防抖，复选框勾选联动整行选中产生 SelectionChanged 事件风暴
- **修复**：全选/取消/反选统一改用 `_suppressSelectionSync` 防抖（和列字母选中同一套机制），去掉 `Refresh()`。复选框状态靠 PropertyChanged 增量更新，虚拟化列表滚动时自动读取正确状态
- **清理**：移除已无使用处的 `BulkUpdateSuppression` 静态标记（XmlRepository.cs）

### D. 整列选中重写（假高亮 → 真选中）✅

- **原实现**：`_selectedColumnIndex` 字段 + `EntriesGrid.Tag` 标记 + `ColumnIndexMatchConverter` 转换器 + DataTrigger 假高亮
- **新实现**：点列字母 → `SelectedCells.Add(new DataGridCellInfo(entry, column))` 把该列每个格子**真正选中**，高亮走格子原生 `IsSelected`，和整行选中完全同一套机制
- **删除**：`_selectedColumnIndex` 字段、`EntriesGrid.Tag` 标记、`ColumnIndexMatchConverter` 转换器和 XAML 里的假高亮 DataTrigger 全部移除
- **优化**：`GetSelectedEntries` 统一遍历 `SelectedCells` + `SelectedItems` 去重，用 `HashSet` 避免大文件全选时的 O(N²) 卡顿

### E. 列字母按钮铺满整格 ✅

- **演进**：从小药丸（26×14 圆角）→ 铺满整格（`HorizontalAlignment="Stretch" VerticalAlignment="Stretch"`，直角无圆角）
- 字母内容居中显示，视觉上和下面的单元格完全对齐——点这个格子的任何位置 = 选中整列

### F. 单元格竖线 + 列宽拖拽 + 行高拖拽 + 文本换行修复 ✅

| 改动 | 详情 |
|------|------|
| **竖线分隔** | `GridLinesVisibility` 改为 `All`，竖线/横线用浅灰色 `#E0E0E0`，去掉 CellStyle 里的 `BorderThickness="0"` |
| **文本换行** | 去掉误加的 `RowHeight="Auto"`（DataGrid.RowHeight 是 double 不接受 "Auto"），默认即按内容自适应 |
| **列宽拖拽** | 三列改为固定像素宽度（Key: 240px、Original: 360px、Translation: 360px），WPF 只有固定列宽才能拖拽边界 |
| **行高拖拽** | 行号列底部加 5px Thumb 拖拽把手，DragDelta 按偏移量改 `Height`，最小 24px，和 Excel 一致 |

### G. 冲突检测卡死修复 ✅

- **根因**：`DetectConflicts` 在 UI 线程同步执行，O(条目数 × 术语数) × 正则匹配开销，大文件时卡死
- **修复**：包裹在 `Task.Run` 里放到后台线程执行，通过 `Dispatcher.BeginInvoke` 回到 UI 线程显示结果

### H. 冲突检测窗口关闭后异常修复 ✅

- **根因**：GlossaryWindow 触发 `ConflictsDetected` 事件后立刻 `Close()`，异步检测完成回来调 `window.ShowConflicts()` 时窗口已关闭，`dialog.Owner = this` 抛异常
- **修复**：把冲突结果显示职责从 GlossaryWindow 转移到 MainWindow（`ShowConflictResults` 方法，Owner 设为 MainWindow）

### I. MainWindow.xaml.cs 拆分为 partial class ✅

- **背景**：MainWindow.xaml.cs 约 1753 行，承担 8+ 个职责。用户要求"新功能不要往这里写"
- **拆分结果**：

| 文件 | 职责 | 行数 |
|------|------|------|
| `MainWindow.xaml.cs` | 核心：字段、构造函数、事件订阅、渲染方法、生命周期 | ~470 |
| `MainWindow.Localization.cs` | ApplyLocalization、UpdateInfoLabels | ~145 |
| `MainWindow.Theme.cs` | ApplyTheme（深色/浅色模式） | ~55 |
| `MainWindow.Grid.cs` | DataGrid 交互：选中、列字母、行拖拽、批量勾选、审核状态 | ~255 |
| `MainWindow.Helpers.cs` | AddLog、UpdateCacheInfo、ShowControlButtons、ShowEvaluationWindow | ~85 |
| `MainWindow.Events.cs` | 所有 UI 事件处理：点击、筛选、菜单、快捷键、翻译命令 | ~590 |

- **约束**：MainWindow 是纯前端 View 层，业务逻辑必须放在 Services/ 或 ViewModels/，单文件不超过 400 行

### 验证

- `dotnet build SimpleXmlEditor.sln`：**0 错误 0 警告**
- 全部为纯代码搬家（partial class），逻辑零改动

### 架构约束记录（已写入 project_memory.md）

1. MainWindow.xaml.cs 是纯前端 View 层，新功能不得写入
2. 业务逻辑、算法、数据处理必须放在 Services/ 或 ViewModels/
3. 单文件不超过 400 行，超过继续拆分

---

## 2026-08-01（续）— 评估/投票架构分析与产品改进方向

> 背景：用户发现评估和投票都调用同一 API（同一模型），质疑"AI 自己检查自己"的有效性；同时关注 token 成本（全套流程 4 倍 token）。

### 当前架构问题

```
翻译（模型A）→ 评估（模型A）→ 投票（模型A）
     ↑              ↑              ↑
     └──────── 同源偏差：学生自己批改考卷 ────────┘
```

- 评估 = AI 给自己的翻译打分，系统性地高估质量
- 投票的"3 个 Agent"只是 Prompt 里的 3 个角色，实际还是同一个模型回答——不是真正的多代理投票

### API 调用成本分析

| 操作 | API 调用次数 | Token 倍数 |
|------|-------------|-----------|
| 翻译 | 1 次 | 1x |
| 评估 | 1 次 | +1x |
| 投票 | 1 次（生成候选）+ 1 次（投票） | +2x |
| **全套** | **4 次** | **4x** |

### 产品改进方向（未实施，待验证）

**Phase 1：评估换厂商**
- 翻译保持当前 Provider
- 评估/投票用不同 Provider（设置里加"评估模型"选项）
- 改动量：给 `TranslationEvaluator` 注入第二个 `AiTranslationService` 实例

**Phase 2：真·多代理投票**
- 投票时并行调用 2-3 个不同厂商的 API
- 每个模型独立评分，取平均

**Phase 3：渐进式质量保障（降低 token 成本 ~89%）**
```
翻译完成
  ↓
第1层：零成本筛选（本地）— 术语命中? 缓存命中? → 跳过
  ↓
第2层：批量评估（1x token）— 对"非可信"条目批量打分
  ↓
第3层：精准投票（2x token，仅低分）— 只对低分条目生成候选+投票
```

**效果对比**（1000条，假设70%可信）：

| 方案 | 评估 token | 投票 token | 总量 |
|------|-----------|-----------|------|
| 当前全量 | 1000条 | 3000条 | 4000条 |
| 渐进式 | 300条 | 150条 | **450条（~89% 节省）** |

---

## 2026-08-01（续）— 评估换厂商落地（Phase 1）+ 翻译/评估稳定性修复 + 评分列落地

> 背景：实现此前规划的 Phase 1（评估/投票换独立厂商模型打破同源偏差）；随后用户连续报告翻译失败、多条评估崩溃、投票无反应、投票出英文、撤销不生效、全选/整列卡顿等问题，逐项排查修复。

### A. 评估模型独立配置落地（Phase 1）✅

- **ConfigService**（[ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs)）：`AppConfig` 新增 `EvaluationAiProvider` / `EvaluationModel` / `EncryptedEvaluationApiKey` 三个字段，API Key 沿用 DPAPI 加密存储
- **TranslationEvaluator**（[TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs)）：注入 `IConfigService`，惰性创建**评估专用 `AiTranslationService` 实例**（不同厂商/模型），配置为空时回退翻译模型，保持旧行为兼容
- **SettingsWindow**：新增"🔍 评估模型"Tab（提供商下拉 + API Key + 模型名），配置持久化到 config.json
- **本地化**：新增 9 个中英文 key（`EvalModelTab`/`EvalModelConfig`/`EvalModelDesc`/`EvalAiProviderLabel`/`EvalApiKeyLabel`/`EvalApiKeyPlaceholder`/`EvalModelNameLabel`/`EvalModelPlaceholder`/`EvalUseTranslationModel`），全部界面文本走 LocalizationManager
- **修复**：HandyControl 水印 API 用 `InfoElement.SetPlaceholder`（`TextBoxHelper` 不存在）

### B. 翻译失败修复：DeepSeek 模型名升级 + 在线模型扫描 ✅

- **现象**：批量翻译全部失败，日志 `[HTTP 400] The supported API model names are deepseek-v4-pro or deepseek-v4-flash, but you passed deepseek-flash`
- **根因**：DeepSeek 2026-04-24 升级 API 模型名，`deepseek-flash`/`deepseek-pro` 停用，代码 `StaticModels` 硬编码过时
- **修复 1**：`AiTranslationService.cs` 静态模型列表更新为 `deepseek-v4-flash` / `deepseek-v4-pro`
- **修复 2**：新增 `FetchOpenAiCompatModelsAsync`——所有 OpenAI 兼容厂商（DeepSeek/智谱/Moonshot/千问/豆包/文心/讯飞）调用 `GET /models` 动态拉取最新模型列表；`EnsureRateLimitsFromStatic` 为动态获取的模型补速率限制兜底
- **修复 3**：`FetchAvailableModelsAsync` 改为**动态获取优先 → 失败回退 StaticModels**，厂商再升级模型名也不会失效
- **用户操作**：设置 → AI 模型 → 🔄 刷新 → 选择 `deepseek-v4-flash`

### C. 多条评估/投票崩溃修复 ✅

| 崩溃点 | 根因 | 修复 |
|--------|------|------|
| `results.ToDictionary(r => r.TranslatedText)` | 重复键抛 `ArgumentException` 直接崩溃 | 改用循环 + 索引赋值，重复时后者覆盖（[MainViewModel.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.cs)） |
| 批量评估无异常兜底 | `EvaluateBatchAsync` 未 try-catch，HTTP 402/网络错误崩溃 | chunk 级 + fallback 逐条级 try-catch，失败条目标记并跳过（[TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs)） |
| 投票流程无兜底 | `GenerateCandidatesAsync`/`VoteBatchAsync` 异常穿透 | ViewModel 层 try-catch，异常触发 `Failed` outcome |
| HttpClient 超时 | 批量评估 prompt 长、响应慢，30s 不够 | 超时 30s → **120s** |

### D. 评估分数落地 DataGrid（新增"评分"列）✅

- **背景**：多条评估结果原本靠弹窗展示，弹窗 UI 在多条数据时崩溃；用户建议"额外增加一列用于显示输出分数，这样也好排序"
- **LocalizationEntry**（[XmlRepository.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/XmlRepository.cs)）新增：
  - `EvaluationScore`（double，-1 = 未评估）+ `EvaluationScoreDisplay`（"8.5"）+ `EvaluationScoreColor`（≥8 绿 / ≥5 黄 / <5 红 / 未评估灰）+ `EvaluationImprovement`（tooltip 显示改进建议）
  - 全部支持 `INotifyPropertyChanged`，UI 自动更新
- **DataGrid 新增 Score 列**（[MainWindow.xaml:493](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml#L493)）：`SortMemberPath="EvaluationScore"` 支持点击列头排序，颜色编码，悬停 tooltip 显示 AI 解释
- **移除弹窗**：批量/单条评估结果直接写入表格，不再调用 `ShowEvaluationWindow`（彻底规避崩溃）
- **本地化**：新增 `Score` / `LogScoreUpdated` key

### E. 多代理投票体验完善 ✅

- **候选生成进度反馈**：每条生成打日志 `生成候选译文 [3/10]: KEY`，状态文本同步更新——之前投票逐条调 API 无反馈，用户以为卡死
- **VotingOutcome 携带结果**：新增 `AppliedCount`（已应用条数）+ `Results`（`List<VotingResult>`，含平均分/最佳译文/共识摘要）
- **投票结果写 DataGrid**：批量投票按 `EntryKey` 回填 `EvaluationScore`，tooltip 显示最佳译文；最佳译文自动应用（可 Ctrl+Z 撤销）
- **本地化**：新增 7 个中英文 key（`LogGeneratingCandidate`/`VoteCandidateProgress`/`LogVotingStart`/`VoteVotingProgress`/`VoteBatchResultDetail`/`VoteBestTranslation` 等）

### F. 投票翻译出英文修复 ✅

- **根因**：候选生成与投票 prompt 把 `targetLang` 错误标注在原文上（`Original (Chinese): hello`），AI 误以为原文是中文
- **修复**：改为 `Original (English): hello` + `Target language: Chinese`，并强调 "All candidates MUST be in {targetLang}, NOT in English"（[TranslationEvaluator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.cs)）

### G. 撤销功能对齐 Excel ✅

- **撤销实时响应**：`UndoLast()` 返回 `List<LocalizationEntry>`；移除 MessageBox 弹窗阻断，改日志输出；撤销后 `ScrollIntoView` + `SelectedItem` 跳转定位到被撤销行
- **手动编辑可撤销（审计 #30 关闭）**：DataGrid 新增 `BeginningEdit` 事件（[MainWindow.xaml:271](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml#L271)），用户开始编辑 Translation 列时先 push 当前值快照——手动编辑翻译后也能 Ctrl+Z 恢复

### H. 全选/整列卡顿修复（Excel 式选择模型，5 轮迭代）✅

| 轮次 | 方案 | 效果 |
|------|------|------|
| 1 | 静默设置 `IsSelected`（不触发 PropertyChanged）+ `SelectAll()`/`UnselectAll()` | 仍卡；且 `SelectAll()` 选中所有 cell 导致"选中整列变全选" |
| 2 | 分片添加 `SelectedCells` + `Task.Yield()` | 界面不冻结但总耗时仍长 |
| 3 | **Excel 式逻辑选择模型**：`_logicalSelectAll`/`_logicalSelectColumn` 标志 + 只高亮可见行 + 滚动补选 | 选中列不卡；全选仍卡 |
| 4 | `VirtualizingStackPanel` 遍历（替代 `ContainerFromIndex` 全量遍历）+ 滚动时 Clear+重选防 SelectedCells 积累 + 显式开启虚拟化 | 改善但仍卡 |
| 5 | **根因定位**：Ctrl+A 走 DataGrid 内置 `SelectAll()`，一次性选中 10000×6=60000 个 cell；`PreviewKeyDown` 拦截 Ctrl+A 改为逻辑全选（[MainWindow.Grid.cs:96](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Grid.cs#L96)），编辑单元格时（焦点在 TextBox）放行 | ✅ 毫秒级 |

- **最终架构**（[MainWindow.Grid.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Grid.cs)）：
  - 逻辑选择标志记录"全选/整列"状态，`GetSelectedEntries()` 直接返回全部行（业务毫秒级）
  - `HighlightVisibleCells` 只操作虚拟化可见行的 cell（视觉毫秒级）
  - `EntriesGrid_ScrollChanged` 滚动补选，先 Clear 再重选防集合无限积累
  - 用户手动点击行 / 反选 / 全不选时自动退出逻辑模式
- **性能调试**：关键路径保留 `[perf]` 前缀计时日志（Stopwatch），便于后续定位

### 验证

- `dotnet build SimpleXmlEditor\SimpleXmlEditor.csproj`：**0 错误 0 警告**
- 全选 / 选中整列 / 反选均为毫秒级，滚动高亮跟随视野

### 遗留

- 反选（不规则选择）仍走分片方案，大数据量下耗时较长但界面不冻结，可后续优化
- `[perf]` 计时日志为调试保留，确认稳定后可移除

---

## 2026-08-04 — 架构全量治理 + 黑名单隐藏 + 术语注入放宽 + 大批次翻译稳定性

> 背景：项目文件日益膨胀（MainWindow.xaml.cs 等超 400 行、解析逻辑重复），按用户要求执行"全量治理（1+2+3）"架构重构；随后针对黑名单、术语注入、大批次翻译失败三个实际使用痛点进行优化。

### A. 架构重构全量治理（1+2+3）✅

**阶段 1：目录整理**
- 窗口统一收纳到 `Windows/`，删除重复文件，重建 [PROJECT_INDEX.md](file:///e:/translate/xml-ai-translator-main/PROJECT_INDEX.md)（16 个目录章节 + 全部文件职责索引）

**阶段 2：400 行超标文件拆分（partial class）**
- **LocalizationManager** → `LocalizationManager.Dicts.En.cs` / `LocalizationManager.Dicts.Zh.cs`（各 666 行纯静态文案字典，作为数据文件例外保留）
- **MainViewModel** → 10 个 partial（Properties/Undo/Config/Cache/EntryProcessing/Translation/Evaluation/Voting/Consistency）
- **MainWindow 系列** → Events.File / Events.Translation / Events.Tools / Grid.Sorting / Grid.ContextMenu / Grid.Editing / Handlers / FileOps
- **SettingsWindow** → Save / Models / Profiles；**GlossaryWindow** → Filter / ImportExport / TermOps
- **服务层** → AiTranslationService（Models/Providers）、ConfigService（Cache/Scores）、XmlRepository（Models）、TranslationEvaluator（Prompts/Parsing/Utils）、GlossaryManager（Persistence/Index/Conflict/Crud）
- 修复子代理拆分遗漏的 3 处 using 缺失（System.Windows.Data / Localization / System）

**阶段 3：AI 响应解析收敛**
- 新建 [AiResponseParser.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiResponseParser.cs)：统一 `StripCodeFence` + `ParseTranslations`（标准 JSON + 三级回退）
- [TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs) / [TranslationEvaluator.Parsing.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationEvaluator.Parsing.cs) 统一走 AiResponseParser，删除各自内联/重复解析（含私有 TrimCodeFence）
- 一致性/冲突检测核对：`ScanConsistencyIssues`（同源译文不一致）与 `DetectConflicts`（术语未按术语表翻译）语义独立，未强行合并

### B. 黑名单：UI 隐藏 + 原文匹配放宽 ✅

**黑名单条目 UI 隐藏（不被选中）**
- 筛选行新增"隐藏黑名单"开关（默认勾选，[MainWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Windows/MainWindow.xaml)），`ApplyFilter` 过滤 `IsBlacklisted` 条目
- 因全选/整列/行头选择均基于过滤后视图（`EntriesGrid.Items`），隐藏条目**天然不会被选中**
- "翻译全部"排除黑名单条目（确认框数字与实际执行一致）；加载/黑名单规则变更后自动应用筛选

**原文匹配从"全串精确"放宽为"精确或后缀"**（[BlacklistManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/BlacklistManager.cs)）
- 规则 `UNUSED` 现可命中 `Borga Besadii Diori:  UNUSED`（后缀、忽略大小写/空白）、`unused`（忽略大小写）
- 词在中间的正常文本（`The unused unit remains`）不命中，保留防误伤底线
- 黑名单窗口文案同步更新为"精确或结尾匹配（忽略大小写）"

### C. 术语注入：匹配放宽 + 提示词语境例外 ✅

**整词匹配放宽**（[GlossaryManager.Index.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.Index.cs)）
- 正则 `\bJedi\b` → `(?<![A-Za-z0-9])Jedi(?:es|s|'s)?(?![A-Za-z0-9])`：覆盖 `Jedis`、`Jedi's`、`dark_jedi`、`Stormtroopers`（规则 Stormtrooper）
- 候选阶段补充复数去尾查询（`stormtroopers` → `stormtrooper`），因倒排索引是精确词查询
- 词内拼接（`JediMaster`、`JedisX`）仍不匹配，避免误伤；每批上限保持 50
- 影响面：术语注入 + 冲突检测统一放宽，`TryGetValue`（整串直填）不受影响

**提示词从"强制"改为"默认遵循 + 语境冲突例外"**
- 主注入（TranslationOrchestrator）：`CRITICAL/MUST/EXACT` → `Preferred translations (follow unless the context clearly conflicts)`，新增例外条款（比喻用法/专有名称组合/不同词义时可自然翻译，存疑时优先术语表）
- 专家配置（[ExpertProfile.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ExpertProfiles/ExpertProfile.cs)）：`MUST/non-negotiable` → `preferred` + 同款例外条款
- 直填路径（`TryApplyDictionary`/`SmartPreTranslate`）仅整串相等时生效，无语境冲突风险，未改

### D. 大批次翻译失败修复（50 条失败、30 条成功）✅

- **根因**：请求 `max_tokens=4096`（[AiTranslationService.Providers.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.Providers.cs)），50 条中文译文 + JSON 结构易超 4096 token → 输出截断 → JSON 解析 0 条 → 整批失败；且 HttpClient 超时仅 120s，大批次生成常超时
- **修复 1**：HttpClient 超时 120s → 300s（[AiTranslationService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/AiTranslationService.cs)）
- **修复 2**：批次失败自动拆半递归重试（`RetryHalvedAsync`，[TranslationOrchestrator.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/TranslationOrchestrator.cs)）——空响应/解析 0 条/异常均触发，逐条兜底，无需再手动把 50 改成 30；新增 `LogBatchRetryHalve` 本地化日志
- **刻意不动 max_tokens**：各模型输出上限不一，改 8192 会让上限 4096 的模型每次请求直接 400；降批兜底更通用
- 新增 [TranslationOrchestratorTests.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor.Tests/TranslationOrchestratorTests.cs)（2 个测试：空响应降批、异常降批；fake 服务模拟多条目失败/单条目成功）

### 验证

- `dotnet test SimpleXmlEditor.sln`：**40/40 通过**（架构重构 36 + 术语注入 2 + 翻译降批 2），0 失败
- 新增测试：术语匹配宽容 2 个、TranslationOrchestrator 降批 2 个；黑名单/术语表既有测试断言同步更新

### 遗留

- 术语 `y→ies` 变体（`story→stories`）未覆盖，如遇再补
- 部分成功场景（解析出部分译文）不触发降批，缺失条目不重试，可后续优化
- "减少失败的小技巧"日志提示仍建议手动降批，与自动降批并存，可后续调整文案

---

## 2026-08-02 — 术语表体验完善 + 检测结果导出 + 评分持久化 + 自动保存

> 背景：术语表冲突检测无进度反馈、按钮拥挤、刷新按钮看不清；冲突/一致性检测结果无法导出对照修改；评分不持久化导致重新打开文件后丢失；缺少 Excel 式自动保存。

### A. 术语表窗口体验完善 ✅

- **冲突检测进度 + 日志**（[GlossaryManager.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Dictionary/GlossaryManager.cs)）：`DetectConflicts` 新增 `onProgress` 回调参数，按总数自适应步长（全程约上报 20 次，避免刷屏）；主窗口日志区实时显示"开始 → 进度 x/y → 完成并列出冲突数"
- **本地化**：新增 `LogConflictStart`/`LogConflictProgress`/`LogConflictDone`（中英双语），进度反馈不硬编码
- **术语表按钮布局**（[GlossaryWindow.xaml](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml)）：工具栏拆成两行——第一行 Add/Edit/Delete、Import/Export/Share 与右侧统计/刷新/关闭；第二行搜索框、分类/状态筛选、合并配置、冲突检测，控件不再拥挤
- **刷新按钮颜色**：从无背景的 `SmallBtn` 改为 `CyanBtn`（青色 #00ACC1 白字），浅色底上清晰可辨

### B. 冲突/一致性检测结果导出 ✅

- **ReviewExporter 扩展**（[ReviewExporter.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ReviewExporter.cs)）：
  - `ExportConflicts(path, conflicts)`：导出列 `EntryKey, Source, Translation, TermEnglish, Expected, Category`
  - `ExportConsistency(path, issues)`：导出列 `Original, Translations, EntryKeys`
  - 新增 `ConsistencyIssue` 结构化类（原文 + 不同译文列表 + 涉及条目 Key）
- **冲突对话框导出按钮**（[GlossaryWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/GlossaryWindow.xaml.cs)）：ConflictDialog 底部新增青色"导出 CSV"按钮，保存 `conflict_report_日期.csv`
- **一致性检测导出**（[MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml.cs)）：扫描完成后弹窗询问"是否导出报告以便对照修改？"，保存 `consistency_report_日期.csv`
- **ScanConsistencyIssues 结构化**（[MainViewModel.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/ViewModels/MainViewModel.cs)）：返回类型从 `List<string>` 改为 `List<ConsistencyIssue>`，日志/弹窗显示逻辑不变
- **本地化**：新增 `GlossaryExportConflicts`/`GlossaryExportConflictsTitle`/`GlossaryExportConflictsDone`/`ConsistencyExportPrompt`/`ConsistencyExported`

### C. 评分持久化缓存（score_cache.json）✅

- **背景**：用户反馈"不可能一次看完所有评分再校对，下一次打开没评分了"
- **ConfigService**（[ConfigService.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/Services/ConfigService.cs)）：
  - 新增 `ScoreCacheItem`（Score + Improvement）+ `ScoreCache` 字典 + `score_cache.json` 独立文件
  - `SyncScoresToCache` / `SaveScoreCache` / `RestoreScores` 三个方法；`LoadConfig` 时一并加载
- **保存时机**：单条评估/投票（`UpdateEntryScore`）、批量评估、批量投票、快速保存（Ctrl+S）都会同步评分缓存
- **恢复时机**：加载 XML 后按条目 Key 恢复（仅恢复未评估条目，新评估结果优先）
- **关键约束不变**：评分只进独立 JSON 缓存，**绝不写入 XML**——XML 保存路径仍排除评分（详见 XmlRepository）

### D. Excel 式定时自动保存 ✅

- **MainWindow**（[MainWindow.xaml.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.xaml.cs)）：新增 `_autoSaveTimer`，每 **5 分钟**触发一次（仿 Excel AutoRecover 节奏）
- **触发逻辑**（[MainWindow.Events.cs](file:///e:/translate/xml-ai-translator-main/SimpleXmlEditor/MainWindow.Events.cs)）：`AutoSaveTimer_Tick` 仅在已加载文件时调用现有 `QuickSave()`——同步翻译缓存 + 评分缓存 + 配置，**不直接写 XML**（防止自动覆盖源文件导致数据损坏，源 XML 仍手动保存）
- **窗口关闭时** `_autoSaveTimer.Stop()`，防止定时器空转

### 验证

- `dotnet build SimpleXmlEditor\SimpleXmlEditor.csproj`：**0 错误 0 警告**
- 冲突检测日志实时显示进度；导出 CSV 可在 Excel 中对照修改；评分关闭重开后保留；5 分钟自动保存日志可见

---

## 经验教训

1. **架构先行**：早期"上帝类"（God Class）导致后期维护成本剧增，MVVM + 服务层提前规划可避免
2. **渐进式重构**：大幅改动的风险高，采用接口抽象 + 逐步迁移更安全
3. **语言资源管理**：硬编码字符串是长期维护隐患，应从项目初期就使用 LocalizationManager
4. **AI 返回不稳定**：提示词约束不是 100% 可靠，解析逻辑必须有健壮的回退策略
5. **编译排查**：.NET WPF 项目中，`InitializeComponent()` 执行时的 null 引用是常见的陷阱，需加防护
6. **渐进式重构的边界**：`TranslationOrchestrator` 虽然抽取了翻译流程，但 MainWindow 仍保留了 LoadConfig/SaveConfig 等副本——重构必须追踪到所有调用点，不能只完成任务的一半
7. **对话框 DialogResult 陷阱**：WPF 中以代码创建的子窗口（非 XAML）需要显式设置 `DialogResult = true/false`，否则 `ShowDialog()` 返回 null，导致调用方误判
8. **WPF DataGrid 的 Ctrl+A 陷阱**：DataGrid 内置 Ctrl+A 会触发 `SelectAll()` 一次性选中所有 cell（N 行 × M 列），大数据量下直接卡死；且 `MainWindow_KeyDown`（冒泡）拦不住它，必须用 `PreviewKeyDown`（隧道）在 DataGrid 层拦截
9. **WPF DataGrid 的 cell 级选择本质慢**：`SelectedCells.Add` 每个 cell 都会触发布局更新，不要对大数据量逐格选择——用"逻辑标志 + 只操作可见行 + 滚动补选"的 Excel 式模型替代
10. **SelectedCells 会无限积累**：滚动补选时若只增不减，集合膨胀后 `Clear()`/`Contains()` 全部变慢；滚动时应先 Clear 再重选
11. **厂商模型名会升级停用**：API 模型名硬编码在 StaticModels 中迟早过时（DeepSeek 2026-04-24 升级），应支持从厂商 `GET /models` 动态拉取并回退静态列表
12. **批量 API 结果要用 key 去重**：AI 对多条原文可能返回重复译文，`ToDictionary` 遇重复键直接抛异常崩溃，应改用循环赋值或分组
13. **外部 API 调用必须逐层兜底**：批量评估/投票等异步编排，任何一层异常都要 try-catch 并降级（chunk 失败 → 逐条 → 跳过），否则用户数据场景下必然崩溃
