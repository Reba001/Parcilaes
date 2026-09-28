using System.Text;

namespace Parcilaes.FileHex;

/// <summary>
/// Conversión de clsCRC32 (VB6). Usa el mismo algoritmo (CRC32 reflejado por tablas) con la semilla
/// por defecto del original 0xABCD1234, que NO es el polinomio estándar 0xEDB88320; por eso los
/// resultados no coinciden con un CRC32 "normal", pero sí con los de la versión VB6.
/// </summary>
public class Crc32
{
    public const uint DefaultSeed = 0xABCD1234;
    public const uint DefaultPrecondition = 0xFFFFFFFF;

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly uint[] _table = new uint[256];
    private uint _crc;

    public Crc32() => Init();

    /// <summary>Genera la tabla con <paramref name="seed"/> y reinicia el acumulador.</summary>
    public uint Init(uint seed = DefaultSeed, uint precondition = DefaultPrecondition)
    {
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ seed : crc >> 1;
            _table[i] = crc;
        }

        _crc = precondition;
        return _crc;
    }

    /// <summary>Acumula el CRC de una cadena (1 byte por carácter, Latin1). Con <paramref name="init"/> reinicia con valores por defecto.</summary>
    public uint AddStringCrc32(string item, bool init = true)
    {
        if (init) Init();
        Update(Latin1.GetBytes(item));
        return GetCrc32();
    }

    /// <summary>Acumula el CRC del contenido de un archivo. Lanza excepción si no se puede leer.</summary>
    public uint AddFileCrc32(string fileName, bool init = true)
    {
        if (init) Init();
        Update(File.ReadAllBytes(fileName));
        return GetCrc32();
    }

    /// <summary>CRC actual (valor acumulado XOR 0xFFFFFFFF).</summary>
    public uint GetCrc32() => _crc ^ 0xFFFFFFFF;

    private void Update(ReadOnlySpan<byte> data)
    {
        uint crc = _crc;
        foreach (byte b in data)
            crc = (crc >> 8) ^ _table[(crc & 0xFF) ^ b];
        _crc = crc;
    }
}
