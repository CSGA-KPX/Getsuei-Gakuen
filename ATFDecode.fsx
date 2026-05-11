#r "nuget: Newtonsoft.Json"
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

open Newtonsoft.Json
open Newtonsoft.Json.Linq


// 参考 https://github.com/WistfulHopes/ASWTools/blob/master/ATFText/Program.cs

type FileHeader = { Type: uint32; TextNum: uint32 }

type ParamHeader =
    { Offset: uint32
      Num: uint32
      Size: uint32 }

type TextHeader =
    { NameIdx: uint32
      TextIdx: uint32
      TextParam: uint32 list }

type StringHeader = { Top: uint32; Len: uint32 }

type AdvTextData =
    { FileHeader: FileHeader
      ParamHeaders: ParamHeader list
      TextHeaders: TextHeader list
      StringHeaders: StringHeader list
      Strings: string list
      WStrings: string list }

type OutputFormat = { Id: string; Value: string }

let build (input) (output) =
    if File.Exists(input) then
        use sr = new StreamReader(input)
        let keys = ResizeArray<string>()
        let values = ResizeArray<string>()

        while not sr.EndOfStream do
            let line = sr.ReadLine()

            if line.StartsWith("Key: ") then
                keys.Add(line.Substring(5))
            elif line.StartsWith("Value: ") then
                values.Add(line.Substring(7))
            else if values.Count > 0 then
                values.[values.Count - 1] <- values.[values.Count - 1] + line

        let keyList = Seq.toList keys
        let valList = Seq.toList values

        let strLen = keyList |> List.sumBy (fun k -> k.Length + 2) |> uint32
        let wstrLen = valList |> List.sumBy (fun v -> v.Length + 1) |> uint32

        let fileHeader =
            { Type = 0x465441u
              TextNum = uint32 keyList.Length }

        let p1 =
            { Offset = 0x50u
              Num = uint32 keyList.Length
              Size = 0x30u * uint32 keyList.Length }

        let p2 =
            { Offset = p1.Offset + p1.Size
              Num = uint32 (keyList.Length + valList.Length)
              Size = 0x10u * uint32 (keyList.Length + valList.Length) }

        let p3 =
            { Offset = p2.Offset + p2.Size
              Num = strLen
              Size = strLen }

        let p4 =
            { Offset = p3.Offset + p3.Size
              Num = wstrLen
              Size = wstrLen * 2u }

        let paramHeaders = [ p1; p2; p3; p4 ]

        let textHeaders =
            keyList
            |> List.mapi (fun i _ ->
                { NameIdx = uint32 (i * 2)
                  TextIdx = uint32 (i * 2 + 1)
                  TextParam = [ 0u; 0u; 0u; 0u; 0u; 0u ] })

        let mutable strTop = 0u
        let mutable wstrTop = 0u

        let stringHeaders =
            [ for i in 0 .. (keyList.Length + valList.Length - 1) do
                  if i % 2 = 0 then
                      let len = uint32 (keyList.[i / 2].Length + 1)
                      yield { Top = strTop; Len = len }
                      strTop <- strTop + len + 1u
                  else
                      let len = uint32 (valList.[(i - 1) / 2].Length)
                      yield { Top = wstrTop; Len = len }
                      wstrTop <- wstrTop + len + 1u ]

        use fs = new FileStream(output, FileMode.Create)
        use bw = new BinaryWriter(fs)

        bw.Write(fileHeader.Type)
        bw.Write(fileHeader.TextNum)
        bw.Write(0u)
        bw.Write(0u)

        for p in paramHeaders do
            bw.Write(p.Offset)
            bw.Write(p.Num)
            bw.Write(p.Size)
            bw.Write(0u)

        for t in textHeaders do
            bw.Write(t.NameIdx)
            bw.Write(t.TextIdx)
            t.TextParam |> List.iter bw.Write
            bw.Write(0u)
            bw.Write(0u)
            bw.Write(0u)
            bw.Write(0u)

        for s in stringHeaders do
            bw.Write(s.Top)
            bw.Write(s.Len)
            bw.Write(0u)
            bw.Write(0u)

        for k in keyList do
            if k <> "" then
                bw.Write(Encoding.ASCII.GetBytes(k))

            bw.Write(0us)

        for v in valList do
            if v <> "" then
                bw.Write(Encoding.Unicode.GetBytes(v))

            bw.Write(0us)

let decompile (input) =
    use fs = new FileStream(input, FileMode.Open)
    use br = new BinaryReader(fs)

    let fileHeader = { Type = br.ReadUInt32(); TextNum = br.ReadUInt32() }
    br.BaseStream.Seek(8L, SeekOrigin.Current) |> ignore

    let paramHeaders =
        [ for _ in 1..4 do
              let p =
                  { Offset = br.ReadUInt32()
                    Num = br.ReadUInt32()
                    Size = br.ReadUInt32() }

              br.BaseStream.Seek(4L, SeekOrigin.Current) |> ignore
              yield p ]

    br.BaseStream.Seek(int64 paramHeaders.[0].Offset, SeekOrigin.Begin) |> ignore

    let textHeaders =
        [ for _ in 1u .. paramHeaders.[0].Num do
              let nameIdx = br.ReadUInt32()
              let textIdx = br.ReadUInt32()

              let textParam =
                  [ for _ in 1..6 do
                        yield br.ReadUInt32() ]

              br.BaseStream.Seek(16L, SeekOrigin.Current) |> ignore

              yield
                  { NameIdx = nameIdx
                    TextIdx = textIdx
                    TextParam = textParam } ]

    br.BaseStream.Seek(int64 paramHeaders.[1].Offset, SeekOrigin.Begin) |> ignore

    let stringHeaders =
        [ for _ in 1u .. paramHeaders.[1].Num do
              let top = br.ReadUInt32()
              let len = br.ReadUInt32()
              br.BaseStream.Seek(8L, SeekOrigin.Current) |> ignore
              yield { Top = top; Len = len } ]

    br.BaseStream.Seek(int64 paramHeaders.[2].Offset, SeekOrigin.Begin) |> ignore

    let strings =
        textHeaders
        |> List.map (fun t ->
            let sh = stringHeaders.[int t.NameIdx]

            br.BaseStream.Seek(int64 paramHeaders.[2].Offset + int64 sh.Top, SeekOrigin.Begin)
            |> ignore

            if sh.Len = 0u then
                ""
            else
                Encoding.ASCII.GetString(br.ReadBytes(int sh.Len - 1)))

    br.BaseStream.Seek(int64 paramHeaders.[3].Offset, SeekOrigin.Begin) |> ignore

    let wstrings =
        textHeaders
        |> List.map (fun t ->
            let sh = stringHeaders.[int t.TextIdx]

            br.BaseStream.Seek(int64 paramHeaders.[3].Offset + int64 (sh.Top * 2u), SeekOrigin.Begin)
            |> ignore

            if sh.Len = 0u then
                ""
            else
                Encoding.Unicode.GetString(br.ReadBytes(int sh.Len * 2)))

    //List.map2 (fun k v -> { Id = k; Value = v }) strings wstrings

    { FileHeader = fileHeader
      ParamHeaders = paramHeaders
      TextHeaders = textHeaders
      StringHeaders = stringHeaders
      Strings = strings
      WStrings = wstrings }

let inFile = @"text_10_4-10\string.atf"

File.WriteAllText(@"text_10_4-10\string.json", JsonConvert.SerializeObject(decompile inFile,Formatting.Indented))
//decompile inFile
