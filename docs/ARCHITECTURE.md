# 技术方案

## 基础

应用使用 .NET 10 与 WPF，SQLite 保存本地数据。代码集中在一个应用项目中；本机发布使用依赖 .NET Desktop Runtime 的 win-x64 多文件目录。

## 职责与目录

| 位置 | 职责 |
| --- | --- |
| `Models` | 作品、体验、封面和查询结果中的事实数据；四维评分与日期约束 |
| `Presentation` | 卡片文案、评分档位、日期显示、月份分组和作品库筛选状态 |
| `Views` | 首页、作品库、作品详情；各页面的绑定、布局、焦点和交互事件 |
| `Controls` | 封面堆和封面网格布局 |
| `Operations/LibraryApplication.cs` | 作品与完成记录的保存、删除，操作结果及详情和首页数据加载 |
| `Data` | 显式 SQL、文件与数据库一致性、连接配置、初始化与迁移 |
| `Themes/ApplicationTheme.xaml` | 颜色、字号、间距、表单样式、评分展示和作品卡片模板 |

`App` 负责单实例激活、主题和依赖创建。`MainWindow` 是导航外壳，负责首页、作品库、详情切换，协调编辑窗口与操作结果。页面通过事件请求打开作品或编辑记录。

## 页面与刷新

`LibraryView` 保存类别、搜索词和展开月份；筛选改变时回到月份概览。`DashboardView` 展示首页统计、评分榜、最近完成和时间线。`WorkDetailView` 展示作品、待完成体验、完成记录与封面。

启动时读取作品列表。保存或删除返回 `WorkChange`，包含作品 ID、变更类型和最新作品摘要。主窗口替换或移除该作品，再更新作品库和首页；当前打开的是这部作品时重新加载其详情。封面管理窗口在实际修改后触发刷新。导航使用递增版本，已离开的详情加载结果不会覆盖当前页面。

## 保存与查询

`LibraryApplication` 串行执行作品和完成记录写入，完成日期必填。`ExperienceRules` 验证日期顺序和评分范围；`RatingScale` 统一计算单次 Rank；`ScorePresentation` 统一展示档位。

- `WorkRepository` 保存作品信息并读取作品摘要。
- `ExperienceRepository` 保存体验，在事务内更新派生状态，并提供历史进度读取。
- `CoverRepository` 协调封面文件暂存、写入、重排、清理与失败恢复。删除作品时，它暂存封面目录，由作品仓储删除数据库记录后清理文件。
- `DashboardQueries` 提供完成时间线、作品聚合评分和作者评分榜。
- `LibraryRepository` 提供现有仓储调用的组合入口，委托给上述组件。

作品状态通过 `WorkStatusSql` 的同一规则派生：已开始且未完成的体验优先，其次是已完成体验，其余为计划状态。查询与写入刷新共用该规则。综合评分仅纳入已完成且四项评分完整的体验。

## 数据目录与迁移

`DataPaths` 解析数据目录并约束封面路径。构造数据库对象仅解析配置，初始化时才创建目录。`SqliteConnectionFactory` 注册评分函数并启用外键。`DatabaseMigrator` 按 `PRAGMA user_version` 执行现有迁移；`DatabaseBackup` 创建并检查迁移前副本。

默认目录为 `%LOCALAPPDATA%\QuietShelf`，支持 `YIYE_DATA_DIR` 和 `QUIETSHELF_DATA_DIR`。`works` 保存作品，`experiences` 保存体验，`work_covers` 保存封面索引，`progress_entries` 保存历史进度。旧 `media_entries` 表保留迁移来源。当前 schema 版本为 3。

写入使用参数化 SQL，体验写入与状态刷新使用同一事务。封面采用暂存与恢复机制。当前版本数据库启动时保留原有 journal mode；升级时启用 WAL。应用数据与源码、发布目录、安装目录分别存放。

## 验证

自动化覆盖仓储操作、评分、状态、历史记录、文件恢复、迁移和操作结果。窗口检查覆盖首页多种宽度、月份展开与滚动、详情布局、评分下拉框及封面窗口。使用本机数据时验证副本；安装验收另行检查完整发布目录及实际安装程序启动。

## 为什么暂不选其他方案

- WinUI 3 的部署面更复杂，本项目暂时不需要它独有的能力。
- Electron 会显著增加安装体积与运行开销。
- Tauri 体积小，但引入 Rust 与 Web 前端双栈，对这个单平台小工具没有明显收益。
