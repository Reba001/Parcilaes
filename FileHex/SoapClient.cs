using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;

namespace Parcilaes.FileHex;

public enum TtSoapReturnCodes
{
    Ok = 1,
    NotDefined = 0,
    GeneralError = -1,
    WrongNumberParameters = -2,
    ErrorFileDoesNotExist = -3,
    ErrorPutFile = -4,
    ErrorGetFile = -5,
    ErrorPackFile = -6,
    ErrorUnpackFile = -7,
    ErrorNoConnection = -10
}

public delegate void StatusEventHandler(string status, int progress);

/// <summary>
/// Conversión de clsTTSOAPCLT (VB6). Cliente SOAP/HTTP contra las páginas ASP TTSoapServer.asp y
/// TTSOAPServerFileX.asp, con transferencia de archivos por segmentos. Mantiene el mismo formato de
/// envelope, cabeceras y parámetros. Las llamadas son síncronas, igual que el original.
/// La instancia guarda estado (StatusCode, Response, Data...), por lo que no es segura entre hilos.
/// </summary>
public class SoapClient : IDisposable
{
    private const string GenericPage = "TTSoapServer.asp";
    private const string FileXPage = "TTSOAPServerFileX.asp";

    private HttpClient? _http;
    private string _page = "";
    private int _resolveMs = 15_000, _connectMs = 15_000, _sendMs = 60_000, _receiveMs = 60_000;

    /// <summary>Se dispara con (mensaje, progreso 0-100). ("", 0) indica fin de operación.</summary>
    public event StatusEventHandler? Status;

    // Antes variables públicas (sWEBServer, sSOAPEnvelope, ...)
    public string WebServer { get; set; } = "";
    public string SoapEnvelope { get; set; } = "";
    public string SoapResponse { get; set; } = "";
    public TtSoapReturnCodes StatusCode { get; set; }
    public string Response { get; set; } = "";
    public string Data { get; set; } = "";
    public int SegmentSize { get; set; } = 10000;

    public bool Inicializar(string webServer,
                            int segmentSize = 50000,
                            int resolveTimeOutSec = 15,
                            int connectTimeOutSec = 15,
                            int sendTimeOutSec = 60,
                            int receiveTimeOutSec = 60)
    {
        WebServer = webServer;
        SegmentSize = segmentSize;
        _resolveMs = resolveTimeOutSec * 1000;
        _connectMs = connectTimeOutSec * 1000;
        _sendMs = sendTimeOutSec * 1000;
        _receiveMs = receiveTimeOutSec * 1000;

        _http?.Dispose();
        _http = null;
        return true;
    }

    public void Dispose() => _http?.Dispose();

    private HttpClient Http => _http ??= new HttpClient(
        new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(_connectMs) })
    {
        Timeout = TimeSpan.FromMilliseconds((long)_resolveMs + _connectMs + _sendMs + _receiveMs)
    };

    private void RaiseStatus(string status, int progress) => Status?.Invoke(status, progress);

    // ---------------------------------------------------------------------------------------
    // SOAP transport

    /// <summary>Envía <see cref="SoapEnvelope"/> a WebServer + página y procesa la respuesta.</summary>
    private TtSoapReturnCodes SoapCall()
    {
        StatusCode = TtSoapReturnCodes.NotDefined;
        Response = "";
        Data = "";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, WebServer + _page);
            request.Headers.TryAddWithoutValidation("Man", WebServer + _page + " HTTP/1.1");
            request.Headers.TryAddWithoutValidation("MessageType", "Call");
            request.Headers.TryAddWithoutValidation("ContentType", "text/xml");
            request.Content = new StringContent(SoapEnvelope, Encoding.UTF8, "text/xml");

            using var response = Http.Send(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                string text = ReadBody(response);
                SoapResponse = text;

                var doc = new XmlDocument();
                bool parsed = true;
                try { doc.LoadXml(text); }
                catch (XmlException) { parsed = false; }

                if (parsed && doc.DocumentElement is { } root)
                {
                    XmlNode statusNode = root.SelectSingleNode(".//status")!;
                    XmlNode responseNode = root.SelectSingleNode(".//response")!;
                    XmlNode dataNode = root.SelectSingleNode(".//data")!;

                    StatusCode = (TtSoapReturnCodes)int.Parse(statusNode.InnerText, CultureInfo.InvariantCulture);
                    Response = responseNode.InnerXml.Trim();
                    Data = dataNode.InnerText;
                }
                else
                {
                    StatusCode = TtSoapReturnCodes.GeneralError;
                }
            }
            else
            {
                StatusCode = TtSoapReturnCodes.GeneralError;
                Response = response.ReasonPhrase ?? "";
            }
        }
        catch (Exception ex)
        {
            StatusCode = TtSoapReturnCodes.ErrorNoConnection;
            Response += $"[{ex.HResult}-{ex.Message}-{ex.Source}]";
        }
        return StatusCode;
    }

    private static string ReadBody(HttpResponseMessage response)
    {
        Encoding encoding = Encoding.UTF8;
        string? charset = response.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrEmpty(charset))
        {
            try { encoding = Encoding.GetEncoding(charset.Trim('"')); } catch (ArgumentException) { }
        }
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Arma el envelope a partir de "nombre=valor&amp;nombre=valor". Los valores NO se escapan
    /// (igual que el original; los datos de archivo van en hex ASCII sin '&lt;' ni '&amp;').
    /// </summary>
    private bool BuildSoapEnvelope(string parameters)
    {
        var sb = new StringBuilder();
        sb.Append("<SOAP:Envelope xmlns:SOAP=\"urn:schemas-xmlsoap-org:soap.v1\">");
        sb.Append("<SOAP:Body>");

        if (parameters.Length > 0)
        {
            foreach (string p in parameters.Split('&'))
            {
                int eq = p.IndexOf('=');
                if (eq < 0) return false;
                sb.Append('<').Append(p, 0, eq).Append('>')
                  .Append(p, eq + 1, p.Length - eq - 1)
                  .Append("</").Append(p, 0, eq).Append('>');
            }
        }

        sb.Append("</SOAP:Body>");
        sb.Append("</SOAP:Envelope>");
        SoapEnvelope = sb.ToString();
        return true;
    }

    /// <summary>Llamada genérica a un método de un componente COM en el servidor.</summary>
    public TtSoapReturnCodes SoapGenericCall(string componentName, string methodName, string param = "")
    {
        _page = GenericPage;
        string parameters = $"Name={componentName}&Proc={methodName}&Param={param}";

        if (BuildSoapEnvelope(parameters)) return SoapCall();

        StatusCode = TtSoapReturnCodes.GeneralError;
        return StatusCode;
    }

    /// <summary>Pide la hora al servidor genérico.</summary>
    public TtSoapReturnCodes Test()
    {
        StatusCode = SoapGenericCall("TTSOAPServ.clsTTSOAPSRVTest", "GetTime",
            DateTime.Now.ToString("dd/MM/yy HH:mm.ss", CultureInfo.InvariantCulture));
        return StatusCode;
    }

    // ---------------------------------------------------------------------------------------
    // File operations

    private TtSoapReturnCodes SoapFileX(string method, string header, string data = "")
    {
        try
        {
            string parameters = $"Header={header}&Method={method}&Data={data}";
            StatusCode = BuildSoapEnvelope(parameters) ? SoapCall() : TtSoapReturnCodes.GeneralError;
        }
        catch
        {
            StatusCode = TtSoapReturnCodes.GeneralError;
        }
        return StatusCode;
    }

    private TtSoapReturnCodes FileXCommand(string method, string header, bool valid)
    {
        try
        {
            _page = FileXPage;
            if (!valid)
            {
                StatusCode = TtSoapReturnCodes.WrongNumberParameters;
                return StatusCode;
            }
            return SoapFileX(method, header);
        }
        catch
        {
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }

    /// <summary>Borra archivos (separados por el servidor) en <paramref name="path"/> bajo el directorio de trabajo.</summary>
    public TtSoapReturnCodes DelFile(string path = "", string files = "")
        => FileXCommand("DELFILE", path + "|" + files, files.Length > 0);

    public TtSoapReturnCodes DirFiles(string path = "", string files = "*.*")
        => FileXCommand("DIRFILES", path + "|" + files, true);

    public TtSoapReturnCodes RenameFile(string original, string newName)
        => FileXCommand("RENAMEFILE", original + "|" + newName, original.Length > 0 && newName.Length > 0);

    public TtSoapReturnCodes PackFile(string original, string pack)
        => FileXCommand("PACKFILE", original + "|" + pack, original.Length > 0 && pack.Length > 0);

    public TtSoapReturnCodes UnPackFile(string original, string unpack)
        => FileXCommand("UNPACKFILE", original + "|" + unpack, original.Length > 0 && unpack.Length > 0);

    /// <summary>Descarga un archivo del servidor por segmentos y valida su CRC32.</summary>
    public TtSoapReturnCodes GetFile(string serverFile, string localFile)
    {
        try
        {
            _page = FileXPage;

            if (serverFile.Length == 0 || localFile.Length == 0)
            {
                StatusCode = TtSoapReturnCodes.WrongNumberParameters;
                return StatusCode;
            }

            var fileHex = new FileHex();
            if (fileHex.FileExists(localFile)) fileHex.FileDelete(localFile);
            fileHex.FileName = localFile;

            // Paso 1: primer segmento (informa cuántos hay)
            RaiseStatus($"Obteniendo archivo {serverFile} [Cargando...]", 0);
            var result = SoapFileX("GETFILE", $"{serverFile}|1|0|{SegmentSize}");
            if (result != TtSoapReturnCodes.Ok)
            {
                StatusCode = result;
                RaiseStatus("", 0);
                return StatusCode;
            }

            int totalSegments = int.Parse(Response[..Response.IndexOf('|')], CultureInfo.InvariantCulture);
            if (!fileHex.SaveStringToFile(Data, 1, totalSegments))
                return FailGet(null, "No pudo grabar el archivo local");
            ReportProgress("Obteniendo", serverFile, 1, totalSegments, "##0");

            // Paso 2: resto de segmentos
            for (int segment = 2; segment <= totalSegments; segment++)
            {
                result = SoapFileX("GETFILE", $"{serverFile}|{segment}|{totalSegments}|{SegmentSize}");
                if (result != TtSoapReturnCodes.Ok)
                {
                    fileHex.FileDelete(localFile + ".tmp");
                    StatusCode = result;
                    RaiseStatus("", 0);
                    return StatusCode;
                }

                if (!fileHex.SaveStringToFile(Data, segment, totalSegments))
                {
                    fileHex.FileDelete(localFile + ".tmp");
                    return FailGet(null, "No pudo grabar el archivo local");
                }
                ReportProgress("Obteniendo", serverFile, segment, totalSegments, "##0.0####");
            }

            RaiseStatus($"Obteniendo archivo {serverFile} [Validando...]", 0);

            // Paso 3: CRC32 (el servidor lo envía como Long con signo tras el '|')
            long serverCrc = long.Parse(Response[(Response.IndexOf('|') + 1)..], CultureInfo.InvariantCulture);
            uint expected = unchecked((uint)serverCrc);
            uint actual = new Crc32().AddFileCrc32(localFile);
            if (expected != actual)
            {
                fileHex.FileDelete(localFile);
                return FailGet(null, "La copia local y el archivo remoto no son idénticos");
            }

            RaiseStatus($"Obteniendo archivo {serverFile} Completado.", 100);
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.Ok;
            return StatusCode;
        }
        catch
        {
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }

    private TtSoapReturnCodes FailGet(string? _, string message)
    {
        StatusCode = TtSoapReturnCodes.ErrorGetFile;
        Response = message;
        RaiseStatus("", 0);
        return StatusCode;
    }

    private void ReportProgress(string verb, string file, int segment, int total, string format)
    {
        double pct = (double)segment / total * 100;
        RaiseStatus($"{verb} archivo {file} [{pct.ToString(format, CultureInfo.CurrentCulture)}%]",
            (int)Math.Round(pct));
    }

    /// <summary>Envía un archivo local al servidor por segmentos (con su CRC32).</summary>
    public TtSoapReturnCodes PutFile(string originalFile, string destinationFile, string tempFile)
    {
        try
        {
            _page = FileXPage;

            if (originalFile.Length == 0 || destinationFile.Length == 0 || tempFile.Length == 0)
            {
                StatusCode = TtSoapReturnCodes.WrongNumberParameters;
                return StatusCode;
            }

            var fileHex = new FileHex();
            if (!fileHex.FileExists(originalFile))
            {
                StatusCode = TtSoapReturnCodes.ErrorFileDoesNotExist;
                return StatusCode;
            }

            RaiseStatus($"Enviando archivo {originalFile} [Cargando...]", 0);
            fileHex.FileName = originalFile;

            // El servidor espera el CRC como Long con signo
            int crc = unchecked((int)new Crc32().AddFileCrc32(originalFile));

            int totalSegments = 0;
            string chunk = fileHex.LoadFileToString2(1, ref totalSegments, SegmentSize);
            for (int segment = 1; segment <= totalSegments; segment++)
            {
                var result = SoapFileX("PUTFILE",
                    $"{destinationFile}|{tempFile}|{segment}|{totalSegments}|{SegmentSize}|{crc}",
                    chunk);

                if (result != TtSoapReturnCodes.Ok)
                {
                    StatusCode = TtSoapReturnCodes.ErrorPutFile;
                    Response = "No se pudo enviar el archivo";
                    RaiseStatus("", 0);
                    return StatusCode;
                }

                ReportProgress("Enviando", originalFile, segment, totalSegments, "##0");

                if (segment < totalSegments)
                    chunk = fileHex.LoadFileToString2(segment + 1, ref totalSegments, SegmentSize);
            }

            RaiseStatus($"Enviando archivo {originalFile} Completado.", 100);
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.Ok;
            return StatusCode;
        }
        catch
        {
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }

    /// <summary>Descarga el resultado de la página <paramref name="xmlPage"/> (sin parámetros).</summary>
    public TtSoapReturnCodes GetXml(string xmlPage)
    {
        try
        {
            RaiseStatus($"Obteniendo archivo {xmlPage}...", 0);
            _page = xmlPage;

            // Nota: el VB6 original fallaba siempre aquí (Split("") no tiene '='); ahora envía un body vacío.
            var result = BuildSoapEnvelope("") ? SoapCall() : TtSoapReturnCodes.GeneralError;
            StatusCode = result;

            RaiseStatus("", 0);
            return result;
        }
        catch
        {
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Binary files (pack + transfer)

    private static string TempName(string serverFileName) =>
        $"{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}_{serverFileName}_" +
        $"{Random.Shared.Next(0, 10000).ToString("00000", CultureInfo.InvariantCulture)}.tmp";

    /// <summary>Empaqueta el archivo en el servidor, lo descarga y lo desempaqueta localmente.</summary>
    public TtSoapReturnCodes GetBinFile(string serverPath, string serverFileName, string localFile)
    {
        try
        {
            string tempName = TempName(serverFileName);

            StatusCode = PackFile(serverPath + serverFileName, "SOAPTemp\\" + tempName);
            RaiseStatus("Preparando archivo remoto....", 0);
            if (StatusCode != TtSoapReturnCodes.Ok)
            {
                RaiseStatus("", 0);
                return StatusCode;
            }

            var getStatus = GetFile("SOAPTemp\\" + tempName, tempName);
            RaiseStatus("Archivo remoto obtenido....", 33);
            DelFile("", "SOAPTemp\\" + tempName);
            if (getStatus != TtSoapReturnCodes.Ok)
            {
                StatusCode = getStatus;
                RaiseStatus("", 0);
                return getStatus;
            }

            var fileHex = new FileHex { FileName = tempName };
            RaiseStatus("Desempacando archivo obtenido....", 66);

            if (fileHex.FileHexToAsci(localFile))
            {
                fileHex.FileDelete(tempName);
                RaiseStatus("Archivo local OK....", 100);
                StatusCode = TtSoapReturnCodes.Ok;
            }
            else
            {
                StatusCode = TtSoapReturnCodes.ErrorGetFile;
                RaiseStatus("No pudo desempacar archivo local", 0);
            }

            RaiseStatus("", 0);
            return StatusCode;
        }
        catch
        {
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }

    /// <summary>Empaqueta el archivo local, lo envía y lo desempaqueta en el servidor.</summary>
    public TtSoapReturnCodes PutBinFile(string localFile, string serverPath, string serverFileName)
    {
        string tempName = TempName(serverFileName);
        var fileHex = new FileHex();
        try
        {
            fileHex.FileName = localFile;
            RaiseStatus("Empacando archivo local....", 0);

            if (!fileHex.FileAsciToHex(tempName))
            {
                StatusCode = TtSoapReturnCodes.ErrorPutFile;
                RaiseStatus("No pudo empacar archivo local", 0);
                RaiseStatus("", 0);
                return StatusCode;
            }
            RaiseStatus("Archivo local preparado....", 33);

            RaiseStatus("Enviando archivo....", 66);
            var putStatus = PutFile(tempName, "SOAPTemp\\" + tempName, "SOAPTemp\\" + tempName + "_TMP");
            RaiseStatus("Archivo enviado....desempacando...", 80);

            if (putStatus == TtSoapReturnCodes.Ok)
            {
                fileHex.FileDelete(tempName);
                putStatus = UnPackFile("SOAPTemp\\" + tempName, serverPath + "\\" + serverFileName);
                RaiseStatus("Archivo remoto desempacado....", 90);
                DelFile("", "SOAPTemp\\" + tempName);
                RaiseStatus("Archivo copiado....", 100);
            }
            else
            {
                StatusCode = TtSoapReturnCodes.ErrorPutFile;
                RaiseStatus("No pudo enviar archivo local", 0);
            }

            RaiseStatus("", 0);
            return putStatus;
        }
        catch
        {
            RaiseStatus("", 0);
            StatusCode = TtSoapReturnCodes.GeneralError;
            return StatusCode;
        }
    }
}
