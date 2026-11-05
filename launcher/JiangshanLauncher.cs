using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Management;
using System.Windows.Forms;

namespace JiangshanAtlasLauncher
{
    internal static class Program
    {
        private static string _logFile;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LocalFileServer server = null;
            Process chromeProcess = null;

            try
            {
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string indexFile = Path.Combine(root, "index.html");
                if (!File.Exists(indexFile))
                    throw new FileNotFoundException("找不到 index.html。請確認你已完整解壓縮江山時序 Portable 資料夾。", indexFile);

                string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Path.Combine(localData, "JiangshanAtlas");
                string profile = Path.Combine(appData, "ChromeProfile");
                Directory.CreateDirectory(appData);
                Directory.CreateDirectory(profile);
                _logFile = Path.Combine(appData, "launcher.log");

                string chrome = FindChrome();
                if (String.IsNullOrEmpty(chrome) || !File.Exists(chrome))
                    throw new FileNotFoundException("找不到 Google Chrome。請先安裝 Chrome，再重新啟動。");

                server = new LocalFileServer(root);
                server.Start();

                string url = server.BaseUrl + "?lang=zh-tw";
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = chrome;
                start.Arguments = "--no-first-run --disable-background-mode --new-window " +
                    "--user-data-dir=\"" + profile + "\" --app=\"" + url + "\"";
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                chromeProcess = Process.Start(start);

                if (chromeProcess == null)
                    throw new InvalidOperationException("Windows 無法啟動 Google Chrome。");

                bool detected = WaitForChromeProfile(profile, chromeProcess, 30000);
                if (!detected)
                    throw new InvalidOperationException("Chrome 沒有成功建立江山時序視窗。請確認 Chrome 可正常執行。");

                // Keep the local server alive until this dedicated Chrome profile closes.
                while (IsChromeProfileRunning(profile, chromeProcess))
                    Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show(
                    ex.Message + (_logFile == null ? "" : "\r\n\r\n錯誤紀錄：" + _logFile),
                    "江山時序啟動失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (server != null)
                    server.Stop();
                if (chromeProcess != null)
                    chromeProcess.Dispose();
            }
        }

        private static string FindChrome()
        {
            List<string> candidates = new List<string>();

            AddRegistryChrome(candidates, RegistryHive.CurrentUser, RegistryView.Registry64);
            AddRegistryChrome(candidates, RegistryHive.LocalMachine, RegistryView.Registry64);
            AddRegistryChrome(candidates, RegistryHive.LocalMachine, RegistryView.Registry32);

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            candidates.Add(Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"));
            candidates.Add(Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"));
            candidates.Add(Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"));

            foreach (string path in candidates)
            {
                if (!String.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            return null;
        }

        private static void AddRegistryChrome(List<string> candidates, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(null);
                        if (value != null)
                            candidates.Add(Convert.ToString(value));
                    }
                }
            }
            catch { }
        }

        private static bool WaitForChromeProfile(string profile, Process initialProcess, int timeoutMs)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (IsChromeProfileRunning(profile, initialProcess))
                    return true;

                try
                {
                    if (initialProcess != null && initialProcess.HasExited)
                    {
                        // Chrome can hand off the launch request. Continue looking for the
                        // browser process using its unique profile before declaring failure.
                    }
                }
                catch { }

                Thread.Sleep(400);
            }
            return false;
        }

        private static bool IsChromeProfileRunning(string profile, Process initialProcess)
        {
            try
            {
                string needle = profile.Replace('/', '\\');
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, CommandLine FROM Win32_Process WHERE Name='chrome.exe'"))
                using (ManagementObjectCollection processes = searcher.Get())
                {
                    foreach (ManagementObject item in processes)
                    {
                        string commandLine = Convert.ToString(item["CommandLine"]);
                        if (!String.IsNullOrEmpty(commandLine) &&
                            commandLine.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            commandLine.IndexOf("--type=", StringComparison.OrdinalIgnoreCase) < 0)
                            return true;
                    }
                }
                return false;
            }
            catch
            {
                // WMI may be restricted by enterprise policy. The dedicated Chrome
                // profile normally keeps the initial browser process alive.
                try { return initialProcess != null && !initialProcess.HasExited; }
                catch { return false; }
            }
        }

        private static void Log(string message)
        {
            try
            {
                if (String.IsNullOrEmpty(_logFile))
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "JiangshanAtlas");
                    Directory.CreateDirectory(folder);
                    _logFile = Path.Combine(folder, "launcher.log");
                }
                File.AppendAllText(_logFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    message + Environment.NewLine + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }
    }

    internal sealed class LocalFileServer
    {
        private readonly string _root;
        private readonly string _rootPrefix;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private int _port;

        private static readonly Dictionary<string, string> MimeTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".html", "text/html; charset=utf-8" },
                { ".htm", "text/html; charset=utf-8" },
                { ".js", "application/javascript; charset=utf-8" },
                { ".mjs", "application/javascript; charset=utf-8" },
                { ".css", "text/css; charset=utf-8" },
                { ".json", "application/json; charset=utf-8" },
                { ".geojson", "application/geo+json; charset=utf-8" },
                { ".txt", "text/plain; charset=utf-8" },
                { ".xml", "application/xml; charset=utf-8" },
                { ".svg", "image/svg+xml" },
                { ".png", "image/png" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".webp", "image/webp" },
                { ".gif", "image/gif" },
                { ".ico", "image/x-icon" },
                { ".mp3", "audio/mpeg" },
                { ".ogg", "audio/ogg" },
                { ".wav", "audio/wav" },
                { ".m4a", "audio/mp4" },
                { ".woff", "font/woff" },
                { ".woff2", "font/woff2" },
                { ".ttf", "font/ttf" },
                { ".otf", "font/otf" },
                { ".wasm", "application/wasm" },
                { ".pdf", "application/pdf" }
            };

        public string BaseUrl
        {
            get { return "http://127.0.0.1:" + _port + "/"; }
        }

        public LocalFileServer(string root)
        {
            _root = Path.GetFullPath(root);
            _rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); }
            catch { }
            try
            {
                if (_acceptThread != null && _acceptThread.IsAlive)
                    _acceptThread.Join(1500);
            }
            catch { }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        HandleClient((TcpClient)state);
                    }, client);
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000;
                    StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
                    string requestLine = reader.ReadLine();
                    if (String.IsNullOrEmpty(requestLine)) return;

                    Dictionary<string, string> headers =
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string line;
                    while (!String.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        int split = line.IndexOf(':');
                        if (split > 0)
                            headers[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
                    }
                    reader.Dispose();

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0].ToUpperInvariant();
                    if (method != "GET" && method != "HEAD")
                    {
                        SendText(stream, "405 Method Not Allowed", "Method Not Allowed", method);
                        return;
                    }

                    Uri uri;
                    if (!Uri.TryCreate(new Uri(BaseUrl), parts[1], out uri))
                    {
                        SendText(stream, "400 Bad Request", "Bad Request", method);
                        return;
                    }

                    string relative = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
                        .Replace('/', Path.DirectorySeparatorChar);
                    if (String.IsNullOrEmpty(relative)) relative = "index.html";

                    string filePath = null;
                    if (relative.IndexOf(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0 &&
                        relative.IndexOf(".github" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        string candidate = Path.GetFullPath(Path.Combine(_root, relative));
                        if (candidate.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(candidate, _root, StringComparison.OrdinalIgnoreCase))
                            filePath = candidate;
                    }

                    if (filePath != null && Directory.Exists(filePath))
                        filePath = Path.Combine(filePath, "index.html");

                    if (String.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        SendText(stream, "404 Not Found", "404 Not Found", method);
                        return;
                    }

                    using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long length = file.Length;
                        long start = 0;
                        long end = length - 1;
                        string status = "200 OK";

                        string range;
                        if (length > 0 && headers.TryGetValue("Range", out range))
                        {
                            long rangeStart;
                            long rangeEnd;
                            if (TryParseRange(range, length, out rangeStart, out rangeEnd))
                            {
                                start = rangeStart;
                                end = rangeEnd;
                                status = "206 Partial Content";
                            }
                        }

                        long count = Math.Max(0, end - start + 1);
                        string extension = Path.GetExtension(filePath);
                        string mime;
                        if (!MimeTypes.TryGetValue(extension, out mime))
                            mime = "application/octet-stream";

                        StringBuilder response = new StringBuilder();
                        response.Append("HTTP/1.1 ").Append(status).Append("\r\n");
                        response.Append("Content-Type: ").Append(mime).Append("\r\n");
                        response.Append("Content-Length: ").Append(count).Append("\r\n");
                        response.Append("Accept-Ranges: bytes\r\n");
                        response.Append("Cache-Control: no-cache\r\n");
                        response.Append("Connection: close\r\n");
                        if (status.StartsWith("206"))
                            response.Append("Content-Range: bytes ").Append(start).Append("-").Append(end)
                                .Append("/").Append(length).Append("\r\n");
                        response.Append("\r\n");

                        byte[] responseHeader = Encoding.ASCII.GetBytes(response.ToString());
                        stream.Write(responseHeader, 0, responseHeader.Length);

                        if (method == "GET" && count > 0)
                        {
                            file.Position = start;
                            byte[] buffer = new byte[65536];
                            long remaining = count;
                            while (remaining > 0)
                            {
                                int read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                                if (read <= 0) break;
                                stream.Write(buffer, 0, read);
                                remaining -= read;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Browsers can cancel resource requests during navigation or shutdown.
                try { client.Close(); } catch { }
            }
        }

        private static bool TryParseRange(string header, long length, out long start, out long end)
        {
            start = 0;
            end = length - 1;
            if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                return false;

            string value = header.Substring(6).Trim();
            int dash = value.IndexOf('-');
            if (dash < 0) return false;

            string left = value.Substring(0, dash);
            string right = value.Substring(dash + 1);
            long parsed;

            if (left.Length == 0)
            {
                if (!Int64.TryParse(right, out parsed) || parsed <= 0) return false;
                start = Math.Max(0, length - parsed);
            }
            else
            {
                if (!Int64.TryParse(left, out start) || start < 0 || start >= length) return false;
                if (right.Length > 0)
                {
                    if (!Int64.TryParse(right, out end) || end < start) return false;
                }
            }

            end = Math.Min(end, length - 1);
            return true;
        }

        private static void SendText(NetworkStream stream, string status, string text, string method)
        {
            byte[] body = Encoding.UTF8.GetBytes(text);
            string header = "HTTP/1.1 " + status + "\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                "Content-Length: " + body.Length + "\r\n" +
                "Connection: close\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(header);
            stream.Write(head, 0, head.Length);
            if (method != "HEAD")
                stream.Write(body, 0, body.Length);
        }
    }
}
