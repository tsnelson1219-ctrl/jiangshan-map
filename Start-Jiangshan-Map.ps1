$ErrorActionPreference = 'Stop'
$listener = $null
$logPath = Join-Path $env:LOCALAPPDATA 'JiangshanAtlas\launcher.log'

function Send-TextResponse {
    param(
        [System.IO.Stream]$Stream,
        [string]$Status,
        [string]$Text,
        [string]$Method
    )
    $nl = [string]([char]13) + [char]10
    $body = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $head = "HTTP/1.1 $Status" + $nl +
        "Content-Type: text/plain; charset=utf-8" + $nl +
        "Content-Length: $($body.Length)" + $nl +
        "Connection: close" + $nl + $nl
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($head)
    $Stream.Write($bytes, 0, $bytes.Length)
    if ($Method -ne 'HEAD') { $Stream.Write($body, 0, $body.Length) }
}

function Handle-Client {
    param(
        [System.Net.Sockets.TcpClient]$Client,
        [int]$Port,
        [string]$Root,
        [hashtable]$Mime
    )
    try {
        $stream = $Client.GetStream()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 4096, $true)
        $requestLine = $reader.ReadLine()
        if (-not $requestLine) { return }

        $headers = @{}
        while ($true) {
            $headerLine = $reader.ReadLine()
            if ([string]::IsNullOrEmpty($headerLine)) { break }
            $separator = $headerLine.IndexOf(':')
            if ($separator -gt 0) {
                $key = $headerLine.Substring(0, $separator).Trim().ToLowerInvariant()
                $headers[$key] = $headerLine.Substring($separator + 1).Trim()
            }
        }
        $reader.Dispose()

        $parts = $requestLine.Split(' ')
        if ($parts.Length -lt 2) { return }
        $method = $parts[0].ToUpperInvariant()
        $requestTarget = $parts[1]
        if ($method -ne 'GET' -and $method -ne 'HEAD') {
            Send-TextResponse -Stream $stream -Status '405 Method Not Allowed' -Text 'Method Not Allowed' -Method $method
            return
        }

        $requestUri = [System.Uri]::new("http://127.0.0.1:$Port$requestTarget")
        $relativePath = [System.Uri]::UnescapeDataString($requestUri.AbsolutePath.TrimStart('/'))
        if ([string]::IsNullOrWhiteSpace($relativePath)) { $relativePath = 'index.html' }
        $relativePath = $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)

        # Do not expose repository metadata or allow paths outside the application folder.
        $filePath = ''
        if ($relativePath -notmatch '(^|[\\/])\.git([\\/]|$)' -and $relativePath -notmatch '(^|[\\/])\.github([\\/]|$)') {
            $candidatePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $relativePath))
            $rootPrefix = $Root.TrimEnd('\') + '\'
            if ($candidatePath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                $candidatePath.Equals($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
                $filePath = $candidatePath
            }
        }

        if ($filePath -and (Test-Path -LiteralPath $filePath -PathType Container)) {
            $filePath = Join-Path $filePath 'index.html'
        }
        if (-not $filePath -or -not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            Send-TextResponse -Stream $stream -Status '404 Not Found' -Text '404 Not Found' -Method $method
            return
        }

        $fileLength = [int64]([System.IO.FileInfo]::new($filePath).Length)
        $start = [int64]0
        $end = [int64]($fileLength - 1)
        $status = '200 OK'

        # Support byte ranges for audio and other media resources.
        if ($headers.ContainsKey('range') -and $fileLength -gt 0 -and $headers['range'] -match '^bytes=(\d*)-(\d*)$') {
            $rangeStart = $Matches[1]
            $rangeEnd = $Matches[2]
            if ([string]::IsNullOrEmpty($rangeStart)) {
                $suffixLength = [int64]$rangeEnd
                if ($suffixLength -gt 0) { $start = [Math]::Max(0, $fileLength - $suffixLength) }
            }
            else {
                $start = [int64]$rangeStart
                if (-not [string]::IsNullOrEmpty($rangeEnd)) { $end = [int64]$rangeEnd }
            }
            if ($start -ge $fileLength -or $end -lt $start) {
                $nl = [string]([char]13) + [char]10
                $head = 'HTTP/1.1 416 Range Not Satisfiable' + $nl +
                    "Content-Range: bytes */$fileLength" + $nl +
                    'Content-Length: 0' + $nl + 'Connection: close' + $nl + $nl
                $bytes = [System.Text.Encoding]::ASCII.GetBytes($head)
                $stream.Write($bytes, 0, $bytes.Length)
                return
            }
            $end = [Math]::Min($end, $fileLength - 1)
            $status = '206 Partial Content'
        }

        $contentLength = [int64]([Math]::Max(0, $end - $start + 1))
        $extension = [System.IO.Path]::GetExtension($filePath).ToLowerInvariant()
        $contentType = 'application/octet-stream'
        if ($Mime.ContainsKey($extension)) { $contentType = $Mime[$extension] }

        $nl = [string]([char]13) + [char]10
        $responseHead = "HTTP/1.1 $status" + $nl +
            "Content-Type: $contentType" + $nl +
            "Content-Length: $contentLength" + $nl +
            'Accept-Ranges: bytes' + $nl +
            'Cache-Control: no-cache' + $nl +
            'Connection: close' + $nl
        if ($status.StartsWith('206')) { $responseHead += "Content-Range: bytes $start-$end/$fileLength" + $nl }
        $responseHead += $nl
        $bytes = [System.Text.Encoding]::ASCII.GetBytes($responseHead)
        $stream.Write($bytes, 0, $bytes.Length)

        if ($method -eq 'GET' -and $contentLength -gt 0) {
            $fileStream = [System.IO.File]::OpenRead($filePath)
            try {
                [void]$fileStream.Seek($start, [System.IO.SeekOrigin]::Begin)
                $buffer = New-Object byte[] 65536
                $remaining = $contentLength
                while ($remaining -gt 0) {
                    $readSize = [int][Math]::Min($buffer.Length, $remaining)
                    $count = $fileStream.Read($buffer, 0, $readSize)
                    if ($count -le 0) { break }
                    $stream.Write($buffer, 0, $count)
                    $remaining -= $count
                }
            }
            finally { $fileStream.Dispose() }
        }
    }
    catch {
        # A browser tab may close a connection while its resource is loading.
    }
    finally { $Client.Close() }
}

try {
    $root = [System.IO.Path]::GetFullPath($PSScriptRoot)
    $stateDir = Join-Path $env:LOCALAPPDATA 'JiangshanAtlas'
    $profileDir = Join-Path $stateDir 'ChromeProfile'
    New-Item -ItemType Directory -Force -Path $stateDir, $profileDir | Out-Null

    $chromeCandidates = @(
        (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
        (Join-Path $env:SystemDrive 'Program Files (x86)\Google\Chrome\Application\chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    $chromePath = $chromeCandidates | Select-Object -First 1
    if (-not $chromePath) {
        throw '找不到 Google Chrome。請先安裝 Chrome，再重新執行 Start-Jiangshan-Map.bat。'
    }

    $port = 0
    for ($candidate = 8765; $candidate -le 8805; $candidate++) {
        $probe = $null
        try {
            $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidate)
            $probe.Start()
            $port = $candidate
            break
        }
        catch {
            if ($probe) { try { $probe.Stop() } catch {} }
        }
        finally {
            if ($probe) { try { $probe.Stop() } catch {} }
        }
    }
    if ($port -eq 0) { throw '找不到可用的本機連接埠（8765–8805）。' }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
    $listener.Start()
    $url = "http://127.0.0.1:$port/?lang=zh-tw"
    $profileArg = '--user-data-dir="' + $profileDir + '"'
    $browser = Start-Process -FilePath $chromePath -ArgumentList @(
        '--no-first-run', '--disable-background-mode', $profileArg, "--app=$url"
    ) -PassThru

    $mime = @{
        '.html' = 'text/html; charset=utf-8'; '.htm' = 'text/html; charset=utf-8'
        '.js' = 'application/javascript; charset=utf-8'; '.mjs' = 'application/javascript; charset=utf-8'
        '.css' = 'text/css; charset=utf-8'; '.json' = 'application/json; charset=utf-8'
        '.geojson' = 'application/geo+json; charset=utf-8'; '.txt' = 'text/plain; charset=utf-8'
        '.xml' = 'application/xml; charset=utf-8'; '.svg' = 'image/svg+xml'
        '.png' = 'image/png'; '.jpg' = 'image/jpeg'; '.jpeg' = 'image/jpeg'
        '.webp' = 'image/webp'; '.gif' = 'image/gif'; '.ico' = 'image/x-icon'
        '.mp3' = 'audio/mpeg'; '.ogg' = 'audio/ogg'; '.wav' = 'audio/wav'; '.m4a' = 'audio/mp4'
        '.woff' = 'font/woff'; '.woff2' = 'font/woff2'; '.ttf' = 'font/ttf'; '.otf' = 'font/otf'
        '.wasm' = 'application/wasm'; '.pdf' = 'application/pdf'
    }

    $acceptTask = $listener.AcceptTcpClientAsync()
    while (-not $browser.HasExited) {
        if ($acceptTask.Wait(300)) {
            $client = $acceptTask.Result
            Handle-Client -Client $client -Port $port -Root $root -Mime $mime
            if (-not $browser.HasExited) { $acceptTask = $listener.AcceptTcpClientAsync() }
        }
    }
}
catch {
    try {
        New-Item -ItemType Directory -Force -Path (Split-Path $logPath) | Out-Null
        $_ | Out-String | Set-Content -LiteralPath $logPath -Encoding UTF8
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show(
            $_.Exception.Message, '江山時序啟動失敗',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
    }
    catch {}
}
finally {
    if ($listener) { try { $listener.Stop() } catch {} }
}
