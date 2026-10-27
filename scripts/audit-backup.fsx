// 只读盘点：对比 work/backup 与游戏目录，列出被改动的文件（不写任何文件到游戏目录）
open System
open System.IO
open System.Security.Cryptography

let backupRoot = @"work\backup"
let gameRoot = @"<游戏安装目录>"

let sha (p: string) =
    use s = File.OpenRead p
    use h = SHA256.Create()
    Convert.ToHexString(h.ComputeHash s)

// 备份目录 → 游戏目录的映射
let mapping =
    [ "story\\text\\jpn", "data\\story\\text\\jpn"          // 237 个对话 pac
      "story\\image-menu", "data\\story\\image"             // 18 张菜单图
      "story\\image", "data\\story\\image"                  // 35 张台词图（与上者同目录）
      "text\\jpn", "data\\text\\jpn"                        // 字库
      "AE\\adv", "data\\AE\\adv"
      "AE\\common", "data\\AE\\common"
      "AE\\title", "data\\AE\\title"
      "image", "data\\story\\image" ]

let mutable changed = 0
let mutable same = 0
let mutable missing = 0

for (bp, gp) in mapping do
    let bdir = Path.Combine(backupRoot, bp)
    if Directory.Exists bdir then
        printfn "── %s → %s" bp gp
        for f in Directory.GetFiles(bdir, "*", SearchOption.AllDirectories) do
            let rel = f.Substring(bdir.Length).TrimStart('\\')
            let target = Path.Combine(gameRoot, gp, rel)
            if not (File.Exists target) then
                printfn "   ⚠️ 游戏里不存在：%s" rel
                missing <- missing + 1
            elif sha f = sha target then
                same <- same + 1
            else
                printfn "   ✏️ 已改动：%s" rel
                changed <- changed + 1

printfn ""
printfn "备份文件总数：%d" (same + changed + missing)
printfn "  未改动 %d ／ 已改动 %d ／ 目标缺失 %d" same changed missing
printfn ""
printfn "还原命令（确认后再执行）："
printfn "  Copy-Item 'work\\backup\\story\\text\\jpn\\*' '%s\\data\\story\\text\\jpn\\' -Force" gameRoot
printfn "  Copy-Item 'work\\backup\\story\\image-menu\\*' '%s\\data\\story\\image\\' -Force" gameRoot
printfn "  Copy-Item 'work\\backup\\story\\image\\*' '%s\\data\\story\\image\\' -Force" gameRoot
