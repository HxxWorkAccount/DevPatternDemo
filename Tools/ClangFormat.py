# -*- coding: utf-8 -*-
import os
import sys
import subprocess

if __name__ == "__main__":
    filePath = sys.argv[1]
    command = [
        "clang-format",
        filePath
    ]
    process = subprocess.Popen(command, stdin=sys.stdin, stdout=sys.stdout, stderr=sys.stderr, text=True, shell=False)
    process.wait()
    if process.returncode != 0:
        print(f"FAILED, return code: {process.returncode}")
