# Game Translate

[![CI](https://github.com/DogSay/game-translate/actions/workflows/ci.yml/badge.svg)](https://github.com/DogSay/game-translate/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Game Translate 是一款 Windows 遊戲簡繁轉換工具，將遊戲**已存在的簡體中文譯文**轉換為繁體中文，並以可停用的外置修補檔安裝。工具不會改寫遊戲原有的封裝檔案。

Game Translate converts existing Simplified Chinese game text into Traditional Chinese and creates reversible patches for compatible Unreal Engine games. It does not translate from English or other languages. Unity conversion is not supported yet.

**重要限制：**本工具不是自動翻譯器。若遊戲沒有現成的簡體中文內容，工具不能將英文、日文或其他語言自行翻譯成繁體中文，也不能補寫原本不存在的譯文。

> **目前僅公開原始碼，尚未提供可下載的 Windows 執行檔。** 開發者可參閱[從原始碼建置](docs/BUILDING.md)；公開發佈進度見[開發路線圖](docs/ROADMAP.md)。

## 主要功能

- 辨識遊戲的 Unreal Engine 或 Unity 封裝結構，並提示已知的支援限制。
- 對符合條件的 Unreal Engine 遊戲，讀取現有的簡體中文在地化文字，透過[繁化姬](https://zhconvert.org/)轉換為繁體中文，建立獨立修補檔。
- 在轉換過程中保留文字標記、變數佔位符及在地化資料結構，並於安裝前驗證修補檔。
- 顯示偵測結果、API 連線狀態及操作紀錄；可停用、重新啟用或移除由 Game Translate 建立的修補檔。
- 可清除目前遊戲的可重建快取，同時保留翻譯記憶、安裝記錄及已安裝的修補檔。

圖片文字、字型問題，以及未使用受支援在地化格式的內容，可能需要額外處理。

## 支援狀態

| 遊戲或格式 | 目前狀態 |
| --- | --- |
| Unreal Engine 的 `.locres` 在地化內容 | 已實作外置修補檔流程；仍須確認個別遊戲的封裝格式與載入方式。 |
| 《The Mound: Omen of Cthulhu》 | 兼容模式的技術配方曾通過遊戲內驗證；目前程式版本仍待重新進行遊戲內檢查。[查看遊戲說明](games/the-mound/README.md)。 |
| Unity 遊戲 | 可辨識部分遊戲結構；尚未支援簡繁轉換或安裝修補檔。 |

偵測到 Unreal Engine 或 Unity，並不代表該遊戲必定可以轉換。加密封裝、無法讀取的原始資源，或不同的在地化格式，均可能需要專用的遊戲適配器。

## 使用方式

目前沒有公開發佈的執行檔。以下說明適用於已依[建置指引](docs/BUILDING.md)取得本機版本的使用者：

1. 將 `GameTranslate.exe` 放在遊戲安裝資料夾，開啟程式並確認偵測結果。
2. 測試繁化姬 API 連線，檢視偵測結果及可選的簡繁轉換模式。
3. 選擇簡繁轉換方法並建立修補檔。工具會在安裝前檢查輸出；遇到不支援的格式時，不應繼續安裝。
4. 進入遊戲確認文字、字型及版面。日後可在工具中停用、重新啟用或移除繁體修補檔。

對於沒有獨立繁體中文語系的遊戲，**兼容模式**會使用原本的簡體中文語系位置載入繁體內容。遊戲內語言選單的顯示名稱因遊戲而異；部分遊戲仍須選擇「簡體中文」才能使用修補檔。這不等同於新增原生繁體中文語系。

外置修補檔不會修改原始遊戲封裝；安裝或停用修補檔時，工具可能同步調整玩家的語言設定。遊戲更新後，原有修補檔亦可能需要重新產生。自動驗證無法取代實際的遊戲內檢查。

可攜版不會在遊戲根目錄新建 `.game-translate` 資料夾。執行資料統一存放於 `%LOCALAPPDATA%\GameTranslate\`：

| 位置 | 用途 |
| --- | --- |
| `shared\tools\<版本>\` | 所有遊戲共用的工具快取；首次使用新版時會搬入可用的舊版工具。 |
| `games\<路徑雜湊>\` | 每隻遊戲獨立的工作檔、輸出、翻譯記憶及 `install-state.json`。路徑雜湊可區分同名遊戲的不同安裝位置。 |
| `session-logs\<路徑雜湊>\` | 每隻遊戲獨立的操作紀錄。 |

EXE 的「清除快取」按鈕只清除目前遊戲可重建的工作檔、輸出、舊版工具快取及舊 Log；不會清除目前 Log、共用工具、翻譯記憶、安裝記錄或已安裝的修補檔。清除前會要求確認，刪除後的工作檔及舊 Log 不可復原。舊版留在遊戲目錄的 `.game-translate` 會在首次啟動時安全搬遷；如果跨磁碟或目標位置已有資料，程式會保留舊資料並提示處理，不會刪除原件。遊戲目錄仍需保留 `GameTranslate.exe` 及 `Paks` 中的外置修補檔；停用或失敗的修補檔也可能暫留於 `Paks`，供重新啟用或故障復原。

## 轉換服務與授權

使用繁化姬 API 時，待轉換的文字會傳送至第三方服務。商業使用須依[繁化姬的收費規定](https://zhconvert.org/)付費；使用前請確認其服務條款。

本專案自行開發的原始碼採用 [MIT 授權](LICENSE)。此授權不涵蓋遊戲內容、第三方工具、翻譯、字型或網絡服務；相關說明請參閱[第三方聲明](THIRD_PARTY_NOTICES.md)。本專案與相關遊戲開發商及發行商並無隸屬關係。

## 參與開發

在 Windows 上安裝 Node.js 20 以上版本及 .NET 10 SDK 後，可以直接執行不依賴商業遊戲檔案的測試：

```powershell
git clone https://github.com/DogSay/game-translate.git
cd game-translate
npm test
npm run audit:repo
```

歡迎透過 [Issues](https://github.com/DogSay/game-translate/issues) 回報問題，或參閱[貢獻指南](CONTRIBUTING.md)。請勿上傳遊戲原始檔案、未獲授權的翻譯內容、金鑰或個人設定。技術架構與發佈檢查分別記載於[架構文件](docs/ARCHITECTURE.md)及[發佈指引](docs/RELEASING.md)。
