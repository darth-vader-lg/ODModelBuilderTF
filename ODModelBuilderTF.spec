# -*- mode: python ; coding: utf-8 -*-
import sys ; sys.setrecursionlimit(sys.getrecursionlimit() * 5)

block_cipher = None


a = Analysis(['main.py'],
             pathex=['.'],
             binaries=[('env\\Scripts\\tensorboard.exe', '.')],
             datas=[],
             hiddenimports=['pandas._libs.tslibs.base', 'tensorflow.python.keras.engine.base_layer_v1'],
             hookspath=[],
             runtime_hooks=[],
             excludes=[],
             win_no_prefer_redirects=False,
             win_private_assemblies=False,
             cipher=block_cipher,
             noarchive=False)
pyz = PYZ(a.pure, a.zipped_data,
             cipher=block_cipher)
exe = EXE(pyz,
          a.scripts,
          a.binaries,
          a.zipfiles,
          a.datas,
          [],
          name='ODModelBuilderTF',
          debug=False,
          bootloader_ignore_signals=False,
          strip=False,
          upx=True,
          upx_exclude=[],
          runtime_tmpdir='%TEMP%\\tf-od-model-builder',
          console=True )
