using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Parcilaes.FileHex;

public enum CompressMethod
{
    None = 0,
    SuperFast = 1,
    Fast = 3,
    Normal = 5,
    Compact = 7,
    Maximum = 9
}

/// <summary>
/// Conversión de clsCompress (VB6). Mantiene el MISMO formato binario del original, por lo que
/// puede leer y escribir archivos creados por la versión VB6:
///
///   Int16  cantidad de archivos
///   por archivo:
///     Byte   longitud del nombre, luego el nombre (ANSI/Latin1)
///     Int32  Packed   (bytes que siguen hasta el próximo archivo)
///     Int32  Size     (tamaño original)
///     Single Modified (fecha OLE/VB)
///     Double (sin uso)
///     segmentos: Int32 (comprimido-1), Int32 (original-1), datos zlib ... ; termina con Int32 0
///
/// Los datos de cada segmento son zlib estándar (compress2/uncompress), igual que zlib.dll.
/// Ya no se necesita zlib.dll ni CopyMemory: se usa System.IO.Compression.ZLibStream.
/// </summary>
public class FileCompress
{
    private const int MaxChunk = 262144;
    private const int MaxFiles = 32767;
    private const int InfoSize = 20; // Int32 + Int32 + Single + Double

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly record struct UniInfo(int Packed, int Size, float Modified);

    private List<(string Name, string Info)>? _files;
    private string _fileName = "";
    private string _errors = "";

    /// <summary>Errores acumulados; al leerlos se limpian (igual que el original).</summary>
    public string Errors
    {
        get
        {
            string e = _errors;
            _errors = "";
            return e;
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            if (_fileName.Length == 0) _files = null;
            else LoadFileList();
        }
    }

    public bool ExtractOverWrite { get; set; }
    public bool ExtractSelectNewer { get; set; }

    /// <summary>Cantidad de archivos en el contenedor, o -1 si no se ha cargado.</summary>
    public int FilesInFile => _files?.Count ?? -1;

    // ---------------------------------------------------------------------------------------
    // File operations

    private bool LoadFileList()
    {
        if (_fileName.Length == 0 || !File.Exists(_fileName)) return false;
        try
        {
            var list = new List<(string, string)>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            short count = br.ReadInt16();
            for (int i = 0; i < count; i++)
            {
                string name = ReadName(br);
                UniInfo info = ReadInfo(br);

                if (!keys.Add(name)) throw new InvalidDataException("Duplicate file name");

                string pct = (info.Size == 0 || info.Size <= info.Packed)
                    ? "0"
                    : Math.Floor(100 - (double)info.Packed / info.Size * 100).ToString(CultureInfo.InvariantCulture);

                list.Add((name,
                    $"{name}:{info.Size}:{info.Packed}:{pct}:{info.Modified.ToString(CultureInfo.InvariantCulture)}"));

                fs.Seek(info.Packed, SeekOrigin.Current);
            }

            _files = list;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool FileExists(string fileName) => File.Exists(fileName);

    /// <summary>Lista "nombre:tamaño:comprimido:%:fecha" de cada archivo, o null.</summary>
    public IReadOnlyList<string>? FileList()
    {
        LoadFileList();
        return _files?.Select(f => f.Info).ToList();
    }

    public bool FileAdd(string sourceFile, CompressMethod method = CompressMethod.Maximum)
    {
        if (_fileName.Length == 0) return false;
        if (!File.Exists(sourceFile)) return false;

        string simple = SimplifyFileName(sourceFile);
        byte[] nameBytes = Latin1.GetBytes(simple);
        if (nameBytes.Length > 255)
            throw new ArgumentException("File name too long (max 255 characters)", nameof(sourceFile));

        FileRemove(sourceFile); // reemplaza si ya existía

        if (!File.Exists(_fileName))
            File.WriteAllBytes(_fileName, new byte[2]); // Int16 = 0 archivos

        if (!LoadFileList()) return false;

        try
        {
            using (var fs = new FileStream(_fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var br = new BinaryReader(fs))
            using (var bw = new BinaryWriter(fs))
            {
                short count = br.ReadInt16();
                if (count + 1 > MaxFiles) return false;

                fs.Seek(0, SeekOrigin.End);
                bw.Write((byte)nameBytes.Length);
                bw.Write(nameBytes);
                long infoPos = fs.Position;

                using var src = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                var info = new UniInfo(0, checked((int)src.Length),
                    (float)File.GetLastWriteTime(sourceFile).ToOADate());
                WriteInfo(bw, info);

                int packed = 0;
                long remaining = src.Length;
                while (remaining > 0)
                {
                    // Igual que el original: el buffer tiene un byte extra (relleno en el último segmento).
                    int chunk = remaining > MaxChunk ? MaxChunk : (int)remaining;
                    int bufLen = chunk + 1;
                    var data = new byte[bufLen];
                    int read = (int)Math.Min(bufLen, remaining);
                    src.ReadExactly(data, 0, read);
                    remaining -= read;

                    byte[] comp = Deflate(data, method);
                    bw.Write(comp.Length - 1);
                    bw.Write(bufLen - 1);
                    bw.Write(comp);
                    packed += comp.Length + 8;
                }

                bw.Write(0);
                packed += 4;

                fs.Seek(infoPos, SeekOrigin.Begin);
                WriteInfo(bw, info with { Packed = packed });

                fs.Seek(0, SeekOrigin.Begin);
                bw.Write((short)(count + 1));
            }
            return LoadFileList();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Devuelve "nombre:tamaño:comprimido:%:fecha" o "" si no existe.</summary>
    public string GetFileInfo(string fileName)
    {
        if (!LoadFileList() || _files is null) return "";
        string simple = SimplifyFileName(fileName);
        foreach (var (name, info) in _files)
            if (string.Equals(name, simple, StringComparison.OrdinalIgnoreCase)) return info;
        return "";
    }

    public bool FileRemove(string fileName)
    {
        string simple = SimplifyFileName(fileName);
        if (GetFileInfo(simple).Length == 0) return false;

        string tmp = _fileName + ".tmp";
        try
        {
            using (var input = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (var br = new BinaryReader(input))
            using (var bw = new BinaryWriter(output))
            {
                short count = br.ReadInt16();
                bw.Write((short)(count - 1));
                for (int i = 0; i < count; i++)
                {
                    byte len = br.ReadByte();
                    byte[] nameBytes = br.ReadBytes(len);
                    byte[] infoBytes = br.ReadBytes(InfoSize);
                    int packed = BitConverter.ToInt32(infoBytes, 0);

                    if (string.Equals(Latin1.GetString(nameBytes), simple, StringComparison.OrdinalIgnoreCase))
                    {
                        input.Seek(packed, SeekOrigin.Current);
                    }
                    else
                    {
                        bw.Write(len);
                        bw.Write(nameBytes);
                        bw.Write(infoBytes);
                        CopyBytes(input, output, packed);
                    }
                }
            }

            File.Move(tmp, _fileName, overwrite: true);
            return true;
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Extrae uno o todos los archivos. Devuelve las rutas extraídas separadas por ';' ("" si falla).
    /// Por defecto extrae a la carpeta temporal.
    /// </summary>
    public string FileExtract(string extractPath = "", string fileName = "")
        => FileExtract(extractPath, fileName, out _);

    public string FileExtract(string extractPath, string fileName, out bool anyError)
    {
        anyError = false;
        if (!LoadFileList()) return "";
        try
        {
            if (extractPath.Length == 0) extractPath = Path.GetTempPath();
            Directory.CreateDirectory(extractPath);

            var extracted = new List<string>();
            using var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            short count = br.ReadInt16();

            for (int i = 0; i < count; i++)
            {
                string name = ReadName(br);
                UniInfo info = ReadInfo(br);

                bool extract = false;
                if (fileName.Length == 0 || fileName == name)
                {
                    // GetFileName evita que un nombre malicioso salga de la carpeta destino
                    string dest = Path.Combine(extractPath, Path.GetFileName(name));
                    extract = true;

                    if (File.Exists(dest))
                    {
                        extract = false;
                        if (ExtractSelectNewer)
                        {
                            if (File.GetLastWriteTime(dest) < DateTime.FromOADate(info.Modified))
                                extract = true;
                        }
                        else if (ExtractOverWrite)
                        {
                            extract = true;
                        }
                    }

                    if (extract)
                    {
                        extracted.Add(dest);
                        bool errors = false;
                        using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write))
                        {
                            int segSize = br.ReadInt32();
                            while (segSize > 0)
                            {
                                int origSize = br.ReadInt32();
                                byte[] comp = br.ReadBytes(segSize + 1);
                                byte[]? data = Inflate(comp);
                                segSize = br.ReadInt32();

                                if (data is null)
                                {
                                    errors = true;
                                    continue;
                                }
                                // El último segmento lleva un byte de relleno que se descarta.
                                int len = segSize == 0 ? Math.Min(data.Length, origSize) : data.Length;
                                output.Write(data, 0, len);
                            }
                        }

                        if (errors)
                        {
                            _errors += $"Error found while decompressing file '{name}'\r\n";
                            anyError = true;
                        }
                    }
                }

                if (!extract) fs.Seek(info.Packed, SeekOrigin.Current);
            }

            return string.Join(";", extracted);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Verifica que todos los archivos se puedan descomprimir (sin escribir a disco).
    /// "OK..." si todo bien, "--..." si hay errores.
    /// </summary>
    public string FileStatus()
    {
        if (!LoadFileList() || _files is null) return "--|Not a valid file";
        if (_files.Count == 0) return "";

        try
        {
            var sb = new StringBuilder();
            int numErrors = 0;

            using var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            short count = br.ReadInt16();

            for (int i = 0; i < count; i++)
            {
                string name = ReadName(br);
                ReadInfo(br);

                int errRet = 0;
                int segSize = br.ReadInt32();
                while (segSize > 0)
                {
                    br.ReadInt32(); // tamaño original
                    byte[] comp = br.ReadBytes(segSize + 1);
                    if (Inflate(comp) is null && errRet == 0) errRet = -2;
                    segSize = br.ReadInt32();
                }

                if (errRet == 0)
                {
                    sb.Append('|').Append(name).Append(": OK");
                }
                else
                {
                    numErrors++;
                    sb.Append('|').Append(name).Append(": Invalid compressed data");
                }
            }

            string details = sb.Length > 0 ? sb.ToString(1, sb.Length - 1) : "";
            return (numErrors == 0 ? "OK" : "--") + details;
        }
        catch
        {
            return "";
        }
    }

    // ---------------------------------------------------------------------------------------
    // Compress helpers

    private static byte[] Deflate(byte[] data, CompressMethod method)
    {
        var level = method switch
        {
            CompressMethod.None => CompressionLevel.NoCompression,
            CompressMethod.SuperFast => CompressionLevel.Fastest,
            CompressMethod.Fast or CompressMethod.Normal => CompressionLevel.Optimal,
            _ => CompressionLevel.SmallestSize
        };

        using var ms = new MemoryStream();
        using (var zs = new ZLibStream(ms, level, leaveOpen: true))
            zs.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static byte[]? Inflate(byte[] comp)
    {
        try
        {
            using var zs = new ZLibStream(new MemoryStream(comp), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            zs.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public bool Test()
    {
        try
        {
            var data = new byte[101];
            Array.Fill(data, (byte)'T', 0, 100);
            return Deflate(data, CompressMethod.SuperFast).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------
    // File manipulation helpers

    private static string ReadName(BinaryReader br)
    {
        byte len = br.ReadByte();
        byte[] bytes = br.ReadBytes(len);
        if (bytes.Length != len) throw new EndOfStreamException();
        return Latin1.GetString(bytes);
    }

    private static UniInfo ReadInfo(BinaryReader br)
    {
        int packed = br.ReadInt32();
        int size = br.ReadInt32();
        float modified = br.ReadSingle();
        br.ReadDouble(); // Dummy
        return new UniInfo(packed, size, modified);
    }

    private static void WriteInfo(BinaryWriter bw, UniInfo info)
    {
        bw.Write(info.Packed);
        bw.Write(info.Size);
        bw.Write(info.Modified);
        bw.Write(0d); // Dummy
    }

    private static void CopyBytes(Stream input, Stream output, long count)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (n == 0) throw new EndOfStreamException();
            output.Write(buffer, 0, n);
            count -= n;
        }
    }

    /// <summary>Quita la ruta (acepta '\' y '/').</summary>
    private static string SimplifyFileName(string fileName)
    {
        int i = fileName.LastIndexOfAny(new[] { '\\', '/' });
        return i >= 0 ? fileName[(i + 1)..] : fileName;
    }
}
