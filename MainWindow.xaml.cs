using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TTS3
{
    public partial class MainWindow : Window
    {
        // Fields used by PluginManager.PluginHost
        internal readonly List<string> _createdAudioFiles = new List<string>();
        internal string _lastOutputFolder = string.Empty;

        // Recognize <delay=1.5sec> (case-insensitive)
        private static readonly Regex DelayTagRegex =
            new Regex(@"<\s*delay\s*=\s*([0-9]*\.?[0-9]+)\s*sec\s*>",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TTS3 — demo window\nSupports <delay=xsec> tags before conversion.", "About");
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            string raw = GetPlainTextFromRichTextBox(rtbTextContent);

            // Extract and strip delay tags before "conversion"
            double delaySeconds = ExtractDelayAndStripFromText(ref raw);
            if (delaySeconds > 0)
            {
                LogMessage($"⏱ Delay {delaySeconds:0.###} sec before processing...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }

            // Simulate a conversion using cleaned text
            LogMessage($"Converting {Math.Min(raw.Length, 80)} chars...");
            await Task.Delay(500);

            // Demo outputs for plugin queries
            _lastOutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dummyFile = System.IO.Path.Combine(_lastOutputFolder, "demo.mp3");
            if (!_createdAudioFiles.Contains(dummyFile))
                _createdAudioFiles.Add(dummyFile);

            LogMessage("Done.");
            txtStatus.Text = "Conversion complete";
        }

        private void TestSelection_Click(object sender, RoutedEventArgs e)
        {
            string text = GetPlainTextFromRichTextBox(rtbTextContent);
            // Remove delay tags for test playback
            text = DelayTagRegex.Replace(text, "");
            LogMessage("Test Selection cleaned (delay tags stripped).");
        }

        private void StopTestSelection_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Test Selection stopped.");
        }

        private void LogMessage(string line)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }

        private static string GetPlainTextFromRichTextBox(RichTextBox rtb)
        {
            TextRange range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            return range.Text ?? string.Empty;
        }

        /// <summary>
        /// Finds all &lt;delay=xsec&gt; tags in 'text', sums total delay seconds,
        /// strips the tags from the text, and returns the total delay.
        /// The cleaned text is returned via the 'ref' parameter.
        /// </summary>
        private static double ExtractDelayAndStripFromText(ref string text)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;

            double totalDelay = 0.0;
            foreach (Match m in DelayTagRegex.Matches(text))
            {
                if (m.Success && m.Groups.Count > 1)
                {
                    if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                        totalDelay += s;
                }
            }

            text = DelayTagRegex.Replace(text, "");
            return totalDelay;
        }
    }
}
