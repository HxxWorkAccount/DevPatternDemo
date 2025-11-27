# -*- coding: utf-8 -*-
import os

PROJECT_NAME = "DummyProjectName"

def createAssetsSubDir():
    SUB_DIR_LIST = (
        "Content/Client/General/Art",
        "Content/Client/General/Audio",
        "Content/Client/General/Prefabs",
        "Content/Client/General/Scenes",
        "Content/Server",
        "Content/Universal",
        "Data",
        "Dev/Content",
        "Dev/Lua",
        "Dev/Scripts",
        "Dev/Tools",
        "Documentation",
        "Editor",
        "Editor/Resources",
        "Editor/Scripts",
        "Gizmos",
        "Lua/Server",
        "Lua/Client",
        "Lua/Universal",
        "Plugins",
        "Resources",
        "Samples",
        "Scenes",
        f"Scripts/{PROJECT_NAME}",
        "Scripts/Lua",
        "Settings",
        "Shaders",
        "Standard Assets",
        "StreamingAssets",
        "Tests/Content",
        "Tests/Scripts/Client",
        "Tests/Scripts/Server",
        "Tests/Scripts/Universal",
        "Tests/Lua/Client",
        "Tests/Lua/Server",
        "Tests/Lua/Universal",
    )
    assetsPath = os.path.join(os.getcwd(), "Assets")
    for subDir in SUB_DIR_LIST:
        dirPath = os.path.join(assetsPath, subDir)
        if not os.path.exists(dirPath):
            os.makedirs(dirPath)
            print(f"Created directory: {dirPath}")
        else:
            print(f"Directory already exists: {dirPath}")

if __name__ == "__main__":
    createAssetsSubDir()
