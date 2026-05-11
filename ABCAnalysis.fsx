#r "nuget: SixLabors.ImageSharp, 3.1.3"
#r "nuget: SixLabors.ImageSharp.Drawing, 2.1.2"

open System
open System.Collections.Generic
open System.IO


(*

1 4byte header
2 4byte unknown
3 4byte unknown
4 4byte unknown
5 4byte unknown
6 4byte enteryCount
7 4byte unk5

entryCount * uint16 过滤非0结果得到glyphCount

24 byte * glyphCount UNK

32 byte * glyCount 应该是字形矩形 x1 x2 y1 y2, 剩下是0填充，全部int32类型

*)

let byteToHex (data: byte[]) =
    let rawHex = BitConverter.ToString(data).Replace("-", "")
    let chunkedHex = rawHex |> Seq.chunkBySize 8 |> Seq.map (fun chars -> String(chars))
    String.Join("-", chunkedHex)

let byteToHexFormat (data: byte[]) (format : int []) =
    let chunks = 
        seq {
            let mutable idx = 0
            for f in format do
                let chk = data.[idx .. idx + f - 1]
                BitConverter.ToString(chk).Replace("-", "")
                idx <- idx + f
        }
    String.Join("-", chunks)

type ABCFile =
    { File: string
      FourCC: byte[]
      UNK1: byte[]
      UNK2: byte[]
      UNK3: byte[]
      UNK4: byte[]
      MapEntryCount: uint16
      Mappings: uint16[]
      UnkChunk1: byte[][]
      UnkChunk2: byte[][]
      GlyphCount: int32 }

let parse (file: string) =
    use f = File.OpenRead(file)
    use br = new BinaryReader(f)

    let fourCC = br.ReadBytes(4)

    let unk1, unk2, unk3, unk4 =
        br.ReadBytes(4), br.ReadBytes(4), br.ReadBytes(4), br.ReadBytes(4)

    let entryCount = br.ReadUInt16()

    // magic 3
    // 反正设置到3以后，后面char mapping的时候就不需要位移了

    // 先转换int32再+3，否则未来考虑0xFFFF的时候会溢出
    let mappings = Array.init ((int32 entryCount) + 3) (fun _ -> br.ReadUInt16())
    let mapCount = mappings |> Array.sumBy (fun i -> if i = 0us then 0 else 1)

    let unkChunk1 = Array.init mapCount (fun _ -> br.ReadBytes(24))
    let unkChunk2 = Array.init mapCount (fun _ -> br.ReadBytes(32))

    if (br.PeekChar()) <> -1 then
        failwithf "流还没有结束，肯定有问题"

    let ret =
        { ABCFile.File = file
          ABCFile.FourCC = fourCC
          ABCFile.UNK1 = unk1
          ABCFile.UNK2 = unk2
          ABCFile.UNK3 = unk3
          ABCFile.UNK4 = unk4
          ABCFile.MapEntryCount = entryCount
          ABCFile.Mappings = mappings
          ABCFile.UnkChunk1 = unkChunk1
          ABCFile.UnkChunk2 = unkChunk2
          ABCFile.GlyphCount = mapCount }

    ret

let abcs =
    [| yield! Directory.EnumerateFiles("../ABCDump", "*.abc")
       yield @"G:\SteamLibrary\steamapps\common\Getsuei Gakuen -kou-\data\text\jpn\jpn_glyph.abc" |]
    |> Array.map (fun path -> parse path)


let getMinGlyCount () =
    let abc = abcs |> Array.minBy (fun abc -> abc.GlyphCount)

    abc

let dumpSections (tag: string) (func: ABCFile -> byte[][]) (formatter : int []) (len : int)=
    if formatter|> Array.sum <> len then
        invalidArg "formatter" "数量不对！"

    let sb = Text.StringBuilder()

    for abc in abcs do
        sb.AppendLine($"[{abc.File}]") |> ignore
        sb.AppendLine() |> ignore

        sb.AppendFormat(
            "{0} {1} {2} {3}",
            byteToHex abc.UNK1,
            byteToHex abc.UNK2,
            byteToHex abc.UNK3,
            byteToHex abc.UNK4
        )
        |> ignore

        sb.AppendLine() |> ignore
        sb.AppendLine() |> ignore

        for chunk in func abc do
            sb.AppendLine(byteToHexFormat chunk formatter) |> ignore

        sb.AppendLine() |> ignore

    File.WriteAllText($"{tag}.txt", sb.ToString())

//dumpSections "UNK1" (fun abc -> abc.UnkChunk1) [| 2;2;2;2;2;2;2;2;2;2;2;2; |] 24

let dumpHeaders() =
    let sb = Text.StringBuilder()

    for abc in abcs do
        sb.Append($"[%20s{Path.GetFileNameWithoutExtension(abc.File)}]\t") |> ignore
        sb.AppendFormat(
            "{0} {1} {2} {3}",
            byteToHex abc.UNK1,
            byteToHex abc.UNK2,
            byteToHex abc.UNK3,
            byteToHex abc.UNK4
        )
        |> ignore

        sb.AppendLine() |> ignore

    File.WriteAllText($"headers.txt", sb.ToString())

//dumpHeaders()

let dumpUnknowns () =
    let sb = Text.StringBuilder()

    let writeSection (tag: string) (func: ABCFile -> byte[]) =
        sb.AppendLine($"[{tag}]") |> ignore

        for abc in abcs do
            sb.AppendLine(abc |> (func >> byteToHex)) |> ignore

        sb.AppendLine() |> ignore

        let uniques = abcs |> Array.map func |> Array.distinct
        sb.AppendLine($"[{tag}-unique]") |> ignore

        for uniq in uniques do
            sb.AppendLine(byteToHex uniq) |> ignore

        sb.AppendLine() |> ignore


    writeSection "UNK1" (fun abc -> abc.UNK1)
    writeSection "UNK2" (fun abc -> abc.UNK2)
    writeSection "UNK3" (fun abc -> abc.UNK3)
    writeSection "UNK4" (fun abc -> abc.UNK4)

    File.WriteAllText("analysis.txt", sb.ToString())


// 测试绘制

open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Drawing.Processing

let drawRectangles (inputPath: string) (outputPath: string) (rects: (float32 * float32 * float32 * float32) array) =
    try
        // 1. 读取原图片内容
        use image = Image.Load<Rgba32>(inputPath)

        // 2. 进入图像修改上下文
        image.Mutate(fun ctx ->
            // 定义画笔颜色 (例如红色) 和 1px 宽度
            let color = Color.Red
            let thickness = 1.0f

            for (x1, x2, y1, y2) in rects do
                // 转换坐标系：(x1, x2, y1, y2) 转换为标准矩形的 (x, y, 宽度, 高度)
                let x = Math.Min(x1, x2)
                let y = Math.Min(y1, y2)
                let width = Math.Abs(x2 - x1)
                let height = Math.Abs(y2 - y1)

                let rect = RectangleF(x, y, width, height)

                // 3. 在图片上绘制矩形边框
                ctx.Draw(color, thickness, rect) |> ignore)

        // 4. 将绘制后的结果保存到输出路径
        image.Save(outputPath)
        printfn "图片绘制完成，已成功保存至: %s" outputPath

    with ex ->
        printfn "处理图片时发生错误: %s" ex.Message

let drawFontOverlay () =
    let fn = "text_10_1-01"
    let abc = parse $"{fn}.abc"

    let rects =
        abc.UnkChunk2
        |> Array.map (fun item ->
            let chk =
                item |> Array.chunkBySize 4 |> Array.map (fun chk -> BitConverter.ToInt32(chk))

            let x1 = float32 chk.[0]
            let x2 = float32 chk.[1]
            let y1 = float32 chk.[2]
            let y2 = float32 chk.[3]
            (x1, x2, y1, y2))



    File.WriteAllLines($"{fn}.glyph.txt", rects |> Array.map (fun points -> String.Join(", ", points)))

    drawRectangles $"{fn}.png" $"{fn}-overlay.png" rects

//drawFontOverlay ()

let dumpMappingIndex () =
    let abc = parse "text_10_1-01.abc"

    let sb = Text.StringBuilder()

    abc.Mappings
    |> Array.iteri (fun idx us ->
        if us <> 0us then
            // MAGIC!
            let offset = 0
            sb.AppendLine($"{us}, {idx + offset}={char (idx + offset)}/{(idx + offset)}") |> ignore)

    File.WriteAllText("text_10_1-01_MappingList.txt", sb.ToString())

dumpMappingIndex ()
