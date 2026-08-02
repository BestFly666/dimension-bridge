# XML AI Translator — 产品规划

> **规划人**：产品经理 Alex  
> **日期**：2026-07-30  
> **版本**：1.0  
> **状态**：已审批，待执行

---

## 一、产品定位与愿景

### 1.1 一句话定位

**面向中文游戏本地化译者（独立汉化者/小型汉化组）的专业 AI 翻译工作站**

> 2026-08-01 决策：**先专注译者**。独立游戏开发者极简版暂缓（见「四、不做的事情」），待译者版验证后再从现有代码派生。

### 1.2 核心价值主张

帮助游戏本地化团队将数千条英文文本快速转化为高质量中文译文，通过 AI 智能翻译 + 术语一致性管理 + 专家领域知识注入，实现 **质量可控、术语统一、成本可算** 的专业本地化流程。

### 1.3 目标用户

| 用户类型 | 规模 | 核心需求 | 痛点 |
|----------|------|----------|------|
| 独立汉化者 | 1-2 人 | 快速翻译 XML 文本 | 人工翻译效率低，AI 翻译术语不一致 |
| 小型汉化组 | 3-10 人 | 协作翻译 + 术语统一 | 多人翻译风格不统一，重复翻译浪费 |
| Mod 作者 | 1 人 | 快速本地化游戏 Mod | 需要处理多种 XML 格式，工具链不统一 |

> 2026-08-01 决策：以上三者为**主用户群（译者）**，产品资源全部投入此处。独立游戏开发者列为**潜在第二用户群**，暂缓开发。

### 1.4 竞争定位

| 维度 | 通用翻译工具（DeepL/Google） | CAT 工具（Trados/memoQ） | XML AI Translator（我方） |
|------|---------------------------|-------------------------|--------------------------|
| XML 支持 | 不支持或有限 | 复杂配置 | 原生 Excel XML 支持 |
| 游戏术语 | 无 | 需手动维护 | 内置术语表 + 倒排索引 |
| 批量翻译 | 按字数计费 | 企业级定价 | 按 API Token 计费，成本可控 |
| 上手难度 | 低 | 极高 | 中低（桌面应用开箱即用） |
| 社区定位 | 全球通用 | 企业级 | **中文游戏社区专属** |

---

## 二、当前状态评估

### 2.1 功能完成度

| 模块 | 完成度 | 说明 |
|------|--------|------|
| AI 翻译核心 | 90% | 8 个提供商，批量翻译，速率限制 |
| 翻译缓存 | 85% | 基于 MD5 的缓存，增量保存 |
| 术语表管理 | 85% | CRUD、导入导出、冲突检测、倒排索引 |
| 专家配置 | 70% | 基础 CRUD，术语注入 |
| UI 本地化 | 85% | 中英文支持，部分遗漏 |
| XML 处理 | 80% | 两种格式支持，译文合并 |
| 翻译评估 | 40% | API 调用实现，无 UI |
| 多代理投票 | 30% | 代码实现，未集成到 UI |

### 2.2 技术债务清单

根据审计报告，存在 8 个已知问题（详见 [HANDOVER.md](HANDOVER.md#7-已知问题2026-07-29-终审)）：

| 优先级 | 数量 | 类型 |
|--------|------|------|
| P0 (严重) | 1 | 线程安全 |
| P1 (高) | 2 | 接口缺失 + 代码重复 |
| P2 (中) | 3 | 代码重复 + UI 本地化 + 资源泄漏 |
| P3 (低) | 2 | 错误处理 + 死代码 |

### 2.3 架构成熟度

```
MVVM 过渡完成度：≈70%
  UI 层 (WPF)          ✓ 完成
  ViewModel 层          ✓ 基本完成（30+ 字段）
  Service 层接口        △ 3/6 已实现接口
  依赖注入              ✗ 手动构造，无 DI 容器
  单元测试              ✗ 0 测试用例
  CI/CD                 ✗ 无
```

---

## 三、产品路线图

### 总览

```
Phase 1: 稳固根基 ──→ Phase 2: 架构完善 ──→ Phase 3: 功能增强
   (2-3 周)              (2-4 周)              (3-5 周)
  技术债务清零            测试 + CI/CD            差异化功能
       │                      │                      │
       └──────────────────────┴──────────────────────┘
                              │
                         Phase 4: 生态扩展
                            (4-8 周)
                         插件系统 + CLI + 协作
```

---

### Phase 1: 稳固根基（P0-P1 技术债务清零）

**目标**：消除所有审计发现的严重和高优先级问题，为后续开发建立安全基础。

**北极星指标**：8 个已知问题全部关闭，0 个 P0/P1 残留。

#### 1.1 线程安全修复 [P0]

| 任务 | 文件 | 当前状态 | 目标状态 |
|------|------|----------|----------|
| Cache 改为 ConcurrentDictionary | ConfigService.cs | `Dictionary<string,string>` | `ConcurrentDictionary<string,string>` |
| RecentRequests 改为 ConcurrentQueue | AiTranslationService.cs | `Queue<DateTime>` | `ConcurrentQueue<DateTime>` |
| 验证并发场景 | 翻译全部 3000+ 条 | 可能存在竞态 | 线程安全，无数据竞争 |

**验收标准**：
- [ ] 并发翻译 3000 条条目，缓存命中数和 API 调用数统计准确
- [ ] 多次暂停/恢复不产生异常
- [ ] 快速连续操作（导入→翻译→保存→清空）无崩溃

#### 1.2 接口补全 [P1]

| 任务 | 新增接口 | 影响范围 |
|------|----------|----------|
| IGlossaryManager | 术语表 CRUD + 查询 + 导入导出 | TranslationOrchestrator, MainViewModel |
| IExpertProfileManager | 专家配置 CRUD + 合并 | SettingsWindow, TranslationOrchestrator |
| ITranslationEvaluator | 评估 + 投票 | MainWindow (未来 UI 集成) |

**验收标准**：
- [ ] `TranslationOrchestrator` 依赖接口而非具体类
- [ ] `MainViewModel` 通过接口注入所有依赖
- [ ] 编译通过，功能无回归

#### 1.3 消除 MainWindow 重复代码 [P1]

| 任务 | 删除的方法 | 统一到 |
|------|-----------|--------|
| LoadConfig | MainWindow.LoadConfig() | MainViewModel / ConfigService |
| SaveConfig | MainWindow 中的内联调用 | ConfigService.SaveConfig() |
| SaveTranslationProgress | MainWindow 中的方法 | ConfigService (已有) |
| RestoreTranslationProgress | MainWindow 中的方法 | ConfigService (已有) |
| SyncEntriesToCache | MainWindow 中的方法 | ConfigService.SyncEntriesToCache() |
| HasChineseChars | MainWindow 中的方法 | 提取为公共扩展方法 |

**验收标准**：
- [ ] MainWindow.xaml.cs 减少 50+ 行重复代码
- [ ] 所有业务逻辑走 ViewModel → Service 路径
- [ ] 功能无回归

#### 1.4 其他 P2 修复 [P2]

| 任务 | 说明 |
|------|------|
| 提取 HasChineseChars 公共方法 | 创建 `StringExtensions.cs`，消除 MainWindow + Orchestrator 重复 |
| 术语表状态本地化 | 状态筛选框显示"已确认/待审核/已拒绝"而非 "confirmed/pending/rejected" |
| HttpRequestMessage Dispose | 循环中所有 HttpRequestMessage 添加 `using` |

---

### Phase 2: 架构完善（质量基础设施）

**目标**：建立测试框架和 CI/CD 流水线，完成 MVVM 架构最后一公里。

**北极星指标**：核心服务测试覆盖率 ≥ 60%，CI 流水线可用。

#### 2.1 依赖注入容器

| 任务 | 说明 |
|------|------|
| 引入 DI 容器 | 使用 `Microsoft.Extensions.DependencyInjection` |
| 注册服务 | 在 `App.xaml.cs` 中配置服务生命周期（Singleton/Transient） |
| 重构 MainWindow | 通过构造函数注入 `MainViewModel` |

```csharp
// 目标架构
services.AddSingleton<IConfigService, ConfigService>();
services.AddSingleton<IGlossaryManager, GlossaryManager>();
services.AddSingleton<IExpertProfileManager, ExpertProfileManager>();
services.AddSingleton<IAiTranslationService, AiTranslationService>();
services.AddSingleton<ITranslationEvaluator, TranslationEvaluator>();
services.AddSingleton<IXmlRepository, XmlRepository>();
services.AddSingleton<TranslationOrchestrator>();
services.AddSingleton<MainViewModel>();
services.AddTransient<MainWindow>();
```

#### 2.2 单元测试框架

| 任务 | 技术选型 | 覆盖范围 |
|------|----------|----------|
| 测试项目 | xUnit + Moq | `SimpleXmlEditor.Tests` |
| ConfigService 测试 | Mock 文件系统 | 缓存读写、配置序列化、进度恢复 |
| GlossaryManager 测试 | 内存术语表 | CRUD、倒排索引、冲突检测、CSV/JSON 导入导出 |
| TranslationOrchestrator 测试 | Mock AI 服务 | 分批逻辑、Prompt 构建、术语注入 |
| AiTranslationService 测试 | Mock HttpClient | 速率限制计算、费用计算、API Key 管理 |

**目标覆盖率**：
- ConfigService: 80%
- GlossaryManager: 75%
- TranslationOrchestrator: 60%
- AiTranslationService: 50%

#### 2.3 CI/CD 流水线

| 任务 | 工具 | 触发条件 |
|------|------|----------|
| 编译检查 | `dotnet build` | 每次 push |
| 单元测试 | `dotnet test` | 每次 push |
| 代码分析 | .NET Analyzers | 每次 push |
| 自动发布 | `dotnet publish` | tag push |

```yaml
# GitHub Actions 示意
name: CI
on: [push, pull_request]
jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build SimpleXmlEditor
      - run: dotnet test SimpleXmlEditor.Tests
      - run: dotnet publish SimpleXmlEditor -c Release -r win-x64 --self-contained
```

#### 2.4 错误处理规范化 [P3]

| 任务 | 说明 |
|------|------|
| 空 catch 块处理 | 所有空 catch 至少添加 `Debug.WriteLine` 或 `_logger.LogWarning` |
| 日志系统引入 | 使用 `ILogger<T>`（或简易版 `LogService`）替代零散的 `LogMessage` 事件 |
| 用户友好错误提示 | 网络异常、API 限流等常见错误给中文提示而非原始异常 |

---

### Phase 3: 功能增强（差异化竞争力）

**目标**：上线翻译质量评估 UI、完善多代理投票、新增 CLI 模式，形成与通用工具的核心差异化。

**北极星指标**：用户翻译质量满意度（通过内置评分功能收集）≥ 4/5。

#### 3.1 翻译质量评估 UI 集成

当前状态：`TranslationEvaluator` 代码已实现，但无 UI 入口。

| 功能 | 交互方式 | 说明 |
|------|----------|------|
| 单条评估 | DataGrid 右键 → "AI 评估此条" | 返回 0-10 评分 + 解释 + 改进建议 |
| 批量评估 | 翻译全部后自动运行 | 对低分条目（<6 分）高亮标记 |
| 评估面板 | 底部 Tab 页或侧面板 | 展示评分分布图、常见问题汇总 |

#### 3.2 多代理投票功能完善

| 功能 | 说明 |
|------|------|
| 投票配置 | 选择 2-3 个 AI 提供商参与投票 |
| 权重设置 | 可配置各代理（Fluency/Accuracy/Style）权重 |
| 结果展示 | 显示各候选译文及其得分，自动选择最佳 |

#### 3.3 CLI 模式

面向自动化脚本和 CI/CD 集成场景：

```
# 单文件翻译
XmlAiTranslator translate -i input.xml -o output.xml -p GoogleGemini -l Chinese

# 批量翻译目录
XmlAiTranslator batch -d ./xml_files/ -p DeepSeek

# 导出翻译记忆（TMX 格式）
XmlAiTranslator export-tmx -i input.xml -o memory.tmx
```

| 命令 | 功能 |
|------|------|
| `translate` | 单文件翻译 |
| `batch` | 目录批量翻译 |
| `export-tmx` | 导出 TMX 标准翻译记忆 |
| `import-tmx` | 导入 TMX 补充缓存 |
| `validate` | 验证 XML 格式 + 术语一致性检查 |

#### 3.4 翻译进度可视化优化

| 改进 | 当前 | 目标 |
|------|------|------|
| 进度条 | 仅文字百分比 | 带进度条 + 剩余时间估算 |
| 实时速度 | 无 | 显示"条/秒"翻译速度 |
| 费用实时更新 | 翻译结束后显示 | 实时累计费用显示 |
| 暂停/恢复状态 | 按钮文字切换 | 视觉状态指示器（绿色运行/黄色暂停/灰色停止） |

#### 3.5 协作功能（轻量级）

| 功能 | 说明 |
|------|------|
| 翻译状态标记 | 人工审核状态：未审/已审/需修改 |
| 冲突标记 | 同一 Key 被多人修改时标记冲突 |
| 导出审校报告 | Excel 格式，标记待审核 / 低分条目 |

---

### Phase 4: 生态扩展（长期愿景）

**目标**：从单一工具发展为本地化平台，建立社区生态。

#### 4.1 插件系统

| 能力 | 说明 |
|------|------|
| 翻译服务插件 | 第三方可开发新 AI 提供商插件 |
| 文件格式插件 | 支持自定义 XML Schema、JSON、YAML 等格式 |
| 后处理插件 | 翻译后自动格式化、去重、质量检查 |

#### 4.2 更多文件格式支持

| 格式 | 优先级 | 说明 |
|------|--------|------|
| `.po` / `.pot` | 高 | Gettext 格式，大量开源项目使用 |
| Android `strings.xml` | 中 | 移动端本地化 |
| iOS `.strings` | 中 | iOS 本地化 |
| JSON i18n | 中 | Web 前端本地化格式 |

#### 4.3 社区建设

| 事项 | 说明 |
|------|------|
| 术语表市场 | 社区共享游戏术语表（如"星空术语表"、"原神术语表"） |
| 专家配置分享 | 导入/导出专家配置，一键切换翻译风格 |
| 文档完善 | API 文档、插件开发指南、贡献指南 |

---

## 四、不做的事情（Non-Goals）

明确说明本项目**不会**涉足的领域，防止范围蔓延：

| 请求 | 原因 | 何时重新考虑 |
|------|------|-------------|
| 图片/视频本地化 | 非本项目定位，技术栈不匹配 | 如果有独立团队介入 |
| 网页版/SaaS 化 | 桌面工具定位，目标用户需要离线能力 | 如果用户强烈需求在线协作 |
| 机器翻译引擎自研 | 成本极高，AI API 已满足需求 | AI API 成本不可接受时 |
| CAT 工具全功能对齐 | Trados/memoQ 级功能需要企业级投入 | 不考虑，保持差异化 |
| 独立开发者极简版（2026-08-01 暂缓） | 双用户群并行会造成"两头不讨好"，先验证译者版护城河（术语一致性） | 译者版验证通过后，从现有代码派生极简版（复用核心引擎），边际成本低 |
| 低置信度条目联网搜索补翻（2026-08-02 延后） | ① 需求未验证（无用户证据，仅担心生僻词翻不准）② 8 家厂商联网搜索方式各不相同，适配成本高（DeepSeek 需走 Responses API、千问 enable_search、智谱/Kimi 工具声明……）③ 游戏专有名词的官方译名更多依赖术语表与社区资料，通用搜索收益有限 | 出现真实信号：用户反馈"生僻词翻译差"为高频痛点，且"低分条目人工复核清单"（见 3.1/3.5）验证后仍覆盖不足时，以**单厂商 MVP**（DeepSeek v4-flash Responses API 或千问 enable_search）+ 可选开关形式重新评估 |

---

## 五、成功指标

### 5.1 北极星指标

**活跃用户周翻译条目数**（衡量工具实际使用深度）

### 5.2 各阶段 KPI

| Phase | 核心指标 | 目标值 |
|-------|----------|--------|
| Phase 1 | P0/P1 问题清零 | 100% 关闭 |
| Phase 2 | 核心服务测试覆盖率 | ≥ 60% |
| Phase 2 | CI 流水线可用 | 每次 push 自动构建+测试 |
| Phase 3 | 翻译评估功能可用 | 单条 + 批量评估 |
| Phase 3 | CLI 基本功能 | translate/batch/export-tmx 三条命令 |
| Phase 4 | 插件接口稳定 | 至少 1 个第三方插件验证 |

### 5.3 质量指标

| 指标 | 当前基线 | Phase 3 目标 |
|------|---------|-------------|
| 翻译一致性（术语命中率） | ~85% | ≥ 95% |
| 缓存命中率（重译避免率） | 取决于术语表大小 | ≥ 80% |
| 用户翻译后人工修改率 | 未测量 | < 10% |
| 应用崩溃率 | 低频（术语表窗口已修复） | 0 已知崩溃 |

---

## 六、风险评估

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| AI API 服务不稳定 | 中 | 高 | 多提供商冗余 + 本地缓存 |
| API 费用超预期 | 中 | 中 | 翻译前费用预估 + 批次大小限制 |
| WPF 技术栈人才稀少 | 中 | 低 | 文档完善 + 架构简单 |
| 用户增长超预期导致性能问题 | 低 | 中 | 倒排索引已解决核心性能瓶颈 |
| 游戏格式变化不兼容 | 低 | 高 | XmlRepository 支持多格式，新格式通过插件扩展 |

---

## 七、资源估算

### 7.1 工作量预估（单人开发）

| Phase | 预估工时 | 核心产出 |
|-------|----------|----------|
| Phase 1: 稳固根基 | 2-3 周 | 技术债务清零，架构安全 |
| Phase 2: 架构完善 | 2-4 周 | 测试框架 + CI/CD |
| Phase 3: 功能增强 | 3-5 周 | 评估 UI + CLI + 可视化 |
| Phase 4: 生态扩展 | 4-8 周 | 插件系统 + 多格式 + 社区 |
| **合计** | **11-20 周** | 从单一工具到本地化平台 |

### 7.2 当前人力

- 开发：1 人（Veloxcity）
- 测试：0 人（依赖自动化测试）
- 设计：通过 AI 辅助设计

---

## 八、立即行动项（本周可启动）

按优先级排列：

1. **[P0]** ConfigService.Cache → ConcurrentDictionary（影响：线程安全）
2. **[P0]** AiTranslationService.RecentRequests → ConcurrentQueue（影响：速率限制准确性）
3. **[P1]** 为 GlossaryManager、ExpertProfileManager、TranslationEvaluator 抽取接口
4. **[P1]** 消除 MainWindow 中的 LoadConfig/SaveConfig/SaveTranslationProgress 等重复代码
5. **[P1]** 提取 HasChineseChars 为公共扩展方法
6. **[P2]** HttpRequestMessage 添加 using/Dispose
7. **[P2]** 术语表状态本地化

---

> **版本历史**  
> v1.2 (2026-08-02) — Non-Goals 新增：低置信度条目联网搜索补翻延后（记录延后理由与重新评估条件）  
> v1.1 (2026-08-01) — 定位收窄：主攻译者，独立开发者极简版暂缓（Non-Goals 新增）  
> v1.0 (2026-07-30) — 初始产品规划，由产品经理 Alex 基于项目审计制定
