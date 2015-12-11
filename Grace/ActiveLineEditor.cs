// Copyright (c) 2015 Michael Homer
//
// This file is distributed under the MIT licence.
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace ActiveLineEditor
{
    /// <summary>
    /// A line-based editor for the console.
    /// </summary>
    class Editor
    {
        StringBuilder text;

        int cursor;
        private int[] graphemeIndices = emptyArray;
        private static int[] emptyArray = new int[0];

        List<string> history = new List<string>();
        int historyIndex;

        int row;
        int maxGraphemes;

        bool close;

        string promptText;

        IList<Completion> completions;
        IList<Completion> lastCompletions;
        int completionIndex;

        Func<string, IList<Completion>> completionHook;

        public Editor()
        {
            text = new StringBuilder();
        }

        /// <param name="hook">
        /// Function to call when tab completion is requested,
        /// returning a list of available completions.
        /// </param>
        public Editor(Func<string, IList<Completion>> hook)
        {
            text = new StringBuilder();
            completionHook = hook;
        }

        /// <summary>
        /// Prompt the user for a single line of input, with
        /// editing and history abilities.
        /// </summary>
        /// <param name="prompt">
        /// Prompt text to display.
        /// </param>
        public string GetLine(string prompt)
        {
            if (Console.IsInputRedirected || Console.BufferWidth == 0)
            {
                // In a pipeline or similar, revert to plain
                // ReadLine rather than trying to draw.
                return Console.ReadLine();
            }
            promptText = prompt;
            reset();
            readLoop();
            Console.TreatControlCAsInput = false;
            if (close)
                return null;
            return text.ToString();
        }

        private void readLoop()
        {
            row = Console.CursorTop;
            ConsoleKeyInfo cki;
            for (int i = 0; i < Console.BufferWidth - promptText.Length; i++)
                Console.Write(' ');
            updateDisplay();
            while (true)
            {
                cki = Console.ReadKey(true);
                switch (cki.Key)
                {
                    case ConsoleKey.Enter:
                        if (completions != null)
                        {
                            // Activate a tab-complete option if selected.
                            if (completionIndex >= 0
                                    && completionIndex < completions.Count)
                            {
                                text.Append(
                                        completions[completionIndex].append);
                                clearTabCompletion();
                                update();
                                cursor = graphemeIndices.Length;
                                updateDisplay();
                                break;
                            }
                        }
                        clearTabCompletion();
                        lastCompletions = null;
                        Console.WriteLine();
                        if (text.Length > 0)
                            history.Add(text.ToString());
                        return;
                    case ConsoleKey.Backspace:
                        backspace();
                        break;
                    case ConsoleKey.Delete:
                        delete();
                        break;
                    case ConsoleKey.LeftArrow:
                        leftArrow();
                        break;
                    case ConsoleKey.RightArrow:
                        rightArrow();
                        break;
                    case ConsoleKey.Home:
                        home();
                        break;
                    case ConsoleKey.End:
                        end();
                        break;
                    case ConsoleKey.UpArrow:
                        upArrow();
                        break;
                    case ConsoleKey.DownArrow:
                        downArrow();
                        break;
                    case ConsoleKey.Tab:
                        tabComplete();
                        break;
                    default:
                        if ((cki.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            // Ctrl-D, A, E, and C have meanings. All other
                            // Ctrl-X combinations are ignored.
                            if (cki.Key == ConsoleKey.D && text.Length == 0)
                            {
                                Console.WriteLine();
                                close = true;
                                return;
                            }
                            if (cki.Key == ConsoleKey.A)
                                home();
                            if (cki.Key == ConsoleKey.E)
                                end();
                            if (cki.Key == ConsoleKey.C)
                            {
                                if (text.Length == 0)
                                    Environment.Exit(-2);
                                clearLine();
                                reset();
                                updateDisplay();
                                break;
                            }
                            break;
                        }
                        else if (cki.KeyChar != 0)
                            handleChar(cki.KeyChar);
                        break;
                }
                if (completions != null)
                {
                    switch (cki.Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.Tab:
                            break;
                        default:
                            if (cki.KeyChar == 0)
                                break;
                            completionIndex = -1;
                            renderTabCompletion();
                            break;
                    }
                }
            }
        }

        private void backspace()
        {
            var last = cursor - 1;
            if (last < 0)
                return;
            var index = graphemeIndices[last];
            var end = graphemeIndices.Length > last + 1
                ? graphemeIndices[last + 1]
                : text.Length;
            text.Remove(index, end - index);
            cursor--;
            update();
            updateDisplay();
        }

        private void delete()
        {
            if (cursor >= graphemeIndices.Length)
                return;
            var index = graphemeIndices[cursor];
            var end = graphemeIndices.Length > cursor + 1
                ? graphemeIndices[cursor + 1]
                : text.Length;
            text.Remove(index, end - index);
            update();
            updateDisplay();
        }

        private void leftArrow()
        {
            if (cursor > 0)
                cursor--;
            updateDisplay();
        }

        private void rightArrow()
        {
            if (cursor < graphemeIndices.Length)
                cursor++;
            updateDisplay();
        }

        private void upArrow()
        {
            if (completions != null)
            {
                if (completionIndex >= 0)
                    completionIndex--;
                renderTabCompletion();
                return;
            }
            if (historyIndex >= history.Count)
                return;
            var s = text.ToString();
            historyIndex++;
            takeFromHistory();
            if (historyIndex == 1 && s != "")
            {
                history.Add(s);
                historyIndex++;
            }
        }

        private void downArrow()
        {
            if (completions != null)
            {
                completionIndex++;
                if (completionIndex == completions.Count)
                    completionIndex--;
                renderTabCompletion();
                return;
            }
            if (historyIndex == 0)
                return;
            historyIndex--;
            takeFromHistory();
        }

        private void takeFromHistory()
        {
            string s = "";
            if (historyIndex > 0)
                s = history[history.Count - historyIndex];
            clearLine();
            text.Length = 0;
            text.Append(s);
            update();
            cursor = graphemeIndices.Length;
            updateDisplay();
        }

        private void home()
        {
            cursor = 0;
            updateDisplay();
        }

        private void end()
        {
            cursor = graphemeIndices.Length;
            updateDisplay();
        }

        private void update()
        {
            if (text.Length > 0)
                graphemeIndices = StringInfo.ParseCombiningCharacters(
                        text.ToString());
            else
                graphemeIndices = emptyArray;
        }

        private void clearLine()
        {
            Console.SetCursorPosition (0, row);
            Console.Write(promptText);
            var l = promptText.Length;
            for (int i = l; i < maxGraphemes; i++)
                Console.Write(' ');
            Console.SetCursorPosition(promptText.Length, row);
        }

        private void reset()
        {
            Console.TreatControlCAsInput = true;
            close = false;
            text.Length = 0;
            maxGraphemes = 0;
            cursor = 0;
            graphemeIndices = emptyArray;
            historyIndex = 0;
            completions = null;
        }

        private int getTextIndex()
        {
            if (cursor < graphemeIndices.Length)
                return graphemeIndices[cursor];
            return text.Length;
        }

        private void handleChar(char c)
        {
            text.Insert(getTextIndex(), c);
            if (c >= 0xD800 && c <= 0xDBFF)
            {
                // Leading surrogate - don't render yet
                return;
            }
            var tmp = graphemeIndices.Length;
            update();
            if (graphemeIndices.Length != tmp)
                cursor++;
            updateDisplay();
        }

        private void updateDisplay()
        {
            var col = promptText.Length + cursor;
            var lines = (promptText.Length + graphemeIndices.Length - 1)
                / Console.BufferWidth;
            col %= Console.BufferWidth;
            Console.SetCursorPosition (0, row - lines);
            Console.Write(promptText);
            Console.Write(text);
            var l = promptText.Length + graphemeIndices.Length;
            if (l > maxGraphemes)
                maxGraphemes = l;
            else
            {
                for (int i = l; i < maxGraphemes; i++)
                    Console.Write(' ');
            }
            // Make sure we get an extra line before we need it.
            if (col == 0)
                Console.Write(' ');
            var offset = lines - (promptText.Length + cursor - 1)
                / Console.BufferWidth;
            if (col == 0 && offset > 0) offset--;
            Console.SetCursorPosition(col, row - offset);
        }

        private void tabComplete()
        {
            completions = lastCompletions;
            clearTabCompletion();
            if (completionHook != null)
                completions = completionHook(text.ToString());
            if (tryComplete())
                return;
            completionIndex = -1;
            renderTabCompletion();
            lastCompletions = completions;
        }

        private bool tryComplete()
        {
            if (completions.Count == 1)
            {
                text.Append(completions[0].append);
                update();
                cursor = graphemeIndices.Length;
                updateDisplay();
                completions = null;
                return true;
            }
            if (completions.Count == 0)
            {
                completions = null;
                return true;
            }
            return false;
        }

        private void clearTabCompletion()
        {
            if (completions == null)
                return;
            updateDisplay();
            Console.ResetColor();
            Console.SetCursorPosition(0, row);
            Console.WriteLine();
            foreach (var comp in completions)
            {
                Console.Write("    ");
                for (var i = 0; i < comp.label.Length; i++)
                    Console.Write(' ');
                Console.WriteLine();
            }
            completions = null;
            updateDisplay();
        }

        private void renderTabCompletion()
        {
            updateDisplay();
            Console.ResetColor();
            var bg = Console.BackgroundColor;
            var fg = Console.ForegroundColor;
            Console.SetCursorPosition(0, row);
            Console.WriteLine();
            var i = 0;
            foreach (var comp in completions)
            {
                Console.Write("    ");
                if (completionIndex == i++)
                {
                    Console.ForegroundColor = bg;
                    Console.BackgroundColor = fg;
                }
                Console.Write(comp.label);
                Console.ResetColor();
                Console.WriteLine();
            }
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
            Console.ResetColor();
            var newRow = Console.CursorTop;
            var col = promptText.Length + cursor;
            col %= Console.BufferWidth;
            row = newRow - completions.Count - 1;
            Console.SetCursorPosition(col, newRow - completions.Count - 1);
        }

        /// <summary>
        /// Represents a single tab-completion option.
        /// </summary>
        public struct Completion
        {
            /// <summary>
            /// Text to append.
            /// </summary>
            public string append;

            /// <summary>
            /// Label to display to the user.
            /// </summary>
            public string label;

            /// <summary>
            /// Additional descriptive text for this option.
            /// </summary>
            public string description;
        }

        /// <summary>
        /// Create a Completion representing a tab completion.
        /// </summary>
        /// <param name="append">
        /// Text to append
        /// </param>
        /// <param name="label">
        /// Label to display to the user
        /// </param>
        /// <param name="description">
        /// Additional descriptive text for this option.
        /// </param>
        public static Completion CreateCompletion(string append,
                string label, string description)
        {
            return new Completion
            {
                append = append, label = label, description = description
            };
        }
    }

    class Test
    {
        // Rename to Main to run this file standalone.
        public static void MainDemo(string[] args)
        {
            var edit = new Editor(complete);
            string line;
            while ((line = edit.GetLine(">>> ")) != null)
            {
                Console.WriteLine("got: " + line);
            }
        }

        private static List<Editor.Completion> complete(string s)
        {
            var ret = new List<Editor.Completion>();
            ret.Add(Editor.CreateCompletion("oo", "foo", ""));
            ret.Add(Editor.CreateCompletion("ix", "fix", ""));
            return ret;
        }
    }
}
