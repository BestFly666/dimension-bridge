# XML AI Translator by Veloxcity

🌐 **基于 AI 批量翻译的现代化 XML 本地化工具**

一款功能强大的现代化 WPF 应用程序，使用多种 AI 提供商翻译 XML 本地化文件。具备智能批量处理、实时速率限制和精美的 Material Design 风格界面。

![XML AI Translator](https://img.shields.io/badge/平台-.NET%208.0-blue)
![License](https://img.shields.io/badge/许可证-MIT-green)
![AI](https://img.shields.io/badge/AI-8%20个提供商-orange)

## ✨ 功能特性

### 🤖 **AI 驱动的翻译**
- **8 个 AI 提供商**：Google Gemini、DeepSeek、豆包、千问、智谱、Kimi、文心一言、讯飞星火
- **批量翻译**：单次 API 调用即可翻译多条内容（成本降低 90% 以上）
- **智能 Token 管理**：根据模型 Token 限制自动分批
- **专家配置文件**：针对不同语境的专业翻译配置
- **术语表管理**：内置术语对照表，自动注入翻译指令，确保术语一致性
- **AI 质量评估**：单条翻译评分（0-10 分）+ 多代理投票选出最佳译文

### 🎨 **现代化 UI/UX**
- **Material Design**：精美的现代化界面，带有流畅动画
- **实时进度**：实时进度跟踪，支持暂停/恢复/停止
- **活动日志**：带时间戳的终端风格活动日志
- **响应式设计**：简洁专业的布局，采用卡片式组件
- **多语言界面**：支持中英文界面

### ⚡ **性能与效率**
- **智能速率限制**：针对不同模型的速率限制自动优化
- **翻译缓存**：避免重复翻译相同内容
- **批量处理**：根据 Token 限制每次 API 调用处理 5-20 条内容
- **自动保存**：成功翻译后自动保存

### 🛡️ **可靠性**
- **暂停/恢复**：实时控制翻译进程
- **错误处理**：带重试逻辑的健壮错误处理
- **进度保持**：可从上次中断处继续
- **优雅取消**：干净的资源管理

## 🚀 快速开始

### 前置条件
- **.NET 8.0 运行时**（Windows）
- **AI 提供商 API 密钥**（支持 8 个主流提供商）

### 安装
1. 从 [Releases](../../releases) 下载最新版本
2. 解压 ZIP 文件
3. 运行 `SimpleXmlEditor.exe`

### 设置
1. **获取 API 密钥**：访问您选择的 AI 提供商网站创建 API 密钥
2. **配置**：点击 ⚙️ 设置 → 输入 API 密钥 → 选择提供商 → 刷新模型列表 → 选择模型
3. **加载 XML**：点击 📁 加载，打开您的 XML 本地化文件
4. **翻译**：选择条目，点击 🎯 翻译选中 或 🚀 全部翻译

## 📋 支持的 XML 格式

本应用适用于游戏本地化常用的 Microsoft Excel XML 格式：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet">
  <Worksheet ss:Name="Localization">
    <Table>
      <Row>
        <Cell><Data ss:Type="String">ui.menu.start</Data></Cell>
        <Cell><Data ss:Type="String">Start Game</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>
```

## 🎯 使用技巧

### **批量翻译效率**
- **选择翻译**：使用 Ctrl+点击 选择特定条目，点击 🎯 翻译选中 只翻译勾选的
- **完整翻译**：使用 🚀 全部翻译 处理所有未翻译条目
- **译文合并**：导入译文文件后自动按 Key 合并到已有原文中，无需重新加载

### **速率限制优化**
- **模型限制**：每个模型有不同的速率限制（在设置中显示）
- **自动延迟**：应用程序自动计算请求之间的最佳延迟
- **暂停功能**：如果遇到速率限制，可使用暂停
- **缓存优势**：相同文本会被缓存，避免重复翻译

### **成本优化**
- **批量处理**：API 调用次数减少 90% 以上
- **翻译缓存**：避免重复翻译
- **模型选择**：根据需求选择合适的模型
- **选择性翻译**：只翻译需要的内容

## 🔧 配置

### **支持的 AI 提供商**
- **Google Gemini**：通过 AI Studio 提供免费层级
- **DeepSeek（深度求索）**：高性价比大模型
- **豆包（火山引擎）**：字节跳动旗下模型
- **千问（阿里云）**：通义千问系列模型
- **智谱 AI**：GLM 系列模型
- **Kimi（月之暗面）**：Moonshot 模型
- **文心一言（百度）**：ERNIE 系列模型
- **讯飞星火**：Spark 系列模型

### **支持的模型**
各大提供商均提供多种模型选择，在设置中点击"刷新"即可获取完整列表（含速率限制和价格信息）：

- **Google Gemini**：gemini-2.5-flash、gemini-2.5-pro 等（动态获取）
- **DeepSeek**：deepseek-flash、deepseek-pro
- **豆包**：doubao-pro、doubao-lite、doubao-thinking-pro
- **千问**：qwen-plus、qwen-max、qwen-turbo、qwen-long 等
- **智谱**：glm-4、glm-4-flash、glm-4-air、glm-4.5 等
- **Kimi**：moonshot-v1-8k、moonshot-v1-32k、moonshot-v1-128k
- **文心一言**：ernie-4.0-turbo、ernie-4.0、ernie-3.5、ernie-speed
- **讯飞星火**：general-v3.5、general-v3、general-v2

### **翻译流程**
1. **术语匹配**：检查条目是否在术语表中有精确匹配
2. **缓存检查**：在翻译缓存中查找原文是否已翻译过
3. **AI 翻译**：调用 AI API 进行批量翻译
4. **缓存写入**：将新翻译结果写入缓存

## 🛠️ 开发

### **构建要求**
- Visual Studio 2022 或 VS Code
- .NET 8.0 SDK
- Windows 10/11

### **构建步骤**
```bash
git clone https://github.com/yourusername/xml-ai-translator.git
cd xml-ai-translator
dotnet build SimpleXmlEditor/SimpleXmlEditor.csproj
dotnet run --project SimpleXmlEditor/SimpleXmlEditor.csproj
```

### **项目结构**
```
SimpleXmlEditor/
├── Services/                       # 服务层
│   ├── AiTranslationService.cs     # IAiTranslationService — AI 翻译核心（8 个提供商）
│   ├── ConfigService.cs            # IConfigService — 配置与缓存管理
│   ├── Interfaces.cs               # 服务接口定义
│   ├── TranslationEvaluator.cs     # AI 翻译质量评估与多代理投票
│   ├── TranslationOrchestrator.cs  # 翻译流程编排（分批/术语/缓存/prompt/API）
│   └── XmlRepository.cs            # IXmlRepository — XML 数据访问
├── ViewModels/                     # MVVM 模式
│   └── MainViewModel.cs            # 主窗口视图模型
├── Localization/                   # 多语言支持
│   └── LocalizationManager.cs      # 中英文 UI 本地化（200+ 键值对）
├── Dictionary/                     # 术语表
│   ├── CsvHelper.cs                # CSV 文件操作
│   └── GlossaryManager.cs          # 统一术语表（CRUD/导入导出/冲突检测）
├── ExpertProfiles/                 # 专家翻译配置
│   ├── ExpertProfile.cs            # 配置定义
│   └── ExpertProfileManager.cs     # 配置管理
├── MainWindow.xaml/.cs             # 主界面
├── GlossaryWindow.xaml/.cs         # 术语表管理窗口
├── SettingsWindow.xaml/.cs         # 设置界面（含专家配置编辑器）
├── InputDialog.xaml/.cs            # 通用双输入对话框
├── FileTypeDialog.xaml/.cs         # 文件类型选择对话框
├── App.xaml/.cs                    # 应用入口
├── PromptTemplates.cs              # AI 提示词模板
└── SimpleXmlEditor.csproj          # 项目文件
```

### **架构设计**
应用遵循 **MVVM（Model-View-ViewModel）** 模式，结合 **依赖注入**：

- **View** (`MainWindow.xaml.cs`)：处理 UI 事件和展示
- **ViewModel** (`MainViewModel.cs`)：管理业务逻辑和状态
- **Model**：数据模型和服务契约
- **Services**：封装的业务逻辑（翻译、配置、XML 访问）
- **Interfaces**：通过抽象实现松耦合

## 📊 性能数据

### **效率提升**
- **API 调用**：通过批量处理减少 90% 以上
- **翻译速度**：比单条翻译快 5-10 倍
- **成本节省**：API 成本显著降低
- **速率限制优化**：智能延迟防止 429 错误

### **批量处理示例**
- **1000 条** → 约 50-100 次 API 调用（而非 1000 次）
- **Gemini 2.5 Flash**：每批约 50-100 条
- **GPT-4o**：每批约 30-50 条
- **Token 效率**：安全使用 70% 的模型限制

## 🤝 贡献

欢迎贡献！请随时提交 Pull Request。

### **可贡献方向**
- 更多 AI 提供商支持
- 更多 XML 格式支持
- UI/UX 改进
- 性能优化
- Bug 修复与测试
- 服务单元测试
- 集成测试

## 📄 许可证

本项目基于 MIT 许可证开源 - 详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

- **Google Gemini**、**DeepSeek**、**豆包**、**千问**、**智谱**、**Kimi**、**文心一言**、**讯飞星火** 提供翻译能力
- **Microsoft WPF** 提供 UI 框架
- **Newtonsoft.Json** 提供 JSON 处理
- **Material Design** 提供 UI 设计灵感

## 📞 支持

- **问题反馈**：[GitHub Issues](../../issues)
- **讨论**：[GitHub Discussions](../../discussions)
- **文档**：本 README 与内联代码注释

---

**Made with ❤️ by Veloxcity**

*使用 AI 驱动的批量翻译，彻底改变您的 XML 本地化工作流程！*