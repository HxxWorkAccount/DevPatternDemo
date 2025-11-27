# -*- coding: utf-8 -*-
# 编译脚本：
# - cd External/CLIProcessor
# - pyinstaller -F CLIProcessor.py
# 用法：
# - 编译后，复制到路径 Assets/Dev/Tools/Binary/<system>/CLIProcessor.exe
# - 其中 <system> 可以是 Windows, macOS
# - 开发版本发包时，编译脚本会在编译结束后，从该目录下将复制到 StreamingAssets 目录

import time
import os
import sys
import json
import asyncio
import argparse
from datetime import datetime
from prompt_toolkit import PromptSession, print_formatted_text
from prompt_toolkit.patch_stdout import patch_stdout
from prompt_toolkit.formatted_text import FormattedText
from prompt_toolkit.styles import Style
from prompt_toolkit.key_binding import KeyBindings
from prompt_toolkit.application import get_app
from prompt_toolkit.lexers import Lexer
from prompt_toolkit.document import Document
from prompt_toolkit.formatted_text.base import StyleAndTextTuples
from prompt_toolkit.history import InMemoryHistory

# 功能：
# - [x] 清屏命令
# - [x] 输入历史
# - [x] 锁定滚动开关
# - [x] 允许选中并复制文本
# - [x] 无头版本的 UI 交互要处理一下
# - [x] 断连后关闭

# Protocol Definitions
class MessageType:
    Log = 0
    Handshake = 1
    Disconnect = 2
    Input = 3
    WorkerError = 4

class LogType:
    Info = 0
    Warning = 1
    Error = 2
    # CLIProcessor Custom
    System = 11
    Exception = 12

HOST = '127.0.0.1'
PORT = 57679

# Custom Lexer for Log Coloring
class LogLexer(Lexer):

    def lex_document(self, document: Document):
        def get_line(lineno) -> StyleAndTextTuples:
            try:
                line = document.lines[lineno]
                if line.startswith("[Error]") :
                    return [("class:log.error", line)]
                elif line.startswith("[Warning]"):
                    return [("class:log.warning", line)]
                elif line.startswith("[System]"):
                    return [("class:log.system", line)]
                else:
                    return [("class:log.info", line)]
            except IndexError:
                return []
        return get_line

class CLIProcessor:

    #====-------------- Life --------------====#

    def __init__(self):
        self.m_writer = None
        self.m_server = None
        self.m_scrollLock = False
        self._InitUI()

    def _InitUI(self):
        self.m_inputHistory = InMemoryHistory()

        # Key bindings
        keyBindings = KeyBindings()

        @keyBindings.add('c-c')
        def _(event):
            self.Quit()

        # Style
        self.m_style = Style.from_dict({
            "log.error": "#ff4444",
            "log.exception": "#f24747",
            "log.warning": "#ffff00",
            "log.info": "#ffffff",
            "log.system": "#00ff00",
        })

        self.m_session = PromptSession(
            history=self.m_inputHistory,
            style=self.m_style,
            key_bindings=keyBindings
        )

    #====-------------- Server --------------====#

    def Quit(self):
        """统一退出接口"""
        if self.m_session and self.m_session.app.is_running:
            self.m_session.app.exit(exception=EOFError)
        else:
            sys.exit(0)

    async def Cleanup(self):
        """统一清理接口"""
        if self.m_writer:
            try:
                # 发送断开连接包
                packet = {"type": MessageType.Disconnect, "data": "CLI closed"}
                data = json.dumps(packet) + "\n"
                self.m_writer.write(data.encode('utf-8'))
                await asyncio.wait_for(self.m_writer.drain(), timeout=0.2)
            except Exception:
                pass

            try:
                self.m_writer.close()
                await asyncio.wait_for(self.m_writer.wait_closed(), timeout=0.2)
            except Exception:
                pass
            self.m_writer = None

        if self.m_server:
            self.m_server.close()
            self.m_server = None

    # 启动 Server 和 UI
    async def Run(self):
        # 同时并发启动 Server 和 UI
        serverTask = asyncio.create_task(self.RunServer()) # 不会阻塞，事件循环自动调度 Task
        try:
            with patch_stdout():
                while True:
                    try:
                        text = await self.m_session.prompt_async('> ')
                        self.HandleInput(text)
                    except KeyboardInterrupt:
                        break
                    except EOFError:
                        break
        finally:
            await self.Cleanup()
            serverTask.cancel()
            try:
                await serverTask
            except asyncio.CancelledError: # 取消成功
                pass

    # 启动 Server
    async def RunServer(self):
        try:
            self.m_server = await asyncio.start_server(self.HandleClient, HOST, PORT) # 异步创建 TCP Server
            addr = self.m_server.sockets[0].getsockname()
            self.Log(f"Serving on '{addr}'", logType=LogType.System)
            async with self.m_server: # 创建安全的异步上下文，异常时会自动触发 '__aexit__'（其中会调用 m_server.close）
                await self.m_server.serve_forever() # 【阻塞】保持事件循环，不断监听客户端连接
        except OSError as e:
            sys.stderr.write(f"Port '{PORT}' is occupied or cannot be bound: {e}\n")
            get_app().exit()

    # 每当有新客户端连接时，就会触发该回调
    async def HandleClient(self, reader, writer):
        # 如果已经有连接了，直接拒绝新连接
        if self.m_writer is not None:
            self.Log(f"Rejecting new connection from {writer.get_extra_info('peername')}", logType=LogType.Warning)
            writer.close()
            await writer.wait_closed()
            return

        # 停止监听新连接（不再接受新的客户端）
        if self.m_server:
            # self.m_server.close() # 建立 TCP 连接后可以直接关闭服务器（但这样就允许重复打开 CLI，所以就不管咯 ψ(｀∇´)ψ）
            pass

        self.m_writer = writer # 缓存了最新的 writer
        addr = writer.get_extra_info('peername') # 获取客户端 IP 和端口
        self.Log(f"Connected by '{addr}'", logType=LogType.System)

        try:
            while True: # 循环接收和处理消息
                # Read line by line (handles \n framing)
                line = await reader.readline() # 【阻塞】py 非常方便提供了 readline，直接解决了粘包
                if not line:
                    break

                try:
                    decodedLine = line.decode('utf-8').strip() # 协议以 utf-8 发送，一行就是一个 json
                    if not decodedLine:
                        continue

                    # 反序列化，得到 CLIProtocol.Packet
                    packet = json.loads(decodedLine)
                    packetType = packet.get("type")
                    packetData = packet.get("data", "")

                    # 处理不同类型的消息
                    if packetType == MessageType.Log: # 格式化 Log 然后输出
                        try:
                            logMsg = json.loads(packetData)
                            self.Log(logMsg["content"], logMsg["logType"], logMsg["timestamp"], logMsg["stackTrace"])
                        except Exception as e:
                            self.Log(f"unpack unity log faile: {e}. Data: {packetData}")
                    elif packetType == MessageType.Handshake: # Handshake 暂时就打印一下
                        self.Log(f"Handshake: {packetData}", logType=LogType.System)
                    elif packetType == MessageType.Disconnect: # 遇到 Disconnect 直接 break 离开循环
                        self.Log(f"Disconnect received: {packetData}", logType=LogType.System)
                        break
                    else:
                        self.Log(f"Invalid packet type: {packetType}", logType=LogType.System)
                except json.JSONDecodeError:
                    self.Log(f"Invalid JSON received", logType=LogType.Error)
                except Exception as e:
                    self.Log(f"Processing message: {e}", logType=LogType.Error)
        except Exception as e:
            self.Log(f"Connection error: {e}", logType=LogType.Error)
        finally:
            self.Log(f"Disconnected {addr}", logType=LogType.System)
            self.m_writer = None
            writer.close()
            try:
                # 加上超时防止阻塞退出
                await asyncio.wait_for(writer.wait_closed(), timeout=0.2) 
            except Exception:
                pass
            
            # 通知主循环退出
            self.Quit()

    #====-------------- Translation --------------====#

    # 向 UI 输出 Log
    def Log(self, message, logType=LogType.Info, timestamp=0, stackTrace=""):
        style_class = "class:log.info"
        if logType == LogType.Warning:
            style_class = "class:log.warning"
        elif logType == LogType.Error:
            style_class = "class:log.error"
        elif logType == LogType.System:
            style_class = "class:log.system"
        elif logType == LogType.Exception:
            style_class = "class:log.exception"

        timeStr = ""
        if timestamp > 0:
            timeStr = self.FromTimestampMs(timestamp)

        # Construct formatted text
        formatted_text = FormattedText([
            (style_class, f"{timeStr}{message}")
        ])

        print_formatted_text(formatted_text, style=self.m_style)
        if stackTrace:
             print_formatted_text(FormattedText([(style_class, stackTrace)]), style=self.m_style)

    def HandleInput(self, text:str):
        if not text:
            return
        strippedText = text.strip()
        if strippedText.startswith("@"):
            self.HandleCLICommand(strippedText)
        else:
            # 发给 Unity（writer 是最新连接的客户端）
            self.SendPacket({"type": MessageType.Input,"data": text})

    def HandleCLICommand(self, command):
        if command == "@clear":
            os.system('cls' if os.name == 'nt' else 'clear')
        else:
            self.Log(f"Unknown command: '{command}'", logType=LogType.System)

    def SendPacket(self, packet) -> bool:
        # 发给 Unity（writer 是最新连接的客户端）
        if self.m_writer:
            try:
                data = json.dumps(packet) + "\n" # 加 '\n' 处理粘包
                self.m_writer.write(data.encode('utf-8')) # 写入缓冲（未发送）
                asyncio.create_task(self.m_writer.drain()) # 在事件循环将 writer 输出（似乎没法处理发送异常欸 (＃°Д°)）
                return True
            except Exception as e:
                self.Log(f"Failed to send packet: {e}", LogType.Error)
        else:
            self.Log("No connection to Unity.", LogType.Error)
        return False

    @staticmethod
    def FromTimestampMs(timestampMs) -> str:
        try:
            timestamp = timestampMs // 1000
            dt = datetime.fromtimestamp(timestamp)
            return f"[{dt.strftime('%H:%M:%S')}.{(timestampMs % 1000):03d}] "
        except:
            return f"[Invalid-{timestampMs}] "

if __name__ == "__main__":
    # Parse command line arguments
    parser = argparse.ArgumentParser(description='Unity CLI Processor')
    parser.add_argument('--ip', type=str, default='127.0.0.1', help='Bind IP address')
    parser.add_argument('--port', type=int, default=57679, help='Bind Port')

    # 忽略未知的参数
    args, unknown = parser.parse_known_args()

    HOST = args.ip
    PORT = args.port

    print(f"Starting CLI Processor on {HOST}:{PORT}...")

    try:
        processor = CLIProcessor()
        asyncio.run(processor.Run())
    except KeyboardInterrupt:
        pass
    except Exception as e:
        sys.stderr.write(f"Fatal Error: {e}\n")
        import traceback
        traceback.print_exc()
        time.sleep(5) # 发生致命错误时等待再退出
