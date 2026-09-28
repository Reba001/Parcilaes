using System.IO.Compression;
using System.Text;

namespace Parcilaes.FileHex;

/// <summary>
/// Reemplazo de la clase VB6 clsCompress. Implemente su propia versión si
/// necesita compatibilidad con el formato original.
/// </summary>
public interface IFileCompressor
{
    /// <summary>Comprime <paramref name="sourceFile"/> dentro de <paramref name="archiveFile"/>.</summary>
    bool AddFile(string archiveFile, string sourceFile);

    /// <summary>Extrae el archivo comprimido y devuelve la ruta del archivo extraído ("" si falla).</summary>
    string Extract(string archiveFile);
}

/// <summary>Implementación por defecto basada en ZIP (System.IO.Compression).</summary>
public sealed class ZipFileCompressor : IFileCompressor
{
    public bool AddFile(string archiveFile, string sourceFile)
    {
        try
        {
            if (File.Exists(archiveFile)) File.Delete(archiveFile);
            using var zip = ZipFile.Open(archiveFile, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile), CompressionLevel.Optimal);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string Extract(string archiveFile)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archiveFile);
            var entry = zip.Entries.FirstOrDefault();
            if (entry is null) return "";

            var dir = Path.GetDirectoryName(Path.GetFullPath(archiveFile))!;
            var target = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(entry.FullName)));
            entry.ExtractToFile(target, overwrite: true);
            return target;
        }
        catch
        {
            return "";
        }
    }
}

/// <summary>
/// Conversión de clsFileHex (VB6). Codifica un archivo binario como "hexadecimal"
/// ASCII con una base configurable (por defecto 61 = '=') y lo decodifica de vuelta.
/// </summary>
public class FileHex
{
    // VB6 usaba Asc/Chr con la página ANSI; Latin1 mapea byte <-> char 1:1.
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly IFileCompressor _compressor;

    public FileHex() : this(new ZipFileCompressor()) { }

    public FileHex(IFileCompressor compressor)
    {
        _compressor = compressor;
    }

    /// <summary>Valor base sumado a cada nibble (por defecto 61, "=" después de "&lt;").</summary>
    public int Base { get; set; } = 61;

    /// <summary>Archivo de origen con el que trabajan la mayoría de los métodos.</summary>
    public string FileName { get; set; } = "";

    /// <summary>Codifica <see cref="FileName"/> a <paramref name="fileDest"/>. Devuelve false ante cualquier error.</summary>
    public bool FileAsciToHex(string fileDest, bool compress = true)
    {
        string fileTemp = compress ? FileName + ".cmp" : FileName;
        try
        {
            if (compress && !_compressor.AddFile(fileTemp, FileName))
                throw new InvalidOperationException("Couldn't use compression");

            byte[] source = File.ReadAllBytes(fileTemp);
            var dest = new byte[source.Length * 2 + 2];
            dest[0] = (byte)Base;
            dest[1] = (byte)((compress ? 1 : 0) + Base);
            for (int i = 0; i < source.Length; i++)
            {
                dest[(i + 1) * 2] = (byte)((source[i] / 16) + Base);
                dest[(i + 1) * 2 + 1] = (byte)((source[i] % 16) + Base);
            }

            File.WriteAllBytes(fileDest, dest); // sobrescribe si existe

            if (compress) TryDelete(fileTemp);
            return true;
        }
        catch
        {
            if (compress) TryDelete(fileTemp);
            return false;
        }
    }

    /// <summary>Decodifica <see cref="FileName"/> a <paramref name="fileDest"/>. Lanza excepción ante error.</summary>
    public bool FileHexToAsci(string fileDest)
    {
        byte[] source = File.ReadAllBytes(FileName);
        if (source.Length < 2)
            throw new InvalidDataException("Invalid hex file");

        Base = source[0];
        bool compress = source[1] == Base + 1;

        int count = (source.Length - 2) / 2;
        var dest = new byte[count];
        for (int i = 0; i < count; i++)
        {
            dest[i] = (byte)(((source[(i + 1) * 2] - Base) * 16) +
                             (source[(i + 1) * 2 + 1] - Base));
        }

        string fileTemp = compress ? fileDest + ".tmpcmp" : fileDest;
        TryDelete(fileTemp);
        File.WriteAllBytes(fileTemp, dest);

        if (compress)
        {
            string extracted = _compressor.Extract(fileTemp);
            if (extracted.Length == 0)
                throw new InvalidOperationException("Couldn't decompress");

            TryDelete(fileDest);
            TryDelete(fileTemp);
            File.Move(extracted, fileDest);
        }

        return true;
    }

    /// <summary>
    /// Compara <see cref="FileName"/> con <paramref name="fileNameComp"/>.
    /// -1 = iguales; -2 = el actual es más grande; -3 = el actual es más pequeño;
    /// n &gt;= 0 = última posición distinta (buscando desde el final); -1000 = error.
    /// </summary>
    public long FileComp(string fileNameComp)
    {
        try
        {
            long len1 = new FileInfo(FileName).Length;
            long len2 = new FileInfo(fileNameComp).Length;
            if (len1 > len2) return -2;
            if (len1 < len2) return -3;

            byte[] data1 = File.ReadAllBytes(FileName);
            byte[] data2 = File.ReadAllBytes(fileNameComp);
            for (int i = data1.Length - 1; i >= 0; i--)
            {
                if (data1[i] != data2[i]) return i;
            }
            return -1;
        }
        catch
        {
            return -1000;
        }
    }

    /// <summary>
    /// Agrega un segmento de texto (1..total) al archivo. En el segmento 1 reinicia el temporal;
    /// en el último reemplaza <see cref="FileName"/>. Lanza excepción ante error.
    /// </summary>
    public bool SaveStringToFile(string data, int actualSegment, int totalSegments)
    {
        string tmp = FileName + ".tmp";
        byte[] bytes = Latin1.GetBytes(data);

        using (var fs = new FileStream(tmp,
                   actualSegment == 1 ? FileMode.Create : FileMode.Append,
                   FileAccess.Write))
        {
            fs.Write(bytes, 0, bytes.Length);
        }

        if (actualSegment == totalSegments)
        {
            TryDelete(FileName);
            File.Move(tmp, FileName);
        }
        return true;
    }

    /// <summary>Devuelve todo el archivo como cadena (1 char por byte); "" si vacío o error.</summary>
    public string LoadFileToString()
    {
        try
        {
            // Igual que el original: archivos de 1 byte o menos devuelven "".
            if (new FileInfo(FileName).Length <= 1) return "";
            return Latin1.GetString(File.ReadAllBytes(FileName));
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Devuelve el segmento <paramref name="fileSegment"/> (base 1) del archivo.
    /// En el segmento 1 calcula <paramref name="totalSegments"/>. "" ante error.
    /// </summary>
    public string LoadFileToString2(int fileSegment, ref int totalSegments, int segmentSize = 10000)
    {
        if (fileSegment == 0) return "";
        try
        {
            long fileLen = new FileInfo(FileName).Length;
            if (fileSegment == 1)
                totalSegments = (int)((fileLen + segmentSize - 1) / segmentSize);

            long offset = (long)(fileSegment - 1) * segmentSize;
            long remaining = fileLen - offset;
            int toRead = (int)Math.Max(0, Math.Min(segmentSize, remaining));

            var buffer = new byte[toRead];
            using var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            fs.Seek(offset, SeekOrigin.Begin);
            fs.ReadExactly(buffer, 0, toRead);
            return Latin1.GetString(buffer);
        }
        catch
        {
            return "";
        }
    }

    public bool FileExists(string fileName) => File.Exists(fileName);

    public bool FileDelete(string fileName)
    {
        try
        {
            File.Delete(fileName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* On Error Resume Next */ }
    }
}
