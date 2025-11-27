# -*- coding: utf-8 -*-

import os
import sys
import platform
import subprocess

# 需求：
# - [x] 支持 Windows、MacOS、Linux 三个平台
# - [x] 在 Builds/<target>/<target>_Data/StreamingAssets/AdditionalLua/ 目录下创建 Lua 脚本外链
# - [x] <target> 直接在代码中指定，如果 Builds/<target>/<target>_Data 目录不存在，则抛出错误
# - [x] 如果 Builds/<target>/<target>_Data/StreamingAssets/AdditionalLua/ 目录不存在，则创建该目录
# - [x] 外链源包括 Assets/Lua, Assets/Dev/Lua, Assets/Tests/Lua 三个目录

# 配置目标名称 (请根据实际情况修改)
TARGET_NAME = "Windows_Server_Dev"

# 定义源目录和目标链接名称
SOURCES_TO_LINKS = [
    (os.path.join("Assets", "Lua"), "Lua"),
    (os.path.join("Assets", "Dev", "Lua"), "Dev/Lua"),
    (os.path.join("Assets", "Tests", "Lua"), "Tests/Lua")
]

def checkSymblink(linkDir):
    if os.path.exists(linkDir) or os.path.islink(linkDir):
        print(f"Error: Link or folder already exists: '{linkDir}'")
        sys.exit(1)  

def createSymlink(sourceDir, linkPath):
    system = platform.system()

    if os.path.exists(linkPath) or os.path.islink(linkPath):
        print(f"Error: Link or folder already exists: '{linkPath}'")
        sys.exit(1)  

    linkDir = os.path.dirname(linkPath)
    sourceDir = os.path.abspath(sourceDir)
    if not os.path.exists(linkDir):
        os.makedirs(linkDir)

    try:
        if system == "Windows":
            # Windows 下尝试创建软链接
            try:
                os.symlink(sourceDir, linkPath)
            except OSError:
                # 如果没有权限，尝试使用 mklink /J 创建目录联接 (Junction)
                if os.path.isdir(sourceDir):
                    # 使用 mklink 需要 shell=True
                    cmd = f'mklink /J "{linkPath}" "{sourceDir}"'
                    subprocess.check_call(cmd, shell=True)
                else:
                    raise
        else:
            # MacOS / Linux
            os.symlink(sourceDir, linkPath)
        print(f"Success: Created link '{linkPath}' -> '{sourceDir}'")
    except Exception as e:
        print(f"Error creating link '{linkPath}': {e}")
        if system == "Windows":
            print("Tip: On Windows, try running as Administrator or enable Developer Mode.")

def main():
    buildsDir = "Builds"
    targetDir = os.path.join(buildsDir, TARGET_NAME)
    targetDataDir = os.path.join(targetDir, f"{TARGET_NAME}_Data")

    # 检查 targetData 目录是否存在
    if not os.path.exists(targetDataDir):
        print(f"Error: Target Data directory not found: {targetDataDir}")
        sys.exit(1)

    streamingAssetsDir = os.path.join(targetDataDir, "StreamingAssets")
    additionalLuaDir = os.path.join(streamingAssetsDir, "AdditionalLua")

    # 如果 AdditionalLua 目录不存在，则创建
    if not os.path.exists(additionalLuaDir):
        try:
            os.makedirs(additionalLuaDir)
            print(f"Created directory: {additionalLuaDir}")
        except OSError as e:
            print(f"Error creating directory '{additionalLuaDir}': {e}")
            sys.exit(1)

    for sourceDir, linkBasename in SOURCES_TO_LINKS:
        linkPath = os.path.join(additionalLuaDir, linkBasename)
        if not os.path.exists(sourceDir):
            continue
        checkSymblink(linkPath)

    for sourceDir, linkBasename in SOURCES_TO_LINKS:
        linkPath = os.path.join(additionalLuaDir, linkBasename)
        if not os.path.exists(sourceDir):
            print(f"Warning: Source dir not found, skipping: {sourceDir}")
            continue
        createSymlink(sourceDir, linkPath)

if __name__ == "__main__":
    main()
