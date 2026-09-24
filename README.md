# Game Translate

集中管理 Unity／Unreal 遊戲翻譯、翻譯記憶、遊戲 adapter 同外置 patch。所有生成物都放在本 project；原裝遊戲 archive（`.pak`、`.utoc`、`.ucas`、Unity asset bundle）不會被改寫。

Game Translate is an open-source Windows toolkit for building reversible,
game-specific localization patches without rewriting original Unity or Unreal
archives. The UI is Cantonese-first; source code, safety boundaries, and
contribution guidance are maintained for an international developer audience.

## Project status

| Area | Status |
| --- | --- |
| Unreal locres v1-v3 surgery | Implemented and regression-tested |
| Unreal Pak + IoStore companion workflow | Implemented and regression-tested |
| The Mound compatibility adapter | Historically verified in-game; current rebuilt artifact still needs a fresh visual check |
| Unity packaging detection | Implemented |
| Unity translation adapter | Not yet implemented or claimed as supported |
| Public source release | Ready for review; generated and copyrighted payloads are excluded |
| Public portable-binary release | Blocked pending the Oodle redistribution gate documented in `THIRD_PARTY_NOTICES.md` |

**The Mound: Omen of Cthulhu**（Unreal Engine 5.7）嘅 locres 手術 + IoStore 三件套配方曾於 2026-07-18 實機顯示繁體。最新 pipeline 會處理 8 個 runtime target（包括 `Engine.locres`），並把 compat 語言選項由「簡體中文」改顯示為「繁體中文」；實際 culture slot 仍然係遊戲支援嘅 `zh-Hans`。目前產品化後 payload 已同最新 Claude 修正版逐 byte 對齊，但新 build 仍要再做一次 fresh in-game check 先可以視為端到端驗證。Unity 暫時只有封裝偵測。

## 可攜式 EXE

本機執行 `npm run publish:exe` 後會建立
`dist/portable/GameTranslate.exe`。佢係單一檔案 Windows x64 桌面工具，使用者不需要另外安裝 Node 或 .NET。將 EXE 複製到遊戲根目錄後執行，程式會以所在資料夾作預設遊戲路徑。

`dist/` 只係本機生成物，唔會提交到 Git。公開 binary 前必須完成
[release checklist](docs/RELEASING.md)，尤其唔可以重新發布未獲授權嘅
Oodle runtime。

介面提供：

- 自動辨識深層 Unreal `Content/Paks`、IoStore／Pak、Unity Player／Data layout；
- Unreal 兼容模式（繁體覆蓋 `zh-Hans`）；未經實機驗證的 native culture 會隱藏；
- 可攜版會以可回復交易同步更新 `GameUserSettings.ini` 的 `Language`／`Locale`；
- 繁化姬 API 連接測試與 protected-token 批次轉換；
- 每個成功 batch 立即以 atomic JSON 寫入 `.game-translate/translation-memory/`，失敗 batch 不會快取；
- detect、進度、warning、exception 與完整 log；
- 偵測 Game Translate 現行或舊版 patch，安全改名為 `.disabled`；
- 內置並自動解壓 UEExtractor、repak、retoc，工具檔會逐一做 SHA-256 比對；
- locres version/header/hash-table byte preservation、pak metadata、精確虛擬路徑、pack/unpack hash、IoStore 三件套 SHA-256，以及 installed pak metadata／path 重讀驗證。

所有暫存、工具、輸出與 log 都放在遊戲根目錄的 `.game-translate/`。程式不會修改原裝 `.pak`、`.utoc`、`.ucas` 或 Unity asset bundle；其他作者的 `_P.pak` 只會顯示，還原功能不會處理。

Unity 目前會準確顯示「已偵測但 adapter 未完成」，不會提供未驗證的翻譯按鈕。純 IoStore 而沒有可讀 base `.pak` metadata、加密 archive 或非 locres 遊戲會顯示明確錯誤及 log，需再新增 adapter。

建立 EXE：

```powershell
npm run publish:exe
```

Clone 後可以先跑純 source 測試；測試唔需要安裝任何商業遊戲：

```powershell
npm test
npm run audit:repo
```

建立 EXE 另外需要 `.tools/manifest.json` 所列嘅 pinned tools 同本機 native
dependencies。下載返嚟嘅工具永遠留喺被忽略嘅 `.tools/`，唔屬於 source
repository。

## 基本指令

CLI 使用前先建立本機路徑設定；`game.local.json` 會被 Git 忽略：

```powershell
Copy-Item games/the-mound/game.local.example.json games/the-mound/game.local.json
# 編輯 game.local.json，填入你自己嘅遊戲安裝位置
```

```powershell
npm test
npm run doctor -- the-mound
npm run scan -- the-mound
npm run extract -- the-mound
npm run convert -- the-mound
```

The Mound 目前只提供 compat；以下新 pipeline 產物安裝後仍需重新實機確認：

```powershell
# 兼容模式：繁中內容覆蓋遊戲的 zh-Hans 槽位
npm run build -- the-mound compat
npm run install-patch -- the-mound compat

# 停用所有外置翻譯 patch，語系還原為 zh-Hans
npm run deactivate-patches -- the-mound
```

`install-patch` 會：

- 對 IoStore 遊戲以同名 `_P.pak/.utoc/.ucas` 三件套安裝；
- 備份將被覆蓋的 patch 與 `GameUserSettings.ini`；
- 只更新 `[Internationalization]` 的 `Language`／`Locale`。

## Project 結構

- `src/core/`：設定、偵測、protected text、translation memory
- `src/providers/`：翻譯 provider
- `src/formats/`：CSV、UEExtractor 正規化
- `src/adapters/`：Unity／Unreal 封裝行為
- `src/workflows/`：轉換與模式啟用流程
- `games/`：逐遊戲 manifest 與已觀察事實
- `work/`：extract、translation memory、backup、驗證資料
- `dist/`：按遊戲及模式分開的 installable patch
- `.tools/manifest.json`：已釘選外部工具與 SHA-256
- `skills/game-translate/`：Codex reusable workflow；implementation 仍以本 project 為準
- `desktop/`：可攜 WinForms EXE、共享 core 與無外部套件測試 harness

整體分層、信任邊界同「implemented／artifact-verified／game-verified」定義見
[Architecture](docs/ARCHITECTURE.md)。發布流程見
[Release checklist](docs/RELEASING.md)，版本變更見 [CHANGELOG.md](CHANGELOG.md)。

## Open-source safety boundary

以下內容永遠唔會收錄入 Git repository 或 source release：

- 原裝或解包後嘅遊戲檔案；
- `.pak/.utoc/.ucas/.locres/.uasset/.uexp` payload；
- 下載返嚟嘅漢化／翻譯包同第三方字型；
- AES key、API key、玩家設定、log、backup、translation memory；
- `.tools/` 下載 binary、Oodle DLL、`work/`、`dist/` 同 `tmp/`。

`npm run audit:repo` 會檢查實際準備加入 Git 嘅檔案，避免意外提交以上內容。
詳細貢獻規則見 [CONTRIBUTING.md](CONTRIBUTING.md)，第三方授權邊界見
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。`package.json` 保留
`"private": true` 只係防止意外發布到 npm，唔代表 source code 係閉源。

## 繁化姬

本程式使用[繁化姬 API](https://zhconvert.org/) 的 `Taiwan` converter；繁化姬商用必須付費。批次轉換只提交可見中文字串，markup、placeholder、printf token、escape、key、namespace 與 row order 會被保護及驗證。

翻譯只有在整份 CSV 成功後才會發布；每個成功 API batch 會先寫入 translation memory，失敗時不會覆蓋上一份完整輸出。

## License

Game Translate 自有 source code 以 [MIT License](LICENSE) 發布。呢個授權唔涵蓋
任何遊戲、mod、翻譯、字型、網絡服務或第三方工具。專案同相關遊戲開發商、
發行商及平台並無隸屬關係。
