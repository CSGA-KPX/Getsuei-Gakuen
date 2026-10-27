#!/usr/bin/env python3
"""快速反汇编 Getsuei.exe 的指定 RVA（不经 IDA，秒级出结果）

为什么用 Python：capstone 一条命令就能反汇编，F# 侧没有等价易用的绑定，
而 IDA MCP 启动要十几秒（有时还起不来）。用于「拿到崩溃地址后第一时间看代码」。

依赖: pip install capstone pefile

用法:
    python scripts/rasm.py 1533d            # 从 0x1533D-0x60 开始反汇编 60 条
    python scripts/rasm.py 1533d 200        # 反汇编 200 条
    python scripts/rasm.py 1533d 60 0       # 从 0x1533D 直接开始（不回退）
    python scripts/rasm.py 106e0 40 0x20 --hex   # 额外打印该处 64 字节原始 hex

注意 RVA 换算：WinDbg 的 `Getsuei+0x107793` 是**模块内偏移**，
VA = 0x400000 + 0x107793 = 0x507793，RVA 同样是 0x107793（ImageBase = 0x400000）。
"""
import sys
import pefile
import capstone

EXE = r'<游戏安装目录>\Getsuei.exe'

pe = pefile.PE(EXE, fast_load=True)
image_base = pe.OPTIONAL_HEADER.ImageBase
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
md.detail = True


def dis(rva, n, label='', show_hex=False):
    off = pe.get_offset_from_rva(rva)
    code = pe.__data__[off:off + n * 16]
    print(f'\n--- {label} RVA 0x{rva:X} (VA 0x{image_base + rva:X}) ---')
    if show_hex:
        print('  bytes:', code[:64].hex())
    cnt = 0
    for ins in md.disasm(code, image_base + rva):
        print(f'0x{ins.address:X}: {ins.bytes.hex():<20} {ins.mnemonic:<8} {ins.op_str}')
        cnt += 1
        if cnt >= n:
            break


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    show_hex = '--hex' in sys.argv
    if not args:
        print(__doc__)
        return 2
    rva = int(args[0], 16)
    count = int(args[1]) if len(args) > 1 else 60
    back = int(args[2], 16) if len(args) > 2 else 0x60
    dis(max(rva - back, 0), count, f'target 0x{rva:X}', show_hex)
    return 0


if __name__ == '__main__':
    sys.exit(main())
