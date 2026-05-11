open System
open System.IO

let file = @"text_10_4-10.pac"

// def from https://github.com/super-continent/arcsys/blob/main/src/pac.rs

type PacStyle =
    | Normal = 0x0
    | AutoLenFilename = 0x1
    | IdOnly = 0x2
    | PathCut = 0x10
    | LongName = 0x20000000
    | HashSort = 0x40000000
    | Version2 = 0x80000000

type Header =
    {
        // total 32 bytes
        FourCC: string
        DataStart: int32 // HeaderSize + Meta Size
        TotalSize: int32 // 本身文件大小
        FileCount: int32
        PacStyle: PacStyle
        /// 指示每个文件名有多长
        StringSize: int32
        Padding: uint64
    }

    static member RecordLength = 32

    static member Read(r: BinaryReader) =
        { FourCC = r.ReadChars(4) |> String
          DataStart = r.ReadInt32()
          TotalSize = r.ReadInt32()
          FileCount = r.ReadInt32()
          PacStyle = enum<PacStyle> (r.ReadInt32())
          StringSize = r.ReadInt32()
          Padding = r.ReadUInt64() }


type FileInfo =
    { // total 48 bytes
      Filename: string // 32 bytes
      FileId: int32
      OffsetStart: int32
      Length: int32
      FileNameHash: string }

    static member CalculateFileNameHash(fileName: string) =
        let hash =
            Text.Encoding.UTF8.GetBytes(fileName)
            |> Array.fold (fun hash c -> uint32 c + 137u * hash) 0u

        BitConverter.GetBytes(hash)

    static member RecordLength = 48

    static member Read(r: BinaryReader) =
        {
          // 需要修改，长度需要由Header指定
          Filename = (r.ReadChars(32) |> String).TrimEnd('\000')
          FileId = r.ReadInt32()
          OffsetStart = r.ReadInt32()
          Length = r.ReadInt32()
          FileNameHash = BitConverter.ToString(r.ReadBytes(4)).Replace("-", "") //r.ReadInt32()
        }


let read () =
    use file = File.OpenRead(file)
    use br = new BinaryReader(file)

    let hdr = Header.Read(br)

    printfn "%A" hdr

    Console.WriteLine($"There are {hdr.FileCount} files")

    for i = 0 to hdr.FileCount - 1 do
        let fi = FileInfo.Read(br)
        let current = br.BaseStream.Position
        //let dataStart = Header.RecordLength + hdr.FileCount * FileInfo.RecordLength + fi.OffsetStart
        let dataStart = hdr.DataStart + fi.OffsetStart
        br.BaseStream.Position <- int64 dataStart
        let data = br.ReadBytes(fi.Length)
        br.BaseStream.Position <- current
        printfn "%A" fi
        File.WriteAllBytes(Path.Join("text_10_4-10", fi.Filename), data)
        ()


read ()


let batchRead() = 
    let folder = @"G:\SteamLibrary\steamapps\common\Getsuei Gakuen -kou-\data\story\text\jpn"

    for path in Directory.EnumerateFiles(folder, "*.pac") do
        printfn "Processing %s" path

        use file = File.OpenRead(path)
        use br = new BinaryReader(file)
        let hdr = Header.Read(br)
        for i = 0 to hdr.FileCount - 1 do
            let fi = FileInfo.Read(br)

            if fi.Filename.Contains(".abc") || fi.Filename.Contains(".hip") then
                let current = br.BaseStream.Position
                let dataStart = hdr.DataStart + fi.OffsetStart
                br.BaseStream.Position <- int64 dataStart
                let data = br.ReadBytes(fi.Length)
                br.BaseStream.Position <- current

                let ext = 
                    if fi.Filename.Contains(".abc") then ".abc"
                    else
                        // hip文件有两种，如果是HIP开头就是HIP文件，用GeoArcSysAIOCLITool工具转换为PNG。如果不是就是TGA文件，随便找个转换器搞定
                        let threeCc = Text.Encoding.UTF8.GetString(data,0,3) 
                        if threeCc = "HIP" then ".hip" else ".tga"

                let baseName = Path.GetFileNameWithoutExtension(path) + ext
                
                File.WriteAllBytes(Path.Join("ABCDump", baseName), data)

//batchRead()
