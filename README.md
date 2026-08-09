# 次元译桥（Dimension Bridge）

🌐 **基于 AI 批量翻译的现代化多格式本地化工具**

一款功能强大的 WPF 桌面应用，专为中文游戏本地化人员设计，支持**国内主流 AI 大模型**批量翻译主流本地化文件格式（XML / CSV / JSON / INI / YAML / RESX / PO / TXT / Android）。具备智能分批、翻译缓存、术语表管理、AI 质量评估等完善的本地化工作流功能。

![平台](https://img.shields.io/badge/平台-.NET%208.0-blue)
![许可](https://img.shields.io/badge/许可-MIT-green)
![AI](https://img.shields.io/badge/AI-8%20家国产模型-orange)
![测试](https://img.shields.io/badge/测试-69%20通过-brightgreen)
![CI](https://github.com/BestFly666/dimension-bridge/actions/workflows/ci.yml/badge.svg)

> [!IMPORTANT]
> **当前状态：Preview（预览版）** — 核心翻译流程已在真实的 星球大战重制版模组4.0 游戏汉化项目（翻译 → DAT 写入 → 游戏内实机验证）完整跑通；**其他游戏与二进制格式（.dat 等）尚未验证**，首次使用请先用副本测试。遇到问题请到 [GitHub Issues](https://github.com/BestFly666/dimension-bridge/issues) 反馈。

---

## 为什么选择这个工具？

| 痛点 | 本工具方案 |
|------|-----------|
| 逐条手动翻译效率低 | 批量翻译，一次 API 调用处理 5-100 条 |
| 术语翻译不统一 | 内置术语表，自动注入 Prompt，保证一致性 |
| 重复翻译浪费配额 | 翻译缓存自动去重，相同内容绝不重复翻译 |
| AI 翻译质量难评估 | 单条评分（0-10）+ 多代理投票，量化翻译质量 |
| 不同语境翻译不准 | 专家配置文件，针对游戏/影视等场景定制 |
| 翻译过程崩溃丢进度 | 崩溃恢复机制，重启后续传 |

---

## 支持的大模型

全部为国内可直接使用的 AI 服务：

| 提供商 | 可用模型示例 |
|--------|------------|
| **Google Gemini** | gemini-2.5-flash、gemini-2.5-pro（动态获取全部） |
| **DeepSeek（深度求索）** | deepseek-v4-flash、deepseek-v4-pro |
| **豆包（火山引擎）** | doubao-pro、doubao-lite、doubao-thinking-pro |
| **千问（阿里云）** | qwen-plus、qwen-max、qwen-turbo、qwen-long |
| **智谱 AI** | glm-4、glm-4-flash、glm-4-air、glm-4.5 |
| **Kimi（月之暗面）** | moonshot-v1-8k、moonshot-v1-32k、moonshot-v1-128k |
| **文心一言（百度）** | ernie-4.0-turbo、ernie-4.0、ernie-3.5、ernie-speed |
| **讯飞星火** | general-v3.5、general-v3、general-v2 |

在设置中点击"刷新模型"即可从厂商服务器在线拉取完整模型列表，并显示各模型的速率限制和价格信息（厂商升级模型名也不会失效）。

---

## 功能一览

### 翻译核心
- 批量翻译：单次 API 调用翻译多条，比逐条翻译减少 90% 以上的 API 请求
- 智能分批：按估算输出 Token 预算动态分批（3800 token/批），中文约 20-30 条/批，避免输出截断
- 并发翻译：3 路批次并发 + 动态 429 退避，有效提升大批量翻译吞吐量
- 条目 Key 注入：翻译时注入条目 Key（如 `TEXT_UNIT_*`），帮助 AI 理解语境，提升翻译准确率
- 批次计时：每批次翻译完成后输出耗时统计，便于定位性能瓶颈
- 翻译缓存：已翻译内容自动缓存，避免重复调用 API
- 译文合并：导入译文文件时自动按 Key 匹配合并，无需重新加载原文
- 强制重译：选中条目支持绕过缓存重新翻译

### 术语管理
- 术语对照表：中英文术语对，支持 CSV/JSON 导入导出
- 自动注入：翻译时自动将相关术语注入 Prompt，保证一致性
- 词边界匹配：智能匹配完整单词，避免误匹配
- 冲突检测：检测同一英文对应多个中文译文

### 专家配置
- 不同项目/语境可创建不同的专家翻译配置
- 自定义翻译背景知识（Context），AI 翻译更贴合场景
- 内置星球大战、漫威等游戏本地化示例配置

### 质量保障
- AI 评估：对翻译进行 0-10 分评分，附带改进建议，**评分直接显示在表格"评分"列**（颜色编码 + 点击列头排序）
- 多代理投票：从流畅度、准确度、风格三个维度评估，自动应用最佳译文（可撤销）
- 独立评估模型：可配置与翻译不同的厂商/模型用于评估和投票，打破"AI 自己检查自己"的同源偏差
- **评分持久化**：评分与改进建议自动缓存到本地（score_cache.json），重新打开文件后评分列保留，可分批校对
- **检测结果导出**：术语冲突检测、一致性检测结果可一键导出 CSV，方便在 Excel 中对照修改

### 用户体验
- Material Design 现代界面，中英文双语切换
- Excel 式选择：单元格/整行/整列/全选（Ctrl+A），大文件毫秒级响应
- 实时翻译进度、支持暂停/继续/停止
- 崩溃恢复：意外退出后重启可继续翻译
- **自动保存**：每 5 分钟自动保存缓存与配置（不覆盖源文件），类似 Excel 的 AutoRecover
- 批量替换：支持正则搜索替换
- 撤销：批量替换、AI 应用、手动编辑均可 Ctrl+Z 撤销，撤销后自动跳转到对应行
- Ctrl+S 快速保存缓存（只写缓存，不覆盖源文件）

### 黑名单过滤
- 按 Key 前缀匹配跳过指定条目（如 `TEXT_SPEECH_` 语音文本），不消耗 API 配额
- 黑名单条目在表格中可选隐藏，翻译时自动排除
- 规则持久化到本地，重启后保留

---

## 快速开始

### 环境要求
- Windows 10 / 11
- [.NET 8.0 运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)

### 三步开始翻译

1. **获取 API Key**：前往任一支持的大模型平台注册并获取 API 密钥
2. **配置工具**：点击右上角 ⚙️ → 选择提供商 → 填入 API Key → 刷新模型 → 选择模型
3. **开始翻译**：加载文件（XML / CSV / JSON / INI / YAML / RESX 等）→ 选中条目或全部翻译 → 导出译文

> **可选**：设置 → "评估模型"Tab 可配置与翻译不同的厂商/模型（如用 DeepSeek 翻译、用智谱评估），打破"AI 自己检查自己"的同源偏差；留空则评估复用翻译模型。

### 支持的文件格式

通过插件系统（`IFileFormatPlugin`）支持主流本地化文件格式，打开/保存时自动识别，编码自动检测并按原编码写回：

| 格式 | 扩展名 | 说明 |
|------|--------|------|
| Excel XML | .xml | 游戏本地化常用 Excel Spreadsheet XML（`<?mso-application progid="Excel.Sheet"?>`，自动识别） |
| 自定义 XML | .xml | `LocalisationData` 结构（Key / 原文 / 译文 CDATA） |
| Android | .xml | `strings.xml` 资源文件 |
| CSV | .csv | 自动识别 3 列（Key / 原文 / 译文）或 2 列（Key / Value） |
| JSON | .json | i18n 格式，扁平或嵌套 key-value |
| INI | .ini | `[Section]` 段 + `key=value` |
| YAML | .yaml / .yml | 嵌套字典 / 数组 |
| RESX | .resx | .NET 资源文件 |
| Java Properties | .properties | Java 规范转义（`\uXXXX` / `\:` / 续行） |
| PO | .po | Gettext 翻译文件 |
| TXT | .txt | 键值对文本 |

> 编码处理：文本格式自动检测 UTF-8（含 BOM）与 GBK，保存时按原编码写回，国内 Excel 打开不乱码。

示例 — Excel XML 格式：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet">
  <Worksheet ss:Name="Localization">
    <Table>
      <Row>
        <Cell><Data ss:Type="String">ui.menu.start</Data></Cell>
        <Cell><Data ss:Type="String">开始游戏</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>
```

---

## 已知问题与限制

- **已验证范围**：完整流程（翻译 → 术语表 → 评估/投票 → 导出 → 游戏内验证）仅在 4.0 游戏上实测通过；**其他游戏与二进制格式（.dat 等）未验证**，请先用副本测试。
- **数据安全**：Ctrl+S / 自动保存只更新本地缓存（`translation_cache.json`，存于 `%LocalAppData%\SimpleXmlEditor\`），**不会覆盖源文件**；只有显式点击"保存"按钮才导出（译文替换原文值供游戏使用）。仍建议定期备份源文件。
- **游戏内断行适配**：中文在游戏引擎内的断行处理（如 4.0 的 79 字符强制换行）是**引擎专用**的，由独立脚本维护（`scripts/` 目录，不在本工具内），换行参数需按游戏自行调整。
- **语音/界面文本**：`TEXT_SPEECH` 等语音文本与单位描述类条目不支持手动换行，游戏使用自动换行。

## 反馈与支持

- 报告 Bug / 建议功能：前往 [GitHub Issues](https://github.com/BestFly666/dimension-bridge/issues) 创建 Issue
- 反馈前请提供：使用的 AI 提供商/模型、文件样例片段、操作步骤、预期结果与实际结果

---

## 构建（开发者）

```bash
git clone https://github.com/BestFly666/dimension-bridge.git
cd dimension-bridge
dotnet build
dotnet run --project SimpleXmlEditor/SimpleXmlEditor.csproj
```

**要求**：.NET 8.0 SDK、Windows 10/11

### 运行测试

```bash
dotnet test SimpleXmlEditor.Tests/SimpleXmlEditor.Tests.csproj
# 当前: 69/69 通过，0 失败，0 跳过
```

### CI/CD

每次 push 自动触发 GitHub Actions：restore → build → test → publish (win-x64 自包含单文件)

---

## 项目结构

```
project-root/
├── SimpleXmlEditor/                     # WPF 主项目
│   ├── Services/                        # 服务层（全部接口化）
│   │   ├── AiTranslationService.cs      # IAiTranslationService — AI 翻译核心（8 个提供商）
│   │   ├── AiTranslationService.Models.cs   # 模型列表与速率限制
│   │   ├── AiTranslationService.Providers.cs # 各提供商 API 实现
│   │   ├── AiResponseParser.cs          # AI 响应解析（截断检测 + 三级回退）
│   │   ├── ConfigService.cs             # IConfigService — 配置管理（DPAPI 加密）
│   │   ├── ConfigService.Cache.cs       # 缓存读写（translation_cache / progress）
│   │   ├── ConfigService.Scores.cs      # 评分缓存管理
│   │   ├── Interfaces.cs                # 服务接口定义
│   │   ├── TranslationEvaluator.cs      # ITranslationEvaluator — 质量评估 + 多代理投票
│   │   ├── TranslationOrchestrator.cs   # 翻译流程编排（token预算分批/术语/缓存/prompt）
│   │   ├── XmlRepository.cs             # IXmlRepository — XML 数据访问
│   │   └── PluginLoader.cs              # IFileFormatPlugin 插件加载器（动态收集扩展名）
│   ├── ViewModels/
│   │   ├── MainViewModel.cs             # MVVM ViewModel（主类）
│   │   ├── MainViewModel.Translation.cs # 翻译流水线（并发/暂停/进度/计时）
│   │   ├── MainViewModel.EntryProcessing.cs # 条目加载与缓存恢复
│   │   ├── MainViewModel.Cache.cs       # 缓存同步
│   │   ├── MainViewModel.Undo.cs        # 撤销快照
│   │   ├── MainViewModel.Consistency.cs # 一致性检测
│   │   └── ...                          # 其他 partial 拆分
│   ├── Windows/                         # WPF 窗口（partial class 拆分）
│   │   ├── MainWindow.xaml/.cs          # 主界面
│   │   ├── MainWindow.Events.File.cs    # 文件加载/保存/QuickSave
│   │   ├── MainWindow.Handlers.cs       # 事件订阅（BeginInvoke 异步）
│   │   ├── MainWindow.Grid.cs           # DataGrid 交互
│   │   ├── SettingsWindow.xaml/.cs      # 设置界面
│   │   └── ...                          # 其他窗口与 partial 拆分
│   ├── Localization/
│   │   ├── LocalizationManager.cs       # 中英文 UI 本地化
│   │   ├── LocalizationManager.Dicts.Zh.cs  # 中文文案
│   │   └── LocalizationManager.Dicts.En.cs  # 英文文案
│   ├── Dictionary/
│   │   ├── BlacklistManager.cs          # IBlacklistManager — 黑名单过滤
│   │   ├── GlossaryManager.cs           # IGlossaryManager — 术语表管理
│   │   └── ...                          # Glossary partial 拆分
│   ├── Utils/
│   │   └── PromptTemplates.cs           # AI 提示词模板
│   ├── Plugins/
│   │   ├── XmlFilePlugin.cs            # Excel / 自定义 XML 格式插件
│   │   ├── AndroidStringsPlugin.cs     # Android strings.xml 插件
│   │   ├── CsvFilePlugin.cs            # CSV 插件（列结构自动识别）
│   │   ├── JsonI18nPlugin.cs           # JSON i18n 插件
│   │   ├── IniFilePlugin.cs            # INI 插件（[Section] 段）
│   │   ├── YamlFilePlugin.cs           # YAML 插件（YamlDotNet）
│   │   ├── ResxFilePlugin.cs           # .NET RESX 插件
│   │   ├── PropertiesFilePlugin.cs     # Java Properties 插件
│   │   ├── PoFilePlugin.cs             # Gettext PO 插件
│   │   ├── TxtFilePlugin.cs            # TXT 键值对插件
│   │   └── TextEncodingDetector.cs     # 共享编码检测（UTF-8/GBK）
│   ├── ExpertProfiles/                  # 专家配置
│   └── SimpleXmlEditor.csproj           # .NET 8.0 WPF 项目文件
├── SimpleXmlEditor.Tests/               # xUnit 测试项目（58 个测试）
│   ├── TranslationOrchestratorTests.cs  # 分批/翻译/Key注入 测试
│   ├── BlacklistManagerTests.cs         # 黑名单过滤测试
│   ├── XmlRepositoryTests.cs            # XML 读写测试
│   ├── GlossaryManagerTests.cs          # 术语表测试
│   └── SimpleXmlEditor.Tests.csproj
├── .github/workflows/ci.yml             # GitHub Actions CI/CD
├── scripts/                             # 附加工具脚本（参考示例）
├── DEVELOPMENT_LOG.md                   # 开发日志
├── HANDOVER.md                          # 交接文档
├── PROJECT_INDEX.md                     # 项目文件索引
├── PRODUCT_PLAN.md                      # 产品规划
└── README.md                            # 项目说明
```

### 架构设计

应用遵循 **MVVM（Model-View-ViewModel）** 模式 + **依赖注入**：

- **View**：WPF 窗口，处理 UI 事件和展示
- **ViewModel** (`MainViewModel`)：管理业务逻辑和状态，由 DI 容器构造注入
- **Model**：数据模型和服务契约
- **Services**：封装业务逻辑，全部通过接口实现松耦合
- **DI 容器**：`Microsoft.Extensions.DependencyInjection`，`App.xaml.cs` 中统一注册

---

## 许可

MIT License — 详见 [LICENSE](LICENSE)  
*基于 Veloxcity 的原始项目扩展维护*

---

**Made with ❤️ By Veloxcity & BestFly666**  
*为中文游戏本地化社区打造*
