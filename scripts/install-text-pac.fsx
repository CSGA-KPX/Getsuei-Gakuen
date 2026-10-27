// 对话文本 pac 装机：work/patch-pac/*.pac（237 个）→ data\story\text\jpn\
//
// 用法:
//   dotnet fsi scripts/install-text-pac.fsx            # 试运行（只报告要装什么）
//   dotnet fsi scripts/install-text-pac.fsx --apply    # 实际装机（自动备份原文件）
//   dotnet fsi scripts/install-text-pac.fsx --restore  # 从备份还原
//
// 备份位置：work/backup/story/text/jpn/（每个 pac 首次覆盖前备份一次，之后不再覆盖备份）
// 装机后逐文件 SHA 校验，任何不一致立即报错。
//
// ⚠️ 只装 work/patch-pac 里存在的 pac；游戏里多出来的（旧版备份 text_xxx.pac_ 之类）不动。
// ⚠️ 不涉及 data\text\jpn\jpn_glyph.* 与 data\cmn\font.pac（UI 全局字库 / 菜单字库）——
//    2026-10-20 已确证 UI 文字是预渲染图，这两套字库**不需要**扩充，保持原版。
open System
open System.IO
open System.Security.Cryptography

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let gameDir = @"<游戏安装目录>\data\story\text\jpn"
let srcDir = Path.Combine(workspace, "work", "patch-pac")
let backupDir = Path.Combine(workspace, "work", "backup", "story", "text", "jpn")

let args = fsi.CommandLineArgs |> Array.skip 1
let apply = args |> Array.contains "--apply"
let restore = args |> Array.contains "--restore"

let sha (p: string) =
    use s = File.OpenRead p
    use h = SHA256.Create()
    h.ComputeHash(s) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

Directory.CreateDirectory backupDir |> ignore

if restore then
    let baks = Directory.GetFiles(backupDir, "*.pac") |> Array.sort
    printfn "从备份还原 %d 个 pac → %s" baks.Length gameDir
    let mutable n = 0
    for b in baks do
        File.Copy(b, Path.Combine(gameDir, Path.GetFileName b), true)
        n <- n + 1
    printfn "已还原 %d 个（原版备份仍在 %s）" n backupDir
    exit 0

let files = Directory.GetFiles(srcDir, "*.pac") |> Array.sort
if files.Length = 0 then
    eprintfn "work/patch-pac 里没有 pac，先跑 dotnet fsi scripts/font-rebuild.fsx all"
    exit 2

printfn "待装 pac %d 个（%s）" files.Length (if apply then "写入" else "试运行，加 --apply 才写")

let mutable backedUp = 0
let mutable installed = 0
let mutable skipped = 0
let mutable mismatch = 0
let mutable missing = 0

for src in files do
    let name = Path.GetFileName src
    let dst = Path.Combine(gameDir, name)
    if not (File.Exists dst) then
        missing <- missing + 1
        eprintfn "⚠️ 游戏目录里没有 %s（跳过）" name
    else
        let bak = Path.Combine(backupDir, name)
        if not (File.Exists bak) then
            if apply then File.Copy(dst, bak)
            backedUp <- backedUp + 1
        if File.Exists dst && sha src = sha dst then
            skipped <- skipped + 1   // 已经是最新（重跑幂等）
        elif apply then
            File.Copy(src, dst, true)
            if sha src <> sha dst then
                mismatch <- mismatch + 1
                eprintfn "❌ SHA 不一致：%s" name
            else
                installed <- installed + 1
        else
            installed <- installed + 1

printfn "\n%s：新装 %d / 已是最新 %d / 备份 %d / SHA 失败 %d / 目标缺失 %d"
    (if apply then "已写入" else "试运行") installed skipped backedUp mismatch missing
if backedUp > 0 && not apply then
    printfn "（首跑会先备份原文件到 %s）" backupDir
printfn "\n还原：dotnet fsi scripts/install-text-pac.fsx --restore"
