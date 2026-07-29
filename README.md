# XML AI Translator

🌐 **基于 AI 批量翻译的现代化 XML 本地化工具**

一款功能强大的 WPF 桌面应用，专为中文游戏本地化人员设计，支持**国内主流 AI 大模型**批量翻译 XML 本地化文件。具备智能分批、翻译缓存、术语表管理、AI 质量评估等完善的本地化工作流功能。

![平台](https://img.shields.io/badge/平台-.NET%208.0-blue)
![许可](https://img.shields.io/badge/许可-MIT-green)
![AI](https://img.shields.io/badge/AI-8%20家国产模型-orange)

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
| **DeepSeek（深度求索）** | deepseek-flash、deepseek-pro |
| **豆包（火山引擎）** | doubao-pro、doubao-lite、doubao-thinking-pro |
| **千问（阿里云）** | qwen-plus、qwen-max、qwen-turbo、qwen-long |
| **智谱 AI** | glm-4、glm-4-flash、glm-4-air、glm-4.5 |
| **Kimi（月之暗面）** | moonshot-v1-8k、moonshot-v1-32k、moonshot-v1-128k |
| **文心一言（百度）** | ernie-4.0-turbo、ernie-4.0、ernie-3.5、ernie-speed |
| **讯飞星火** | general-v3.5、general-v3、general-v2 |

在设置中点击"刷新模型"即可获取完整模型列表，并显示各模型的速率限制和价格信息。

---

## 功能一览

### 翻译核心
- 批量翻译：单次 API 调用翻译多条，比逐条翻译减少 90% 以上的 API 请求
- 智能分批：根据模型 Token 上限自动计算每批条目数量
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
- AI 评估：对单条翻译进行 0-10 分评分，附带改进建议
- 多代理投票：从流畅度、准确度、风格三个维度评估，选出最佳译文

### 用户体验
- Material Design 现代界面，中英文双语切换
- 实时翻译进度、支持暂停/继续/停止
- 崩溃恢复：意外退出后重启可继续翻译
- 批量替换：支持正则搜索替换
- Ctrl+S 快速保存缓存

---

## 快速开始

### 环境要求
- Windows 10 / 11
- [.NET 8.0 运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)

### 三步开始翻译

1. **获取 API Key**：前往任一支持的大模型平台注册并获取 API 密钥
2. **配置工具**：点击右上角 ⚙️ → 选择提供商 → 填入 API Key → 刷新模型 → 选择模型
3. **开始翻译**：加载 XML 文件 → 选中条目或全部翻译 → 导出译文

### 支持的 XML 格式

适用于游戏本地化常用的 Excel XML 格式：

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

## 构建（开发者）

```bash
git clone https://github.com/BestFly666/xml-ai-translator-tool.git
cd xml-ai-translator-tool
dotnet build SimpleXmlEditor/SimpleXmlEditor.csproj
dotnet run --project SimpleXmlEditor/SimpleXmlEditor.csproj
```

**要求**：.NET 8.0 SDK、Windows 10/11

---

## 项目结构

```
SimpleXmlEditor/
├── Services/                    # 核心服务层
│   ├── AiTranslationService.cs  # AI 翻译（8 家国内大模型）
│   ├── ConfigService.cs         # 配置管理 + 缓存 + DPAPI 加密
│   ├── Interfaces.cs            # 服务接口
│   ├── TranslationEvaluator.cs  # 质量评估 + 多代理投票
│   ├── TranslationOrchestrator.cs # 翻译流程编排
│   └── XmlRepository.cs         # XML 读写
├── ViewModels/MainViewModel.cs  # MVVM ViewModel
├── Localization/                # 中英双语 UI
├── Dictionary/GlossaryManager.cs # 术语表管理
├── ExpertProfiles/              # 专家翻译配置
├── MainWindow.xaml/.cs          # 主界面
├── SettingsWindow.xaml/.cs      # 设置窗口
├── GlossaryWindow.xaml/.cs      # 术语表窗口
└── PromptTemplates.cs           # Prompt 模板
```

---

## 许可

MIT License — 详见 [LICENSE](LICENSE)

---

**Made with ❤️ By Veloxcity**  
*为中文游戏本地化社区打造*
