// jpn_glyph / Font00 的图集尺寸、格子布局与剩余容量评估
#load "./Pac.fsx"
#load "./hip.fsx"

open System
open System.IO
open System.Text
open PacLib

let game = @"<游戏安装目录>"

/// 图集按 rec32 像素矩形推算格子尺寸与利用率
let analyzeAtlas (label: string) (abcBytes: byte[]) (hipBytes: byte[]) (lineHeight: float32) =
    let hip = HipLib.decode hipBytes
    let ec = int (BitConverter.ToUInt16(abcBytes, 20))
    let countOff = 24 + 2 * ec
    let gc = BitConverter.ToInt32(abcBytes, countOff)
    // rec32 是否存在（ver5 无 rec32，走 float UV）
    let rec32Start = countOff + 4 + 24 * gc
    let hasRec32 = rec32Start + 32 * gc <= abcBytes.Length
    let cw, ch =
        if hasRec32 then
            let o = rec32Start
            // rec32 布局 = (x1, x2, y1, y2)
            let w = BitConverter.ToInt32(abcBytes, o + 4) - BitConverter.ToInt32(abcBytes, o)
            let h = BitConverter.ToInt32(abcBytes, o + 12) - BitConverter.ToInt32(abcBytes, o + 8)
            max 1 w, max 1 h
        else
            max 1 (int (Math.Round(float lineHeight))), max 1 (int (Math.Round(float lineHeight)))
    let cols = hip.ImageWidth / cw
    let rows = hip.ImageHeight / ch
    printfn "\n### %s" label
    printfn "- 图集：%d×%d（%s，%d bpp），按 rec32 格子 %d×%d 推算 → %d 列 × %d 行 = %d 格"
        hip.ImageWidth hip.ImageHeight (sprintf "%A" hip.Format) (hip.Format.Bpp) cw ch cols rows (cols * rows)
    printfn "- 已用字形：%d（占有率 %.1f%%），剩余 %d 格"
        gc (100.0 * float gc / float (cols * rows)) (cols * rows - gc)
    printfn "- LineHeight=%.1f，HIP 文件 %d 字节（%.2f MB），abc %d 字节"
        lineHeight hipBytes.Length (float hipBytes.Length / 1048576.0) abcBytes.Length

let abcPath = Path.Combine(game, @"data\text\jpn\jpn_glyph.abc")
let hipPath = Path.Combine(game, @"data\text\jpn\jpn_glyph.hip")
analyzeAtlas "jpn_glyph（UI 全局字库）" (File.ReadAllBytes abcPath) (File.ReadAllBytes hipPath) 39.0f

// Font00：从 cmn/font.pac 取
let fontPac = PacLib.read (Path.Combine(game, @"data\cmn\font.pac"))
let abcEntry = fontPac.Entries |> Array.find (fun e -> e.Filename.Contains "abc")
let tmp = Path.Combine(Path.GetTempPath(), "font00.pac")
File.WriteAllBytes(tmp, PacLib.extract fontPac abcEntry)
let abcInner = PacLib.read tmp
let abcBytes =
    let ie = abcInner.Entries |> Array.find (fun e -> e.Filename.EndsWith(".abc", StringComparison.OrdinalIgnoreCase))
    printfn "abc.pac 内文件: %s" (abcInner.Entries |> Array.map (fun e -> sprintf "%s(%d)" e.Filename e.Length) |> String.concat ", ")
    PacLib.extract abcInner ie
let imgEntry = fontPac.Entries |> Array.find (fun e -> e.Filename.Contains "img")
let tmp2 = Path.Combine(Path.GetTempPath(), "font00img.pac")
File.WriteAllBytes(tmp2, PacLib.extract fontPac imgEntry)
let imgInner = PacLib.read tmp2
printfn "fullimg.pac 内文件: %s" (imgInner.Entries |> Array.map (fun e -> sprintf "%s(%d)" e.Filename e.Length) |> String.concat ", ")
match imgInner.Entries |> Array.tryFind (fun e -> e.Filename.EndsWith(".hip", StringComparison.OrdinalIgnoreCase)) with
| Some he -> analyzeAtlas (sprintf "Font00（菜单字库）") abcBytes (PacLib.extract imgInner he) 15.0f
| None -> printfn "⚠️ fullimg.pac 内无 hip"
File.Delete tmp
File.Delete tmp2

// 译文实际用字统计（用于评估需要追加多少字形）
printfn "\n### 译文用字需求"
let cnDir = "work/atf-cn"
if Directory.Exists cnDir then
    let chars = Collections.Generic.HashSet<int>()
    for f in Directory.GetFiles(cnDir, "*.json") do
        for c in File.ReadAllText f do
            if int c > 0x20 then chars.Add(int c) |> ignore
    let hanzi = chars |> Seq.filter (fun c -> (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF)) |> Seq.length
    let kana = chars |> Seq.filter (fun c -> (c >= 0x3040 && c <= 0x30FF)) |> Seq.length
    printfn "- 译文去重字符 %d 个：汉字 %d / 假名 %d / 其他（标点数字拉丁）%d"
        chars.Count hanzi kana (chars.Count - hanzi - kana)
    printfn "- jpn_glyph 现有 2860 字形，追加 %d 汉字后约 %d 字形" hanzi (2860 + hanzi)
    printfn "- Font00 现有 250 字形，图集仅 256 格（16×16）→ **装不下中文，需换更大图集或改走实时字体路径**"
