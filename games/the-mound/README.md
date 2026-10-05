# The Mound: Omen of Cthulhu

- Engine：Unreal Engine 5.7
- Packaging：IoStore + legacy pak
- Localization 來源：未加密 `pakchunk0-Windows.pak` 內的 `zh-Hans/*.locres`
- 原裝 archive：永不修改
- Pak metadata：V11、mount point `../../../`、Zlib、path hash seed `0xD8195615`
- UE archive probe：`GAME_UE5_7`；retoc target：`UE5_7`

## 模式

### `compat`

- Culture：`zh-Hans`
- Patch：`pakchunk99-GameTranslate_zhHans_P.pak` + 同名 `.utoc/.ucas`
- 用途：最高兼容性；patch 仍覆蓋遊戲嘅 `zh-Hans` slot，但語言選項字串會顯示「繁體中文」，遊戲內容亦顯示繁中。

此遊戲的 manifest 僅支援 `compat`：遊戲沒有可用的 `zh-Hant` culture／語言選項，而且會把設定改回 `zh-Hans`。目前可攜版 UI 仍提供通用的實驗性原生選項；該選項未通過此遊戲的驗證，請使用兼容模式。

## Runtime localization targets（locres string array）

- `Engine.locres`：41,075 strings（v3）
- `Game.locres`：1,955 strings（v3）
- `Legal.locres`：100 entries
- `MyNacon.locres`：81 strings（v3）
- `XeSS.locres`：31 strings（v3）
- `OnlineSubsystem.locres`：6 entries
- `OnlineSubsystemSteam.locres`：1 entry
- `OnlineSubsystemUtils.locres`：20 entries
- 合計：43,269 strings

UEExtractor 只再用於 probe/CSV extraction，禁止重建 locres。Build 會從原版 pak 抽取每個 locres，保留 v1/v3 header 與 hash tables，只重寫檔尾字串陣列；因此 `Engine.locres` 亦可以安全加入 patch，補回引擎層 runtime UI 字串。

2026-07-18 14:30 最新 EXE log 嘅修正版安裝 PAK SHA-256 為 `CE6DB2D5CE802F8CF3E7DFE00F936321ABFA2E9C1D0E000917631DD94223DD21`；同名 202-byte `.utoc`（`8A231393...`）與 64-byte `.ucas`（`1EF841FD...`）缺一不可。當次 surgery replacement：Engine 36,722/41,075、Game 1,748/1,955、Legal 97/100，其餘 target 合計 122/139。中央 CLI 重建後 8 個 locres payload SHA-256 已逐一同該修正版完全一致；PAK 容器 hash 可因 repak file order 不同而改變，驗證以 unpack round-trip 及逐檔 hash 為準。

## 指令

```powershell
npm run build -- the-mound compat
npm run install-patch -- the-mound compat

npm run deactivate-patches -- the-mound
```

可攜版已安裝於遊戲根目錄：

`C:\Program Files (x86)\Steam\steamapps\common\The Mound\GameTranslate.exe`

雙擊後會顯示 Unreal／IoStore 偵測結果、現有翻譯 patch、API 測試、兼容模式與 log。可攜版會更新 `GameUserSettings.ini` 至 `zh-Hans`；遊戲文化仍用 `zh-Hans`，但 patch 會把語言選項文字改成「繁體中文」，並由外加 patch 提供繁體內容。

停用時會把工具擁有的 `.pak/.utoc/.ucas` 三件套一同改名為 `.disabled`，並先備份玩家設定；不需要 Steam 驗證原裝檔案。
