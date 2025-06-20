using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections.ObjectModel;

namespace SimpleC.IncrementalEditor.Views
{
    // NEW: Class to represent a line in the TemplateListBox
    public class CodeLine
    {
        public string LineNumber { get; set; } = "";
        public string Content { get; set; } = "";
        public bool HasError { get; set; } = false;

        // 行首空白 / tab，用於對齊
        public string Indent  
        {
            get
            {
                int i = 0;
                while (i < Content.Length && (Content[i] == ' ' || Content[i] == '\t')) i++;
                return Content[..i];
            }
        }
        // 旗幟 + 行號用
        public string DisplayText => $"[#{LineNumber}] {Content}";
        public string ErrorIndicator => HasError ? "🚩" : "";

        // 關鍵字高亮 – 不再 TrimStart，所以縮排保留
        private static readonly string[] kw =
            { "int", "float", "double", "char", "if", "else", "while", "for", "return" };

        public string HighlightPrefix
        {
            get
            {
                var trimmed = Content.TrimStart();
                foreach (var k in kw)
                    if (trimmed.StartsWith(k + " "))
                        return k;
                return "";
            }
        }
        public string HighlightSuffix
        {
            get
            {
                var prefix = HighlightPrefix;
                if (string.IsNullOrEmpty(prefix)) return Content.TrimStart();
                return Content.TrimStart().Substring(prefix.Length);
            }
        }
    }

    // NEW: Class to represent a node in the Abstract Syntax Tree (AST)
    public class AstNode
    {
        public string NodeType { get; set; } = "Node";
        public string Value { get; set; } = "";
        public ObservableCollection<AstNode> Children { get; set; } = new();
        public string DisplayText => string.IsNullOrEmpty(Value) ? NodeType : $"{NodeType}: {Value}";
    }

public partial class MainWindow : Window
{
        // ======== 自動補全 相關欄位 =========
        private List<string> autoCompleteCandidates = new();
        private int autoCompleteStart = -1;

        // ======== 自動補全 KeyUp 事件（TextBox 註冊 KeyUp="CodeTextBox_KeyUp"） =========
        private void CodeTextBox_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            // NOTE: Tab and Enter are handled in KeyDown.

            // Up/Down keys to navigate the list
            if ((e.Key == Avalonia.Input.Key.Down || e.Key == Avalonia.Input.Key.Up) && AutoCompleteListBox.IsVisible)
            {
                var count = AutoCompleteListBox.ItemCount;
                if (count == 0) return;
                if (e.Key == Avalonia.Input.Key.Down)
                    AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex + 1) % count;
                else
                    AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex + count - 1) % count;
                e.Handled = true;
                return;
            }

            // Escape to close the list
            if (e.Key == Avalonia.Input.Key.Escape && AutoCompleteListBox.IsVisible)
            {
                AutoCompleteListBox.IsVisible = false;
                return;
            }

            // Check for other keys to show/update the list
            if ((e.Key >= Avalonia.Input.Key.A && e.Key <= Avalonia.Input.Key.Z)
                || (e.Key >= Avalonia.Input.Key.D0 && e.Key <= Avalonia.Input.Key.D9)
                || e.Key == Avalonia.Input.Key.Back || e.Key == Avalonia.Input.Key.Delete)
            {
                ShowAutoComplete();
            }
            else
            {
                AutoCompleteListBox.IsVisible = false;
            }
        }

        // ======== 補全清單點擊選擇 =========
        private void AutoCompleteListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // 移除自動補全邏輯，只允許明確的用戶操作
            // 這裡不應該自動補全，只有點擊時才補全
        }

        // ======== 判斷游標前的關鍵字片段 =========
        private string GetCurrentWord()
        {
            int caret = CodeTextBox.CaretIndex;
            var text = CodeTextBox.Text ?? "";
            if (caret == 0) return "";
            int start = caret - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_')) start--;
            start++;
            autoCompleteStart = start;
            return text.Substring(start, caret - start);
        }

        // ======== Show suggestions for keywords AND declared variables =========
        private void ShowAutoComplete()
        {
            var curWord = GetCurrentWord();
            if (string.IsNullOrEmpty(curWord) || curWord.Length < 2)
            {
                AutoCompleteListBox.IsVisible = false;
                return;
            }

            var declaredVars = GetDeclaredVariables();
            var allSuggestions = CKeywords.Concat(declaredVars).Distinct();

            autoCompleteCandidates = allSuggestions.Where(s => s.StartsWith(curWord) && s != curWord).ToList();

            if (autoCompleteCandidates.Count == 0)
            {
                AutoCompleteListBox.IsVisible = false;
                return;
            }

            AutoCompleteListBox.ItemsSource = autoCompleteCandidates;
            AutoCompleteListBox.SelectedIndex = 0;
            AutoCompleteListBox.IsVisible = true;
        }

        // ======== 補全插入（只有確認選擇時才執行） =========
        private void ApplyAutoComplete()
        {
            if (AutoCompleteListBox.SelectedIndex < 0) return;
            var selected = autoCompleteCandidates[AutoCompleteListBox.SelectedIndex];
            var text = CodeTextBox.Text ?? "";
            int caret = CodeTextBox.CaretIndex;
            if (autoCompleteStart < 0 || autoCompleteStart > caret) return;

            // 真正插入補全的文字
            var newText = text.Substring(0, autoCompleteStart) + selected + text.Substring(caret);
            CodeTextBox.TextChanged -= CodeTextBox_TextChanged; // 避免觸發 ListBox 更新
            CodeTextBox.Text = newText;
            CodeTextBox.CaretIndex = autoCompleteStart + selected.Length;
            CodeTextBox.TextChanged += CodeTextBox_TextChanged;

            // 關閉補全清單
            AutoCompleteListBox.IsVisible = false;
        }

        // ======== NEW: Method to get all declared variables from code =========
        private List<string> GetDeclaredVariables()
        {
            var declaredVars = new HashSet<string>();
            var code = CodeTextBox.Text ?? "";
            var lines = code.Split('\n');
            var typeKeywords = new HashSet<string> { "int", "float", "double", "char" };

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                foreach (var typeKeyword in typeKeywords)
                {
                    if (trimmedLine.StartsWith(typeKeyword + " "))
                    {
                        // 如果這行有 ( 就當作函數宣告
                        if (trimmedLine.Contains("("))
                        {
                            // int foo(int a) {...   -> 取 foo 為函數名
                            var idxType = trimmedLine.IndexOf(typeKeyword) + typeKeyword.Length;
                            var afterType = trimmedLine.Substring(idxType).TrimStart();
                            var idxParen = afterType.IndexOf("(");
                            if (idxParen > 0)
                            {
                                var fname = afterType.Substring(0, idxParen).Trim();
                                if (IsVar(fname)) declaredVars.Add(fname);
                            }
                        }
                        // 處理變數宣告
                        var parts = trimmedLine.Split(new[] { ' ', ';', '=', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int p = 1; p < parts.Length; p++)
                        {
                            var varName = parts[p];
                            if (IsVar(varName))
                            {
                                declaredVars.Add(varName);
                            }
                        }
                    }
                }
            }
            return declaredVars.ToList();
        }

        private List<CodeLine> codeLines = new();

        private static readonly string[] CKeywords =
            { "int", "float", "double", "char", "if", "else", "while", "for", "return" };

        // Helper to check if a token is a valid variable name
        private static bool IsVar(string token)
            => !string.IsNullOrEmpty(token)
                && char.IsLetter(token[0])
                && token.All(c => char.IsLetterOrDigit(c) || c == '_')
                && !char.IsDigit(token[0]);

    public MainWindow()
    {
        InitializeComponent();

            // Set ItemsSource in code-behind
            TemplateListBox.ItemsSource = codeLines;

            // Re-add the event handler for selection
            TemplateListBox.SelectionChanged += TemplateListBox_SelectionChanged;

            AnalyzeButton.Click += AnalyzeButton_Click;
            ToggleAstButton.Click += ToggleAstButton_Click; // Register click event for the new button
            CodeTextBox.TextChanged += CodeTextBox_TextChanged;
            CodeTextBox.AddHandler(KeyDownEvent, CodeTextBox_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            UpdateTemplateListBox();
        }

        private void CodeTextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            UpdateTemplateListBox();
        }

        private void UpdateTemplateListBox(HashSet<int>? errorLines = null)
        {
            var rawLines = (CodeTextBox.Text ?? "").Split('\n');
            codeLines = new List<CodeLine>();
            for (int i = 0; i < rawLines.Length; i++)
            {
                codeLines.Add(new CodeLine
                {
                    LineNumber = (i + 1).ToString(),
                    Content = rawLines[i],
                    HasError = errorLines?.Contains(i + 1) ?? false
                });
            }
            TemplateListBox.ItemsSource = codeLines;
        }

        private void TemplateListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (TemplateListBox.SelectedItem is not CodeLine selectedLine) return;

            int idx = TemplateListBox.SelectedIndex;
            if (idx < 0) return;

            var code = CodeTextBox.Text ?? "";
            int startIdx = 0;
            var rawLines = code.Split('\n');

            for (int i = 0; i < idx; i++)
            {
                startIdx += rawLines[i].Length + 1; // +1 for newline
            }

            CodeTextBox.SelectionStart = startIdx;
            CodeTextBox.SelectionEnd = startIdx + rawLines[idx].Length;
            CodeTextBox.Focus();
        }

        // NEW: Event handler to toggle AST visibility
        private void ToggleAstButton_Click(object? sender, RoutedEventArgs e)
        {
            AstTreeView.IsVisible = !AstTreeView.IsVisible;
            if (AstTreeView.IsVisible && lastAstRoot != null)
            {
                AstTreeView.ItemsSource = new[] { lastAstRoot };
            }
        }

        // ========= Main analysis logic (Refactored) =========
        private AstNode? lastAstRoot = null;

        private void AnalyzeButton_Click(object? sender, RoutedEventArgs e)
        {
            AnomalyListBox.ItemsSource = null;
            var anomalies = new List<string>();
            var errorLines = new HashSet<int>();
            var code = CodeTextBox.Text ?? "";
            var lines = code.Split('\n');
            var defined = new HashSet<string>();
            var used = new HashSet<string>();
            var assigned = new HashSet<string>();
            var rootNode = new AstNode { NodeType = "Program" };

            var typeKeywords = new HashSet<string> { "int", "float", "double", "char" };

            // Helper to parse the right-hand side of an assignment for usages
            Action<string, int> parseRhsForUsage = (rhs, lineNum) =>
            {
                // Splits expression by operators and delimiters to find variable/function names
                foreach (var token in rhs.Split(new[] { '+', '-', '*', '/', ';', ' ', '(', ')', '=' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IsVar(token))
                    {
                        used.Add(token);
                        if (!defined.Contains(token))
                        {
                            anomalies.Add($"UR({token}) on line {lineNum}");
                            errorLines.Add(lineNum);
                        }
                    }
                }
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                // ----- 把 // 之後的註解去掉 -----
                var cmtIdx = line.IndexOf("//");
                if (cmtIdx >= 0) line = line.Substring(0, cmtIdx).Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var currentLineNum = i + 1;

                if (string.IsNullOrWhiteSpace(line)) continue;

                bool isDeclaration = false;
                foreach (var typeKeyword in typeKeywords)
                {
                    if (line.StartsWith(typeKeyword + " "))
                    {
                        isDeclaration = true;
                        var stripped = line.Substring(typeKeyword.Length).Trim();
                        var parenIndex = stripped.IndexOf('(');

                        // Heuristic: Is it a function declaration like "foo(int a)"?
                        // The name before the parenthesis must be a single, valid identifier.
                        if (parenIndex > 0 && IsVar(stripped.Substring(0, parenIndex).Trim()))
                        {
                            // Function Declaration Logic
                            var funcName = stripped.Substring(0, parenIndex).Trim();
                            defined.Add(funcName);
                            rootNode.Children.Add(new AstNode { NodeType = "Function Declaration", Value = funcName });

                            // Parameter Declaration Logic
                            var rParenIndex = stripped.LastIndexOf(')');
                            if (rParenIndex > parenIndex)
                            {
                                var paramStr = stripped.Substring(parenIndex + 1, rParenIndex - parenIndex - 1);
                                var paramParts = paramStr.Split(',');
                                foreach (var part in paramParts)
                                {
                                    var paramName = part.Trim().Split(' ').LastOrDefault();
                                    if (!string.IsNullOrEmpty(paramName) && IsVar(paramName))
                                    {
                                        defined.Add(paramName);
                                        // Params are considered defined and used within the function scope
                                        used.Add(paramName);
                                        assigned.Add(paramName);
                                        rootNode.Children.Add(new AstNode { NodeType = "Parameter", Value = paramName });
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Variable Declaration Logic (e.g., "int x = 1;" or "int y = foo(x);")
                            var equalsIndex = stripped.IndexOf('=');

                            // The part before '=' (or ';') contains the variable name
                            var declPart = (equalsIndex != -1) ? stripped.Substring(0, equalsIndex) : stripped.TrimEnd(';');
                            var varName = declPart.Trim();
                            if (IsVar(varName))
                            {
                                if (defined.Contains(varName))
                                {
                                    anomalies.Add($"DD({varName}) on line {currentLineNum}");
                                    errorLines.Add(currentLineNum);
                                }
                                defined.Add(varName);
                                rootNode.Children.Add(new AstNode { NodeType = "Declaration", Value = $"{typeKeyword} {varName}" });
                            }
                            else if (!string.IsNullOrEmpty(varName))
                            {
                                anomalies.Add($"Line {currentLineNum}: 詞法錯誤，'{varName}' 不是合法的識別字");
                                errorLines.Add(currentLineNum);
                            }

                            // If there's an initializer, parse it for usages
                            if (equalsIndex != -1)
                            {
                                var rhs = stripped.Substring(equalsIndex + 1).Trim().TrimEnd(';');
                                parseRhsForUsage(rhs, currentLineNum);
                                rootNode.Children.Add(new AstNode { NodeType = "Initialization", Value = rhs });
                            }
                        }
                        break;
                    }
                }

                if (isDeclaration) continue;

                // Assignment Logic (for non-declaration lines)
                if (line.Contains("="))
                {
                    var eqIdx = line.IndexOf('=');
                    var left = line.Substring(0, eqIdx).Trim();
                    if (IsVar(left))
                    {
                        assigned.Add(left);
                        if (!defined.Contains(left))
                        {
                            anomalies.Add($"UR({left}) on line {currentLineNum}");
                            errorLines.Add(currentLineNum);
                        }
                    }
                    var rhs = line.Substring(eqIdx + 1);
                    parseRhsForUsage(rhs, currentLineNum);
                    rootNode.Children.Add(new AstNode { NodeType = "Assignment", Value = line });
                    continue;
                }

                // Standalone Function Call Usage Check
                if (line.Contains("(") && line.Contains(")"))
                {
                    var lparen = line.IndexOf('(');
                    var funcName = line.Substring(0, lparen).Trim();
                    if (IsVar(funcName))
                    {
                        used.Add(funcName);
                    }
                    // Parse arguments for usages
                    parseRhsForUsage(line.Substring(lparen), currentLineNum);
                }
            }

            // --- Final Anomaly Checks ---
            foreach (var varName in defined)
            {
                if (!used.Contains(varName) && !assigned.Contains(varName))
                    anomalies.Add($"DU({varName}) defined but never used");
            }

            // Spelling errors
            anomalies.AddRange(CheckSpellingErrors(lines, errorLines));

            // === 檢查圓括號對稱 ===
            int totalLParen = code.Count(c => c == '(');
            int totalRParen = code.Count(c => c == ')');
            if (totalLParen > totalRParen)
                anomalies.Add($"缺少 {totalLParen - totalRParen} 個 )");
            else if (totalRParen > totalLParen)
                anomalies.Add($"缺少 {totalRParen - totalLParen} 個 (");

            // === 檢查分號漏寫 ===
            for (int i = 0; i < lines.Length; i++)
            {
                // 先移除註解，再 Trim
                var lineContent = lines[i];
                var commentIndex = lineContent.IndexOf("//");
                if (commentIndex >= 0)
                {
                    lineContent = lineContent.Substring(0, commentIndex);
                }
                var trimmed = lineContent.Trim();

                // 忽略空行、註解、預處理、大括號、函數標頭等
                if (string.IsNullOrEmpty(trimmed) ||
                    trimmed.StartsWith("#") ||
                    trimmed.StartsWith("{") ||
                    trimmed.StartsWith("}") ||
                    CKeywords.Any(kw => trimmed.StartsWith(kw))) // 寬鬆一點，避免函數標頭誤判
                {
                    continue;
                }
                // 如果非以 ; 結尾（且不是 { 或 } 行），警告
                if (!trimmed.EndsWith(";") && !trimmed.EndsWith("{") && !trimmed.EndsWith("}"))
                {
                    anomalies.Add($"Line {i + 1}: 缺少分號（;）");
                    errorLines.Add(i + 1);
                }
            }

            // === 檢查大括號對稱 ===
            int lbrace = code.Count(c => c == '{');
            int rbrace = code.Count(c => c == '}');
            if (lbrace > rbrace)
                anomalies.Add($"缺少 {lbrace - rbrace} 個 }}");
            else if (rbrace > lbrace)
                anomalies.Add($"缺少 {rbrace - lbrace} 個 {{");

            if (anomalies.Count == 0)
            {
                anomalies.Add("（未發現異常）");
            }

            AnomalyListBox.ItemsSource = anomalies;

            // REFRESH the TemplateListBox with error info
            UpdateTemplateListBox(errorLines);

            // Update the AST TreeView
            lastAstRoot = rootNode;
            AstTreeView.ItemsSource = new[] { rootNode };
        }

        private IEnumerable<string> CheckSpellingErrors(string[] lines, HashSet<int> errorLines)
        {
            var errors = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                // 去掉行內 // 註解，避免註解文字被當成關鍵字
                var cmt = line.IndexOf("//");
                if (cmt >= 0) line = line.Substring(0, cmt).TrimStart();

                if (string.IsNullOrWhiteSpace(line)) continue;
                var firstToken = line.Split(new[] { ' ', ';', '(', ')', '{', '}', '=' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstToken) && firstToken.Length > 2 && !CKeywords.Contains(firstToken))
                {
                    foreach (var kw in CKeywords)
                    {
                        if (LevenshteinDistance(firstToken, kw) == 1)
                        {
                            errors.Add($"Line {i + 1}: 未知關鍵字 '{firstToken}'，你是想輸入 '{kw}' 嗎？");
                            errorLines.Add(i + 1);
                            break;
                        }
                    }
                }
            }
            return errors;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)
                    );
            return dp[a.Length, b.Length];
        }

        // ======== KeyDown: Handles Tab/Enter for completion and indentation =========
        private void CodeTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            // Handle Tab and Enter for auto-completion first
            if ((e.Key == Avalonia.Input.Key.Tab || e.Key == Avalonia.Input.Key.Enter) && AutoCompleteListBox.IsVisible)
            {
                if (AutoCompleteListBox.SelectedIndex >= 0)
                {
                    ApplyAutoComplete();
                    e.Handled = true; // This is crucial to stop default Tab/Enter behavior
                }
                return; // Prevent Enter from also triggering indentation
            }

            // If Enter was pressed but autocomplete was not active, handle indentation
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var text = CodeTextBox.Text ?? "";
                var caret = CodeTextBox.CaretIndex;
                int lineStart = text.LastIndexOf('\n', caret - 1);
                if (lineStart == -1) lineStart = 0; else lineStart += 1;
                int nextNL = text.IndexOf('\n', lineStart);
                if (nextNL == -1) nextNL = text.Length;
                string curLine = text.Substring(lineStart, nextNL - lineStart);

                string indent = "";
                int p = lineStart;
                while (p < text.Length && (text[p] == ' ' || text[p] == '\t')) indent += text[p++];

                if (curLine.Trim() == "}" && indent.Length >= 4 && caret == lineStart + indent.Length + 1)
                {
                    string newIndent = indent.Substring(0, indent.Length - 4);
                    string newLine = newIndent + "}";
                    string newText = text.Substring(0, lineStart) + newLine + text.Substring(nextNL);
                    string insertText = "\n" + newIndent;
                    newText = newText.Insert(lineStart + newLine.Length, insertText);
                    int newCaret = lineStart + newLine.Length + insertText.Length;
                    CodeTextBox.TextChanged -= CodeTextBox_TextChanged;
                    CodeTextBox.Text = newText;
                    CodeTextBox.CaretIndex = newCaret;
                    CodeTextBox.TextChanged += CodeTextBox_TextChanged;
                    e.Handled = true;
                    return;
                }

                string nextIndent = indent;
                bool extraIndent = false;
                if (caret > 0)
                {
                    int prevEnd = caret - 1;
                    while (prevEnd >= lineStart && char.IsWhiteSpace(text[prevEnd])) prevEnd--;
                    if (prevEnd >= lineStart && text[prevEnd] == '{') extraIndent = true;
                }
                if (extraIndent) nextIndent += "    ";
                string insertText2 = "\n" + nextIndent;
                var newText2 = text.Insert(caret, insertText2);
                CodeTextBox.TextChanged -= CodeTextBox_TextChanged;
                CodeTextBox.Text = newText2;
                CodeTextBox.CaretIndex = caret + insertText2.Length;
                CodeTextBox.TextChanged += CodeTextBox_TextChanged;
                e.Handled = true;
            }
        }
    }
}
