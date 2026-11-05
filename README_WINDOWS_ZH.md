# 江山時序 Windows Portable（Chrome 版）

本分支提供真正的 Windows x64 啟動程式，不需要使用者操作 BAT、PowerShell、Python、Node.js 或 Docker。

## 使用方式

1. 到 GitHub Actions 的 **Build Windows Chrome Portable** 工作流程。
2. 開啟最新一次成功的執行紀錄，在 Artifacts 下載 `JiangshanAtlas-Windows-x64`。
3. 解壓縮 `JiangshanAtlas_Portable_Windows_x64.zip`。
4. 雙擊資料夾中的 `JiangshanAtlas.exe`。
5. 程式會自動啟動本機地圖服務，並以 Chrome 獨立應用程式視窗開啟繁體中文版。
6. 關閉江山時序 Chrome 視窗後，本機服務會自動結束。

## 封裝內容

- `JiangshanAtlas.exe`：Windows x64 GUI 啟動器，沒有命令提示字元視窗。
- 地圖靜態檔案：與原始專案分開打包，解壓縮後可離線使用。
- Chrome 使用獨立 Profile，避免干擾日常 Chrome 的登入、分頁及設定。

## 系統需求

- Windows 10 / 11 x64
- 已安裝 Google Chrome
- Windows .NET Framework 4.8（Windows 10/11 通常已包含）

## 啟動器行為與安全

- 本機 HTTP 服務只監聽 `127.0.0.1`，不會開放給區域網路或外部網路。
- 由系統分配可用的隨機連接埠，不與其他本機服務搶固定連接埠。
- 支援常見網頁、圖片、音訊與字型 MIME type，以及媒體檔案的 Range 請求。
- 關閉專用 Chrome 視窗後，啟動器會停止本機服務。
- 啟動錯誤會以 Windows 對話框顯示，詳細紀錄存放於 `%LOCALAPPDATA%\JiangshanAtlas\launcher.log`。
- 啟動器原始碼位於 `launcher/JiangshanLauncher.cs`，由 GitHub Actions 在 Windows runner 編譯。

## 授權提醒

專案主體採 GPL-3.0。郡、州、府級歷史邊界資料另有 CHGIS V6 與 Hartwell 原始授權限制，部分資料僅供學術、非商業使用。請一併閱讀專案的 Credits & License 與資料來源說明。
