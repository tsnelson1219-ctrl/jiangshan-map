# Windows 一鍵啟動（Chrome 版）

## 使用方式

1. 下載本專案 ZIP 並完整解壓縮。
2. 確認 Windows 已安裝 Google Chrome。
3. 雙擊資料夾內的 `Start-Jiangshan-Map.bat`。啟動器會保留命令視窗顯示狀態與錯誤訊息。
4. 程式會在背景啟動本機靜態檔案服務，並以 Chrome 獨立應用程式視窗開啟繁體中文地圖。
5. 關閉該 Chrome 地圖視窗後，本機服務會自動結束，啟動器命令視窗也會隨之關閉。

不需要另外安裝 Python、Node.js、Docker 或 npm 套件。地圖檔案保留在解壓縮資料夾中，瀏覽器設定檔存放於使用者的 `%LOCALAPPDATA%\JiangshanAtlas\ChromeProfile`。

## 安全與限制

- HTTP 服務只監聽 `127.0.0.1`，不對區域網路或外部網路開放。
- 啟動器會在 8765–8805 之間尋找可用連接埠。
- 使用獨立 Chrome profile，避免影響一般 Chrome 的分頁、登入與設定。
- 目前需安裝 Google Chrome；若 Chrome 安裝在非標準路徑，請調整 `Start-Jiangshan-Map.ps1` 中的搜尋路徑。
- 啟動時會保留 PowerShell 命令視窗；若 Chrome 未出現，先查看該視窗顯示的錯誤。
- 若啟動失敗，錯誤紀錄位於 `%LOCALAPPDATA%\JiangshanAtlas\launcher.log`。
- 這是本機網頁啟動器，不會把網站轉成單一 EXE；請勿刪除或移動解壓縮資料夾中的地圖檔案。

## 檔案

- `Start-Jiangshan-Map.bat`：雙擊啟動入口。
- `Start-Jiangshan-Map.ps1`：本機 HTTP 服務與 Chrome 啟動邏輯。

## 授權提醒

本專案主體採 GPL-3.0。部分郡、州、府級歷史邊界資料另受 CHGIS V6 與 Hartwell 原始條款限制，僅供學術、非商業使用；請閱讀專案的 Credits & License 與資料來源說明。
