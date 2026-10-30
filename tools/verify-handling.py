#!/usr/bin/env python3
"""Compatibility entry point for the current vehicle and handling verification."""
from pathlib import Path
import runpy

if __name__ == '__main__':
    runpy.run_path(str(Path(__file__).with_name('verify-tumbler-update.py')), run_name='__main__')
