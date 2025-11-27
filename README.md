# README



## Introduction

这一个简单的 Unity 项目脚手架，约 5000 行代码左右。其中代码命名清晰、附有注释，且通过多轮 Gemini 3 Pro AI 代码审查避免基本错误。

部分灵感来自我的另一个项目（[游戏内 CLI 与 xLua 热重载工具](https://github.com/HxxWorkAccount/unity-ingame-cli-with-xlua)），实现该项目的目的是想为自己未来的 Unity 项目提供一个**可复用的基础框架**，

### Feature

包含以下功能：

#### 项目结构划分
- 划分发布类型（Release、Dev、Test），划分执行环境（Client、Server、Host）

  已在对应目录设置自定义程序集，并设置依赖关系、编译符号。其中 Editor、Tests、Dev 环境可以引用 Release 环境代码。

- 基于 C/S 和发布类型的项目结构划分，可参考[初始化空目录脚本](Tools/InitEmptyProject.py)

#### 编译控制
提供一个 [Editor GUI 面板](Assets/Editor/Scripts/Build_/BuildControl.cs)做控制中心。
![](.Images/2025-11-28-01-02-39.png)
- 支持基于发布类型、执行环境等因素，[控制编译符号](Assets/Editor/Scripts/Build_/BuildControl.cs)，并**剔除不需要的程序集**
- 环境宏，目前为了避免频繁切换 PlayerSettings 的编译符号，暂时将 Editor 环境[设为全部启用](Assets/Scripts/DevPattern/Universal/Utils/EnvironmentUtils.cs)
- 通过 ScriptableObject 存储构建配置，可以在 Editor 面板中创建、编辑、保存
- 自动控制 BuildPipeline、Addressables 的构建环境、构建配置
- 提供构建备份等后处理

#### 编译时资源剔除
通过重写 Addressables 的 [BuildScriptPackedMode](Assets/Editor/Scripts/Build_/CustomBundledAssetGroupSchema.cs)，支持基于发布类型、执行环境等因素，**剔除不需要的资源**（例如：发布 Server Release 版本，会剔除 Client、Dev 相关的资源）

#### 游戏内置 CLI
启动游戏后，Unity 会启动一个独立的 CLI 进程，可以在该进程上查看 Unity 日志输出（标准输出也会转发到 CLI 上），并且可以通过命令行输入与 Unity 进行实时交互！

![](.Images/2025-11-28-01-12-40.png)

CLI 功能仅在 Dev 模式下启用，可参考 [Dev 目录下的代码](Assets/Dev/Scripts/Universal/CLI/CLIManager.cs)，具体功能如下：
- 通过一个 CLIManager 单例组件管理 CLI 功能，包括：监听 Unity 日志、监听标准输出、与工作线程进行交互...
- CLIManager 启动一个**工作线程**，用于**一对多**的转发输出、接收输入

  CLIManager 通过一些简单的[协议](Assets/Dev/Scripts/Universal/CLI/CLIProtocol.cs) 与工作线程通信

  同理，工作线程也会通过这些协议与 [CLI Host](Assets/Dev/Scripts/Universal/CLI/CLIHostBase.cs) 进行通信。CLI Host 负责创建用户前端。

- 内置了一个本地的 [Client Host](Assets/Dev/Scripts/Universal/CLI/ClientCLI.cs)。游戏启动后，会自动开启一个用 [Python prompt-toolkit 编写](External/CLIProcessor/CLIProcessor.py)的 [CLI 进程](Assets/Dev/Scripts/Universal/CLI/PromptToolkitCLI.cs)

  该 Python 程序需要用 pyinstaller 打包成可执行文件，然后手动放入 `Assets/Dev/Tools` 目录下。这个要在不同平台下分别编译，目前内置了 [Windows 的版本](Assets/Dev/Tools/Windows/CLIProcessor/CLIProcessor.exe)。

  如果是有图形的客户端版本，可通过快捷键 <kbd>ctrl</kbd> + <kbd>`</kbd> 来开启 CLI 窗口。

  注：因为一些原因，**服务器无头版本也使用了和客户端相同的方案**（即，开启一个独立进程来进行交互）。一方面是这样简单，另一方面是无头版本的标准 IO 接管起来有点麻烦。

CLI 工具最初的设计图：
![](.Images/CLI%20Design.png)

该内置 CLI 提供两种交互方式：
- 命令交互

  约定：`@` 开头的是 CLI 命令、`$` 开头的是服务端命令、`$$` 开头的是客户端命令。

  可在 CLIManager 上注册命令处理器，例如这里内置了[一个热更 Lua 文件的命令](Assets/Dev/Scripts/Universal/CLI/UniversalCommandHandler.cs)，可以参考一下。

- 直接执行 Lua 代码

  通过一个叫 [LuaBridge 的类提供 CLI 上的 Lua 代码解析功能](Assets/Scripts/DevPattern/Universal/Lua/LuaBridge.cs)。

  CLI 上的 Lua 代码运行在一个沙盒化的环境中，无法使用 local，但可以安全的对全局赋值（伪全局，实际全局用 `_G` 写入）。

#### Lua 脚本
- 已为 Lua 脚本编写 [Importer](Assets/Editor/Scripts/Importer/LuaImporter.cs)，可直（也只能）接用 `.lua` 做文件结尾
- 安装了 LuaPanda 进行代码调试，[默认接口](Assets/Lua/Universal/Main.lua)为 8818
- 已安装 xLua 并编写[生成代码配置](Assets/Editor/XLuaConfig.cs)
- 实现了一个 [Lua 面向对象](Assets/Lua/Universal/Class.lua)，提供万物基类、**多重继承**（基于 MRO C3 线性化算法）、提供 `callbase` / `callable` / `isinstance` / `issubclass` 等常用面向对象工具
- 提供一个 [Lua 侧的绑定基类](Assets/Lua/Universal/BindingComponentBase.lua) 和 C# 侧的[绑定基类](Assets/Scripts/DevPattern/Universal/Lua/LuaBehaviour.cs)，通过对两者进行派生，可**快速创建 Lua 和 Unity 组件的绑定**

  这里可以参考内置的简单绑定测试：[TestBinding.lua](Assets/Lua/Universal/TestBinding.lua) 和 [TestBinding.cs](Assets/Scripts/DevPattern/Universal/Misc/TestBinding.cs)。

  Lua 绑定类会自动完成对常见 Unity MonoBehaviour 消息的绑定（如：awake、start、update...）。Lua 绑定类内也可以通过 `self.component` 直接访问对应的 C# 组件。

  C# 绑定类会自动创建 Lua 绑定类的实例，并完成绑定。然后会缓存 Lua 侧的消息函数，并在对应 MonoBehaviour 生命周期中调用（也可以自己扩展，但要注意自己释放）。

- 提供了一份[适配代码](Assets/Scripts/DevPattern/Universal/Lua/LuaBridge.cs)，供自定义程序集上的代码访问 xLua 功能

  之所以这么做，是因为 xLua 文档里说，如果要使用 Hotfix 功能则要把核心代码 Assembly-CSharp 程序集。而 Assembly-CSharp 程序集是无法被自定义程序集访问的。因此我在 Release 程序集中做了一套接口，然后通过依赖注入的方式，让自定义程序集也能访问 xLua 功能。

#### Lua 脚本热重载
这不是一个脚本资源功能（虽然思路可以沿用），而是一个**开发时快速代码迭代的辅助工具**。特点：只要是 Lua 模块导出的间接调用（即，非编译时绑定闭包），就可以通过热重载，**在游戏运行过程中实时更新**。

要点：
- LuaManager 上管理了重载状态，并触发[自定义（自己注册）的模块加载器](Assets/Scripts/Lua/LuaManager.cs)

  这里是直接通过 `File.Read` 接口来读取新 Lua 代码，Addressables 想做到这一点比较麻烦。而且即使 Addressables 使用编辑器上的 AssetDatabase 加载，也无法实时反馈资源状态。
  
  不过 LuaManager 对 Lua 加载做了缓存，所有已加载的 Lua 代码在内存都有备份。除非热重载，否则不会重复读取磁盘文件。

- 函数必须在 Lua 上是间接调用（否则无法热重载，因为重载不更新闭包）
- 可按文件重载，也可以重载所有已加载的模块
- 重载会替换模块缓存（package.loaded），但会通过弱引用保留之前的旧模块，以便在下次重载中还能更新到这些仍被使用的旧模块
- 重载的原理是递归遍历 Lua 表，然后针对性替换覆盖

  这里，除了函数是直接替换外，其他类型我通过一套约定来判断该对象是否为**状态或结构（而不是数据）**，具体约定参考 [LuaHotreload.lua](Assets/Lua/Universal/LuaHotreload.lua)。

- 通过[创建 C# Watcher 监听本地 Lua 脚本的改动](Assets/Dev/Scripts/Universal/Lua/LuaHotreloadListener.cs)，监听到变化后，自动触发对该文件的重载

  该监听器不但支持 Editor 下的重载，还支持编译后的散包重载。

#### Lua 散包脚本开发
LuaManager 上提供了对脚本**散包**开发的支持。所谓散包开发，就是指在编译输出的构建中，在特定路径附加 Lua 代码，然后**覆盖游戏内的 Lua 代码**。

当前散包开发特点：
- 通过外链接将 Assets 下的 Lua 脚本，在 StreamingAssets 下创建符号链接，该过程提供了一份 [Python 脚本](Tools/CreateLuaSymlink.py)来完成
- Dev 版本中，会注册一个叫 `DevAdditionalLoader` 的加载器，它会优先读取散包脚本
- [脚本源识别工具]()对散包有支持
- 监听重载器对散包有支持

  注：Windows 下是能正常监听符号链接，并返回变化文件的逻辑路径的，Mac 和 Linux 下的可用性暂未测试。

#### 一个简单的异步初始化系统

初始化系统设计图：
![](.Images/Initializer%20Design.png)

特点：
- 通过 IInitOperation 接口表示一个初始化操作：
  - 提供一个唯一的初始化操作 id，无法重复启动，成功后无法注销也无法重启
  - 操作状态，支持 NotStarted、Pending、InProgress、Completed、Failed、Cancelled 状态
  - 可以提供一份**不变**的依赖列表，通过 id 组表示对其他初始化操作的依赖
  - 失败后可以重启
  - 成功和失败的回调
- 通过 InitOperationManager 管理初始化行为，其中包含一些**复杂的安全并发处理逻辑**
- InitOperationManager 会在启动时自动加载一些 prefab，这些 prefab 是在代码上 Hardcode 的，其上面的代码负责注册后续的 InitOperation 操作
- 内置实现了一些常见的初始化操作基类，如：加载单个 Prefab、创建对象并挂载脚本等

游戏 CLI 正是通过该系统进行初始化的，由于该系统启动有一定延迟，因此一些早期的 Log 可能无法在 CLI 上看到。

### Todo
我认为当前的状态离实际商业项目可用的水平还有相当的举例，希望能持续完善。如果有什么好的经验或建议，欢迎直接在 Issues 中告诉我。**如果我觉得非常非常有用，可以请你吃个疯狂星期四当教学费啦** o(〃＾▽＾〃)o

未来待完善功能：
- [ ] **制作一个实际的 C/S + 3C Demo 来验证该手脚架的可用性**
- [ ] Editor 下资源热更开发
- [ ] CLI 通信 GC 优化
- [ ] 散包模式下资源热更开发
  - [ ] 配置 remote build，启动 Unity Addressables Host
  - [ ] 资源变动时，可手动执行一次 Addressables 构建（本地 Host 会自动上传）；然后客户端重启、或执行命令来强制刷新然后重进场景
  - [ ] 通过外链接入 Addressables 的编译输出
  - [ ] 通过 Addressables 的 Update Catelog 机制动态更新资源
  - [ ] 提供 CLI 命令更新（或是强制卸载）
- [ ] 优化构建系统
  - [ ] 通过修改生成模板，使 xLua 生成代码能跨平台适应（宏分支），避免切换平台构建时要重新生成代码
  - [ ] **修复 Addressables 构建时偶现的空对象报错**
- [ ] 支持 PuerTs（Lua 还是太落后了 desuwa）
- [ ] 更多平台的编译和发布支持
- [ ] 发布版本的热更支持
- [ ] 更多高级内容（CICD 支持、DCC 集成、自动化测试...）





















<br>
<br>
<br>
<br>
<br>
<br>

---End---
