// 导出 exe 里 UI 资源名字表（0xD911C8 附近的 ASCII 字符串池）与 pac 名表（0xCE3D40 附近）
open System
open System.IO
open System.Text
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let exe = @"<游戏安装目录>\Getsuei.exe"
let bytes = File.ReadAllBytes exe

/// 从 offset 起提取连续的 NUL 结尾 ASCII 字符串
let pool (off: int) (maxLen: int) =
    let res = ResizeArray<string>()
    let mutable i = off
    let mutable stop = false
    while not stop && i < min bytes.Length (off + maxLen) do
        // 跳过连续 NUL
        while i < bytes.Length && bytes.[i] = 0uy do i <- i + 1
        if i >= min bytes.Length (off + maxLen) then stop <- true
        else
            let start = i
            while i < bytes.Length && bytes.[i] <> 0uy do i <- i + 1
            if i > start then
                let s = Encoding.ASCII.GetString(bytes, start, i - start)
                if s.Length >= 2 && s |> Seq.forall (fun c -> c >= ' ' && c <= '~') then res.Add s
                elif s.Length > 0 then res.Add("·" + s)
            i <- i + 1
    res

printfn "===== UI 资源名池（0xD91100 起 4KB）====="
let a = pool 0xD91100 4096
printfn "共 %d 条：" a.Count
a |> Seq.iter (fun s -> printfn "  %s" s)

printfn "\n===== pac 名池（0xCE3D00 起 2KB）====="
let b = pool 0xCE3D00 2048
printfn "共 %d 条：" b.Count
b |> Seq.truncate 60 |> Seq.iter (fun s -> printfn "  %s" s)
