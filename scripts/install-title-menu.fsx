// 标题菜单图装机：18 张 → data\story\image\（自动备份原文件到 work/backup/story/image-menu/）
open System
open System.IO
open System.Security.Cryptography

let gameDir = @"<游戏安装目录>\data\story\image"
let srcDir = "work/patch-menu"
let backupDir = "work/backup/story/image-menu"

let sha (p: string) =
    use s = File.OpenRead p
    use h = SHA256.Create()
    h.ComputeHash(s) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

Directory.CreateDirectory backupDir |> ignore

let files = Directory.GetFiles(srcDir, "*.hip") |> Array.sort
printfn "待装机 %d 张\n" files.Length

let mutable backedUp = 0
let mutable installed = 0
let mutable mismatch = 0

for src in files do
    let name = Path.GetFileName src
    let dst = Path.Combine(gameDir, name)
    if not (File.Exists dst) then
        printfn "⚠️  目标不存在：%s" name
    else
        let bak = Path.Combine(backupDir, name)
        if not (File.Exists bak) then
            File.Copy(dst, bak)
            backedUp <- backedUp + 1
        File.Copy(src, dst, true)
        installed <- installed + 1
        if sha src <> sha dst then
            mismatch <- mismatch + 1
            printfn "❌ SHA 不一致：%s" name

printfn "已备份 %d 个原文件 → %s" backedUp backupDir
printfn "已安装 %d 个文件，SHA 不一致 %d 个" installed mismatch
printfn "\n还原：Get-ChildItem '%s' | ForEach-Object { Copy-Item $_.FullName '%s' -Force }" backupDir gameDir
