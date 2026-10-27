namespace AtfJson

open System.IO
open System.Text.Encodings.Web
open System.Text.Json

/// JSON 读写：只存放人类需要编辑的最小内容
///   type                        : 魔数（默认 "ATF"）
///   trailingEmptyStringHeaders  : 尾部空 StringHeader 数（为 0 时不写出）
///   records[]                   : { key, value, textParam? }（textParam 全 0 时不写出）
module AtfJsonIO =

    let writeJson (atf: AtfFile) (path: string) =
        use fs = new FileStream(path, FileMode.Create, FileAccess.Write)
        // UnsafeRelaxedJsonEscaping：不转义日文、全角空格及 < > 等字符，保证可读性
        let opts =
            JsonWriterOptions(Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
        use w = new Utf8JsonWriter(fs, opts)

        w.WriteStartObject()
        w.WriteString("type", AtfBinary.typeName atf.Type)
        if atf.TrailingEmptyStringHeaders <> 0 then
            w.WriteNumber("trailingEmptyStringHeaders", atf.TrailingEmptyStringHeaders)

        w.WriteStartArray("records")
        for r in atf.Records do
            w.WriteStartObject()
            w.WriteString("key", r.Key)
            w.WriteString("value", r.Value)
            if r.TextParam |> Array.exists ((<>) 0u) then
                w.WriteStartArray("textParam")
                for p in r.TextParam do
                    w.WriteNumberValue(p)
                w.WriteEndArray()
            w.WriteEndObject()
        w.WriteEndArray()

        w.WriteEndObject()
        w.Flush()
        fs.WriteByte('\n'B)

    let readJson (path: string) : AtfFile =
        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let root = doc.RootElement

        let typ =
            match root.TryGetProperty("type") with
            | true, p -> AtfBinary.typeValue (p.GetString())
            | _ -> 0x465441u // "ATF"

        let trailing =
            match root.TryGetProperty("trailingEmptyStringHeaders") with
            | true, p -> p.GetInt32()
            | _ -> 0

        let records =
            [| for r in root.GetProperty("records").EnumerateArray() ->
                   let key =
                       match r.TryGetProperty("key") with
                       | true, p -> p.GetString()
                       | _ -> ""
                   let value =
                       match r.TryGetProperty("value") with
                       | true, p -> p.GetString()
                       | _ -> ""
                   let ps =
                       match r.TryGetProperty("textParam") with
                       | true, p -> [| for x in p.EnumerateArray() -> x.GetUInt32() |]
                       | _ -> [||]
                   // 固定补/截到 6 个
                   let textParam = Array.init 6 (fun i -> if i < ps.Length then ps[i] else 0u)
                   { Key = key
                     Value = value
                     TextParam = textParam } |]

        { Type = typ
          TrailingEmptyStringHeaders = trailing
          Records = records }
