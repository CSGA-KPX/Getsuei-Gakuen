// 把测试补丁装进游戏（自动备份原文件到 work/backup/AE/title/，可一键还原）
open System
open System.IO

let game = @"<游戏安装目录>"
let src = "work/patch-test/UI_02_title.pac"
let dst = Path.Combine(game, @"data\AE\title\UI_02_title.pac")

// 备份（只备份一次，不覆盖已有备份）
let backupDir = "work/backup/AE/title"
Directory.CreateDirectory backupDir |> ignore
let backup = Path.Combine(backupDir, "UI_02_title.pac")
if not (File.Exists backup) then
    File.Copy(dst, backup)
    printfn "已备份原文件 → %s（%d 字节）" backup (FileInfo(backup).Length)
else
    printfn "备份已存在（不覆盖）→ %s（%d 字节）" backup (FileInfo(backup).Length)

File.Copy(src, dst, true)
printfn "已安装测试补丁 → %s（%d 字节）" dst (FileInfo(dst).Length)

// SHA256 校验
let sha (p: string) =
    use s = File.OpenRead p
    use h = Security.Cryptography.SHA256.Create()
    h.ComputeHash(s) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

let a = sha src
let b = sha dst
let c = sha backup
printfn "\n补丁  SHA256 = %s" a
printfn "已装  SHA256 = %s  %s" b (if a = b then "✅ 一致" else "❌ 不一致")
printfn "备份  SHA256 = %s  %s" c (if a <> c then "（与原版不同，符合预期）" else "⚠️ 与原版相同？")
printfn "\n还原方法：Copy-Item '%s' '%s' -Force" backup dst
