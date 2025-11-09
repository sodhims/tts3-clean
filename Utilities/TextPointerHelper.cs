using System.Windows.Documents;

namespace TTS3.Utilities
{
    /// <summary>
    /// Helper methods for working with TextPointer in RichTextBox
    /// IMPROVED VERSION with better offset handling
    /// </summary>
    public static class TextPointerHelper
    {
        /// <summary>
        /// Get TextPointer at a specific character offset from start
        /// This version properly handles RichTextBox's internal formatting
        /// </summary>
        public static TextPointer GetTextPointerAtOffset(TextPointer start, int offset)
        {
            if (start == null) return null;
            if (offset <= 0) return start;

            var navigator = start;
            int charCount = 0;

            while (navigator != null && charCount < offset)
            {
                var context = navigator.GetPointerContext(LogicalDirection.Forward);

                if (context == TextPointerContext.Text)
                {
                    // Get the text run
                    string textRun = navigator.GetTextInRun(LogicalDirection.Forward);
                    int textLength = textRun.Length;
                    int remaining = offset - charCount;

                    if (remaining <= textLength)
                    {
                        // We found the position within this text run
                        return navigator.GetPositionAtOffset(remaining, LogicalDirection.Forward);
                    }

                    charCount += textLength;
                }
                else if (context == TextPointerContext.ElementStart || 
                         context == TextPointerContext.ElementEnd)
                {
                    // Skip formatting elements - they don't count as characters
                }

                // Move to next position
                var next = navigator.GetNextContextPosition(LogicalDirection.Forward);
                if (next == null) break;
                navigator = next;
            }

            return navigator;
        }

        /// <summary>
        /// Get character offset of TextPointer from document start
        /// </summary>
        public static int GetOffsetFromStart(TextPointer start, TextPointer position)
        {
            if (position == null || start == null) return 0;

            var range = new TextRange(start, position);
            return range.Text.Length;
        }

        /// <summary>
        /// Debug helper: Get text snippet around a position
        /// </summary>
        public static string GetTextAroundPosition(TextPointer position, int contextLength = 20)
        {
            if (position == null) return "[NULL]";

            var start = position.GetPositionAtOffset(-contextLength, LogicalDirection.Backward) 
                        ?? position.DocumentStart;
            var end = position.GetPositionAtOffset(contextLength, LogicalDirection.Forward) 
                      ?? position.DocumentEnd;

            var range = new TextRange(start, end);
            return $"...{range.Text}...";
        }
    }
}