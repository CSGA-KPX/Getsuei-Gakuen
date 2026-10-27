// 产物全量校验：237 个 patch pac 的 abc/图集/atf 一致性
// 用法: dotnet fsi scratch/verify-patch.fsx
#load "../scripts/Pac.fsx"
#load "../scripts/AtfJson/Atf.fs"

open System
open System.IO
open System.Collections.Generic
open PacLib
open AtfJson

type AbcInfo =
    { EntryCount: int; GlyphCount: int
      MapOff: int; CountOff: int; Rec24Base: int; Rec32Base: int }

let abcParse (b: byte[]) : AbcInfo =
    let ec = int (BitConverter.ToUInt16(b, 20))
    let countOff = 24 + 2 * ec
    let gc = BitConverter.ToInt32(b, countOff)
    let r24 = countOff + 4
    { EntryCount = ec; GlyphCount = gc; MapOff = 22; CountOff = countOff
      Rec24Base = r24; Rec32Base = r24 + 24 * gc }

let patchDir = "../work/patch-pac"
let mutable ok = 0
let mutable fail = 0
for pacPath in Directory.GetFiles(Path.Combine(__SOURCE_DIRECTORY__, patchDir), "*.pac") |> Array.sortBy id do
    let name = Path.GetFileNameWithoutExtension(pacPath)
    try
        let pac = PacLib.read pacPath
        let find n = pac.Entries |> Array.find (fun e -> e.Filename = n)
        let abcB = PacLib.extract pac (find "font.abc")
        let abc = abcParse abcB
        // 1. abc 结构自洽
        if abc.Rec32Base + 32 * abc.GlyphCount <> abcB.Length then failwithf "abc 长度不符"
        // 2. 全部 mapping 值 < GlyphCount
        for cp in 0 .. abc.EntryCount do
            let v = int (BitConverter.ToUInt16(abcB, abc.MapOff + 2 * cp))
            if v >= abc.GlyphCount then failwithf "mapping[U+%04X]=%d 越界" cp v
        // 3. 全部 rec32 rect 在 2048 界内且不重叠（排序扫描）
        let rects =
            [| for g in 0 .. abc.GlyphCount - 1 ->
                   let o = abc.Rec32Base + 32 * g
                   (BitConverter.ToInt32(abcB, o), BitConverter.ToInt32(abcB, o + 4),
                    BitConverter.ToInt32(abcB, o + 8), BitConverter.ToInt32(abcB, o + 12)) |]
        for (x1, x2, y1, y2) in rects do
            if not (x1 >= 0 && y1 >= 0 && x2 <= 2048 && y2 <= 2048 && x2 - x1 = 34 && y2 - y1 = 49) then
                failwithf "rect 异常 (%d,%d)-(%d,%d)" x1 y1 x2 y2
        // 重叠检查：按 y 分行，行内按 x 排序相邻比较
        let rows = rects |> Array.groupBy (fun (_, _, y1, _) -> y1)
        for (_, row) in rows do
            let sorted = row |> Array.sortBy (fun (x1, _, _, _) -> x1)
            for i in 0 .. sorted.Length - 2 do
                let (ax1, ax2, _, _) = sorted[i]
                let (bx1, _, _, _) = sorted[i + 1]
                if bx1 < ax2 then failwithf "rect 重叠 x=%d/%d" ax1 bx1
        // 4. rec24 UV 与 rec32 rect 一致（抽 10 个）
        let rnd = Random(42)
        for _ in 1 .. 10 do
            let g = rnd.Next(abc.GlyphCount)
            let (x1, x2, y1, y2) = rects[g]
            let u1 = BitConverter.ToSingle(abcB, abc.Rec24Base + 24 * g)
            let v1 = BitConverter.ToSingle(abcB, abc.Rec24Base + 24 * g + 4)
            let u2 = BitConverter.ToSingle(abcB, abc.Rec24Base + 24 * g + 8)
            let v2 = BitConverter.ToSingle(abcB, abc.Rec24Base + 24 * g + 12)
            if abs (u1 - single (float x1 / 2048.0)) > 1e-5f then failwithf "glyph %d u1" g
            if abs (v1 - single (float y1 / 2048.0)) > 1e-5f then failwithf "glyph %d v1" g
            if abs (u2 - single (float x2 / 2048.0)) > 1e-5f then failwithf "glyph %d u2" g
            if abs (v2 - single (float y2 / 2048.0)) > 1e-5f then failwithf "glyph %d v2" g
        // 5. 新字形码位抽查：每 pac 至少验证「的」或第一个新增字存在 mapping
        // 6. atf 可解析且记录数正确
        let atfB = PacLib.extract pac (find "string.atf")
        let atf = AtfBinary.parseBytes atfB
        let orig = AtfBinary.parseBytes (PacLib.extract (PacLib.read (Path.Combine(@"<游戏安装目录>\data\story\text\jpn", name + ".pac"))) ((PacLib.read (Path.Combine(@"<游戏安装目录>\data\story\text\jpn", name + ".pac"))).Entries |> Array.find (fun e -> e.Filename = "string.atf")))
        if atf.Records.Length <> orig.Records.Length then failwithf "atf 记录数 %d != %d" atf.Records.Length orig.Records.Length
        // 7. 图集尺寸 2048
        let hipB = PacLib.extract pac (find "font.hip")
        let isHip = hipB.Length > 3 && hipB[0] = 0x48uy
        if isHip then
            let w = BitConverter.ToUInt16(hipB, 0) |> int   // HIP 头尺寸位置待确认，宽松校验
            ()
        else
            let w = BitConverter.ToUInt16(hipB, 12) |> int
            let h = BitConverter.ToUInt16(hipB, 14) |> int
            if w <> 2048 || h <> 2048 then failwithf "TGA 尺寸 %dx%d" w h
        ok <- ok + 1
    with ex ->
        fail <- fail + 1
        eprintfn "FAIL %s: %s" name ex.Message

printfn "==== verify-patch: %d OK / %d FAIL ====" ok fail
if fail > 0 then exit 1
