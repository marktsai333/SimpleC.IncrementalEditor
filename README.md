# SimpleC Incremental DFA Editor

## 1. 簡介

本專案是一個支援 C 語言基本語法增量分析、錯誤提示、自動補全的簡易 SDE（Source Development Environment）。
左側有語法高亮/錯誤旗幟，中間可編輯 C code，右側顯示資料流異常與 AST（語法樹）。

## 2. 執行方式

### macOS

```sh
cd publish/osx-arm64/
chmod +x SimpleC.IncrementalEditor
./SimpleC.IncrementalEditor
```

### Windows

直接執行：
```sh
publish/win-x64/SimpleC.IncrementalEditor.exe
```

## 3. 主要功能
- 文字編輯功能：支援基本編輯、複製、貼上、Undo/Redo
- 語法高亮（左側 Template 區塊）：自動標色 C 語言關鍵字與錯誤旗幟
- 自動縮排：如 C 的大括號/結尾自動對齊縮排
- 語法偵測：即時偵測分號、大括號、小括號、拼字錯誤
- 自動補全：支援關鍵字、已宣告變數/函數名稱補全
- 資料流異常提示：如 DD/UR/DU 錯誤、合法性分析
- AST 語法樹顯示（選配，可切換顯示/隱藏）

## 4. 測試用例

可用 /測試範例/ 資料夾內的 .c 檔測試，也可直接貼上以下片段：

```c
int x;
float y;

imt z;           // 拼字錯誤
int x;           // DD

y = foo(x);      // UR(foo)
if (y){          // 缺分號
x = x + 1;
}

int foo(int a){
a = a + 1;
}                // 括號/大括號平衡
```

## 5. 其他說明
- 程式碼區尚未支援高亮，僅左側 Template 區塊有語法高亮。
- 若需 macOS 可點擊啟動的 .app 版本，請見 Avalonia 官方文件或手動用 Terminal 啟動。
- 詳細開發說明、設計理念、功能實現請見 FinalReport.pdf。
