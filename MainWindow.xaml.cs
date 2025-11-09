using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Text;
using Microsoft.Win32;
using TTS3.Models;
using TTS3.Services;
using TTS3.Utilities;
using TTS3.Plugins;


namespace TTS3
{
    public partial class MainWindow : Window
    {
        // Services
        private readonly CredentialService _credentialService;
        private readonly SAPITTSService _sapiService;
        private readonly GoogleTTSService _googleService;
        private readonly AWSPollyTTSService _awsService;
        private readonly ElevenLabsTTSService _elevenLabsService;
        private readonly LemonfoxTTSService _lemonfoxService;

        private readonly AudioPlaybackService _playbackService;
        private readonly AudioMergeService _mergeService;
        private readonly FileManagementService _fileService;
        private readonly SSMLProcessingService _ssmlProcessingService;
        private readonly SSMLValidationService _ssmlValidationService;

        // Current state
        private List<ITTSService> _ttsServices;
        private ITTSService _currentService;
        private string _currentFilePath = "";
        internal List<string> _createdAudioFiles = new List<string>();
        internal string _lastOutputFolder = "";
        private System.Windows.Threading.DispatcherTimer _playbackTimer;

        private PluginManager _pluginManager;

        public MainWindow(
            CredentialService credentialService,
            SAPITTSService sapiService,
            GoogleTTSService googleService,
            AWSPollyTTSService awsService,
            ElevenLabsTTSService elevenLabsService,
            LemonfoxTTSService lemonfoxService,
            AudioPlaybackService playbackService,
            AudioMergeService mergeService,
            FileManagementService fileService,
            SSMLProcessingService ssmlProcessingService,
            SSMLValidationService ssmlValidationService)
        {
            InitializeComponent();
            InitializePluginSystem();



            // Store services
            _credentialService = credentialService;
            _sapiService = sapiService;
            _googleService = googleService;
            _awsService = awsService;
            _elevenLabsService = elevenLabsService;
            _lemonfoxService = lemonfoxService;  
            _playbackService = playbackService;
            _mergeService = mergeService;
            _fileService = fileService;
            _ssmlProcessingService = ssmlProcessingService;
            _ssmlValidationService = ssmlValidationService;

            // Initialize TTS services list
            _ttsServices = new List<ITTSService>
            {
                _sapiService,
                _googleService,
                _awsService,
                _elevenLabsService,
                _lemonfoxService  
            };

            // Set default service
            cmbTTSEngine.SelectedIndex = 0;
            cmbOutputFormat.SelectedIndex = 0;
            _currentService = _sapiService;

            // Initialize playback timer
            _playbackTimer = new System.Windows.Threading.DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
            _playbackTimer.Tick += PlaybackTimer_Tick;

            // Setup playback service events
            _playbackService.PlaybackStateChanged += PlaybackService_PlaybackStateChanged;
            _playbackService.PlaybackCompleted += PlaybackService_PlaybackCompleted;

            // Keyboard shortcuts
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Initialize comment textbox placeholder
            InitializeCommentTextBox();

            LoadVoices();
        }

        #region Initialization

        private void InitializeCommentTextBox()
        {
            txtCommentText.Text = "Enter comment...";
            txtCommentText.Foreground = Brushes.Gray;

            txtCommentText.GotFocus += (s, e) =>
            {
                if (txtCommentText.Text == "Enter comment...")
                {
                    txtCommentText.Text = "";
                    txtCommentText.Foreground = Brushes.Black;
                }
            };

            txtCommentText.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtCommentText.Text))
                {
                    txtCommentText.Text = "Enter comment...";
                    txtCommentText.Foreground = Brushes.Gray;
                }
            };

            txtCommentText.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    InsertComment_Click(s, e);
                    e.Handled = true;
                }
            };
        }

        private void LoadVoices()
        {
            cmbVoices.Items.Clear();

            if (_currentService != null)
            {
                var voices = _currentService.GetAvailableVoices();
                foreach (var voice in voices)
                {
                    cmbVoices.Items.Add(voice.DisplayName);
                }

                if (cmbVoices.Items.Count > 0)
                {
                    cmbVoices.SelectedIndex = 0;
                }
            }
        }

        #endregion

        #region File Operations

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _currentFilePath = openFileDialog.FileName;
                    string content = _fileService.LoadTextFile(_currentFilePath);

                    rtbTextContent.Document.Blocks.Clear();
                    rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(content)));

                    LogMessage($"Loaded file: {_currentFilePath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveText_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string text = new TextRange(rtbTextContent.Document.ContentStart,
                        rtbTextContent.Document.ContentEnd).Text;
                    _fileService.SaveTextFile(saveFileDialog.FileName, text);
                    LogMessage($"Saved file: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region TTS Engine and Voice Selection

        private void TTSEngine_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTTSEngine.SelectedIndex >= 0 && cmbTTSEngine.SelectedIndex < _ttsServices.Count)
            {
                _currentService = _ttsServices[cmbTTSEngine.SelectedIndex];
                LoadVoices();
                LogMessage($"Switched to {_currentService.GetType().Name}");
            }
        }
        private void Voice_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Voice selection changed
        }

        #endregion

        #region Quick Insert Tools

        private void InsertSplit_Click(object sender, RoutedEventArgs e)
        {
            var caretPos = rtbTextContent.CaretPosition;
            caretPos.InsertTextInRun("<split>");
        }

        private void InsertVoice_Click(object sender, RoutedEventArgs e)
        {
            if (cmbVoiceTag.SelectedItem != null)
            {
                var voiceTag = (cmbVoiceTag.SelectedItem as ComboBoxItem).Content.ToString();
                var caretPos = rtbTextContent.CaretPosition;
                caretPos.InsertTextInRun($"<{voiceTag}>");
            }
        }

        private void InsertService_Click(object sender, RoutedEventArgs e)
        {
            if (cmbServiceTag.SelectedItem != null)
            {
                var selectedItem = (cmbServiceTag.SelectedItem as ComboBoxItem).Content.ToString();
                // Extract the service number from "service=1 (SAPI)" format
                var match = Regex.Match(selectedItem, @"service=(\d+)");
                if (match.Success)
                {
                    string serviceTag = $"<service={match.Groups[1].Value}>";
                    var caretPos = rtbTextContent.CaretPosition;
                    caretPos.InsertTextInRun(serviceTag);
                    LogMessage($"Inserted service tag: {serviceTag}");
                }
            }
            else
            {
                MessageBox.Show("Please select a service from the dropdown first.", "Insert Service",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                cmbServiceTag.Focus();
            }
        }

        private void InsertComment_Click(object sender, RoutedEventArgs e)
        {
            string commentText = txtCommentText.Text;

            if (string.IsNullOrWhiteSpace(commentText) || commentText == "Enter comment...")
            {
                MessageBox.Show("Please enter comment text first.", "Insert Comment",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                txtCommentText.Focus();
                return;
            }

            var caretPos = rtbTextContent.CaretPosition;

            if (commentText.Contains("\""))
            {
                string escapedText = commentText.Replace("\"", "\\\"");
                caretPos.InsertTextInRun($"<comment=\"{escapedText}\">");
            }
            else
            {
                caretPos.InsertTextInRun($"<comment={commentText}>");
            }

            txtCommentText.Text = "Enter comment...";
            txtCommentText.Foreground = Brushes.Gray;

            LogMessage($"Inserted comment: {commentText}");
        }

        // REPLACE the existing InsertSSML_Click method with this improved version
        // REPLACE both InsertSSML_Click and WrapSSML_Click methods with these updated versions

        private void InsertSSML_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSSMLTags.SelectedItem == null)
            {
                MessageBox.Show("Please select an SSML tag from the dropdown first.", "Insert SSML",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                cmbSSMLTags.Focus();
                return;
            }

            var selectedItem = cmbSSMLTags.SelectedItem as ComboBoxItem;
            // Get the actual tag from the Tag property
            string ssmlTag = selectedItem.Tag?.ToString() ?? selectedItem.Content.ToString();

            var caretPos = rtbTextContent.CaretPosition;

            // Check if it's a self-closing tag
            if (ssmlTag.Contains("/>"))
            {
                // Self-closing tag - just insert it
                caretPos.InsertTextInRun(ssmlTag);
                LogMessage($"Inserted SSML tag: {ssmlTag}");
            }
            else
            {
                // Opening/closing tag pair - insert both and position cursor between them
                var tagMatch = Regex.Match(ssmlTag, @"<(\w+(?:-\w+)?)[^>]*>");
                if (tagMatch.Success)
                {
                    var tagName = tagMatch.Groups[1].Value;
                    var openTag = tagMatch.Groups[0].Value;
                    var closeTag = $"</{tagName}>";

                    // Insert opening tag
                    caretPos.InsertTextInRun(openTag);

                    // Get position after opening tag
                    var middlePos = caretPos;

                    // Insert closing tag
                    caretPos.InsertTextInRun(closeTag);

                    // Position cursor between tags
                    rtbTextContent.CaretPosition = middlePos;

                    LogMessage($"Inserted SSML tags: {openTag}...{closeTag}");
                }
                else
                {
                    caretPos.InsertTextInRun(ssmlTag);
                }
            }

            rtbTextContent.Focus();
        }

        private void WrapSSML_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSSMLTags.SelectedItem == null)
            {
                MessageBox.Show("Please select an SSML tag from the dropdown first.", "Wrap with SSML",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                cmbSSMLTags.Focus();
                return;
            }

            var selection = rtbTextContent.Selection;
            if (selection.IsEmpty)
            {
                MessageBox.Show("Please select text to wrap with SSML tags.", "Wrap with SSML",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selectedText = selection.Text;
            var selectedItem = cmbSSMLTags.SelectedItem as ComboBoxItem;
            // Get the actual tag from the Tag property
            string ssmlTag = selectedItem.Tag?.ToString() ?? selectedItem.Content.ToString();

            if (ssmlTag.Contains("/>"))
            {
                MessageBox.Show("Cannot wrap text with a self-closing tag. Please select a tag with opening and closing elements.",
                    "Wrap with SSML", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tagMatch = Regex.Match(ssmlTag, @"<(\w+(?:-\w+)?)[^>]*>");
            if (tagMatch.Success)
            {
                var tagName = tagMatch.Groups[1].Value;
                var openTag = tagMatch.Groups[0].Value;
                var closeTag = $"</{tagName}>";

                string wrappedText = openTag + selectedText + closeTag;
                selection.Text = wrappedText;

                LogMessage($"Wrapped selection with {tagName} tags");
            }
            else
            {
                MessageBox.Show("Could not parse SSML tag format.", "Wrap with SSML",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Plugin System

        private void InitializePluginSystem()
        {
            try
            {
                _pluginManager = new PluginManager(this);
                _pluginManager.LoadPlugins();

                // Add plugins to Tools menu
                if (_pluginManager.LoadedPlugins.Count > 0)
                {
                    // Find the Tools menu
                    var toolsMenu = MainMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Header.ToString() == "Tools");

                    if (toolsMenu != null)
                    {
                        // Add separator before plugins
                        toolsMenu.Items.Add(new Separator());

                        // Add each plugin as menu item
                        foreach (var plugin in _pluginManager.LoadedPlugins)
                        {
                            var pluginMenuItem = new MenuItem
                            {
                                Header = $"{plugin.Icon} {plugin.Name}",
                                Tag = plugin
                            };

                            pluginMenuItem.Click += (s, e) =>
                            {
                                try
                                {
                                    var menuItem = s as MenuItem;
                                    var selectedPlugin = menuItem?.Tag as IPlugin;
                                    selectedPlugin?.Execute();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Plugin error: {ex.Message}", "Plugin Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                    LogMessage($"Plugin error: {ex.Message}");
                                }
                            };

                            toolsMenu.Items.Add(pluginMenuItem);
                        }

                        LogMessage($"Loaded {_pluginManager.LoadedPlugins.Count} plugin(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing plugin system: {ex.Message}");
            }
        }

        #endregion
        // Add these methods to MainWindow.xaml.cs
        // Create a new region or add to an existing one

        #region Tools

        private void StripCharacters_Click(object sender, RoutedEventArgs e)
        {
            var stripWindow = new Window
            {
                Title = "Strip/Ignore Characters",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize
            };

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Instructions
            var instructionText = new TextBlock
            {
                Text = "Enter characters or patterns to remove from your text. Each line is treated as a separate pattern.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                FontStyle = FontStyles.Italic
            };
            Grid.SetRow(instructionText, 0);

            // Quick presets
            var presetsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            var presetsLabel = new Label { Content = "Quick Presets:", FontWeight = FontWeights.Bold };
            presetsPanel.Children.Add(presetsLabel);

            var presets = new Dictionary<string, string[]>
            {
                { "Extra Spaces", new[] { "  ", "   ", "    " } },
                { "Quotes", new[] { "\"", "'" } },
                { "Brackets", new[] { "[", "]", "(", ")" } },
                { "Special Chars", new[] { "@", "#", "$", "%", "^", "&", "*" } },
                { "Line Numbers", new[] { @"^\d+\.\s*", @"^\d+\)\s*" } },
                { "Timestamps", new[] { @"\d{1,2}:\d{2}:\d{2}", @"\d{1,2}:\d{2}" } }
            };

            foreach (var preset in presets)
            {
                var btn = new Button
                {
                    Content = preset.Key,
                    Margin = new Thickness(5, 0, 0, 0),
                    Padding = new Thickness(8, 3, 8, 3)
                };
                btn.Click += (s, args) =>
                {
                    // Will be handled below
                };
                presetsPanel.Children.Add(btn);
            }

            Grid.SetRow(presetsPanel, 1);

            // Input area
            var inputLabel = new Label
            {
                Content = "Characters/Patterns to Remove (one per line):",
                FontWeight = FontWeights.SemiBold
            };

            var inputBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };

            var inputStack = new StackPanel();
            inputStack.Children.Add(inputLabel);
            inputStack.Children.Add(inputBox);
            Grid.SetRow(inputStack, 2);

            // Wire up presets
            foreach (var child in presetsPanel.Children)
            {
                if (child is Button btn)
                {
                    string presetName = btn.Content.ToString();
                    btn.Click += (s, args) =>
                    {
                        if (presets.ContainsKey(presetName))
                        {
                            inputBox.Text = string.Join("\n", presets[presetName]);
                        }
                    };
                }
            }

            // Options
            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var regexCheckBox = new CheckBox
            {
                Content = "Use Regular Expressions",
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var caseSensitiveCheckBox = new CheckBox
            {
                Content = "Case Sensitive",
                VerticalAlignment = VerticalAlignment.Center
            };

            optionsPanel.Children.Add(regexCheckBox);
            optionsPanel.Children.Add(caseSensitiveCheckBox);
            Grid.SetRow(optionsPanel, 3);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var previewButton = new Button
            {
                Content = "Preview",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            var applyButton = new Button
            {
                Content = "Apply",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            buttonPanel.Children.Add(previewButton);
            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 4);

            mainGrid.Children.Add(instructionText);
            mainGrid.Children.Add(presetsPanel);
            mainGrid.Children.Add(inputStack);
            mainGrid.Children.Add(optionsPanel);
            mainGrid.Children.Add(buttonPanel);

            stripWindow.Content = mainGrid;

            // Preview functionality
            previewButton.Click += (s, args) =>
            {
                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string originalText = textRange.Text;

                string[] patterns = inputBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (patterns.Length == 0)
                {
                    MessageBox.Show("Please enter at least one character or pattern to remove.", "Preview",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string previewText = originalText;
                int totalRemovals = 0;

                foreach (var pattern in patterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern)) continue;

                    int beforeLength = previewText.Length;

                    if (regexCheckBox.IsChecked == true)
                    {
                        try
                        {
                            var options = caseSensitiveCheckBox.IsChecked == true ?
                                RegexOptions.None : RegexOptions.IgnoreCase;
                            previewText = Regex.Replace(previewText, pattern, "", options);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Invalid regex pattern '{pattern}':\n{ex.Message}", "Regex Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        var comparison = caseSensitiveCheckBox.IsChecked == true ?
                            StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        previewText = previewText.Replace(pattern, "", comparison);
                    }

                    int removed = beforeLength - previewText.Length;
                    totalRemovals += removed;
                }

                MessageBox.Show(
                    $"Preview Results:\n\n" +
                    $"Original length: {originalText.Length} characters\n" +
                    $"New length: {previewText.Length} characters\n" +
                    $"Total removed: {totalRemovals} characters\n\n" +
                    $"First 200 characters of result:\n" +
                    $"{previewText.Substring(0, Math.Min(200, previewText.Length))}...",
                    "Strip Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };

            // Apply functionality
            applyButton.Click += (s, args) =>
            {
                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string originalText = textRange.Text;

                string[] patterns = inputBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (patterns.Length == 0)
                {
                    MessageBox.Show("Please enter at least one character or pattern to remove.", "Apply",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string newText = originalText;
                int totalRemovals = 0;

                foreach (var pattern in patterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern)) continue;

                    int beforeLength = newText.Length;

                    if (regexCheckBox.IsChecked == true)
                    {
                        try
                        {
                            var options = caseSensitiveCheckBox.IsChecked == true ?
                                RegexOptions.None : RegexOptions.IgnoreCase;
                            newText = Regex.Replace(newText, pattern, "", options);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Invalid regex pattern '{pattern}':\n{ex.Message}", "Regex Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        var comparison = caseSensitiveCheckBox.IsChecked == true ?
                            StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        newText = newText.Replace(pattern, "", comparison);
                    }

                    int removed = beforeLength - newText.Length;
                    totalRemovals += removed;
                }

                rtbTextContent.Document.Blocks.Clear();
                rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(newText)));

                LogMessage($"Stripped {totalRemovals} characters using {patterns.Length} pattern(s)");

                MessageBox.Show(
                    $"Successfully removed {totalRemovals} characters from text.",
                    "Strip Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                stripWindow.Close();
            };

            closeButton.Click += (s, args) => stripWindow.Close();

            stripWindow.ShowDialog();
        }

        private void CleanText_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will perform the following cleanup operations:\n\n" +
                "• Remove extra whitespace\n" +
                "• Normalize line breaks\n" +
                "• Trim leading/trailing spaces\n" +
                "• Remove empty lines\n\n" +
                "Continue?",
                "Clean Text",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string text = textRange.Text;

                // Clean operations
                text = Regex.Replace(text, @"[ \t]+", " "); // Multiple spaces to single
                text = Regex.Replace(text, @"^\s+", "", RegexOptions.Multiline); // Trim line starts
                text = Regex.Replace(text, @"\s+$", "", RegexOptions.Multiline); // Trim line ends
                text = Regex.Replace(text, @"\n{3,}", "\n\n"); // Max 2 consecutive newlines
                text = text.Trim();

                rtbTextContent.Document.Blocks.Clear();
                rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(text)));

                LogMessage("Text cleaned: whitespace normalized");
                MessageBox.Show("Text has been cleaned!", "Clean Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region ssml tools

        // Add this method to your MainWindow.xaml.cs
        // Put it in the #region SSML Tools or #region Help Dialogs section

        private void ShowColorLegend_Click(object sender, RoutedEventArgs e)
        {
            var legendWindow = new Window
            {
                Title = "Tag Colorization Legend",
                Width = 500,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(15) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "TTS3 Tag Color Scheme",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Use the '🎨 Colorize' button to highlight tags in your text editor.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15),
                FontStyle = FontStyles.Italic
            });

            // Control Tags Section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Control Tags:",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 5)
            });

            AddLegendItem(stackPanel, "Cyan", "Split", "<split>", "Divides output into separate files");
            AddLegendItem(stackPanel, "LightGreen", "Voice", "<voice=1>", "Selects voice within current service");
            AddLegendItem(stackPanel, "LightBlue", "Service", "<service=2>", "Switches TTS service");
            AddLegendItem(stackPanel, "Orange", "Label", "<label=5>", "Sets custom output file number");
            AddLegendItem(stackPanel, "Violet", "VID", "<vid=myself>", "Custom voice ID (ElevenLabs)");

            // SSML Tags Section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "SSML Tags:",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            });

            AddLegendItem(stackPanel, "Yellow", "SSML", "<emphasis>, <break>, <prosody>, etc.", "Standard SSML markup");

            // Comments Section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Other:",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            });

            AddLegendItem(stackPanel, "LightGray", "Comment", "<comment=note>", "Ignored during conversion");

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => legendWindow.Close();

            stackPanel.Children.Add(closeButton);

            scrollViewer.Content = stackPanel;
            legendWindow.Content = scrollViewer;

            legendWindow.ShowDialog();
        }

        private void AddLegendItem(StackPanel parent, string colorName, string tagType, string example, string description)
        {
            var itemPanel = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            // Color box
            var colorBox = new Border
            {
                Width = 20,
                Height = 20,
                Background = (Brush)new BrushConverter().ConvertFromString(colorName),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(colorBox, Dock.Left);
            itemPanel.Children.Add(colorBox);

            // Text info
            var textStack = new StackPanel();

            textStack.Children.Add(new TextBlock
            {
                Text = $"{tagType}: {example}",
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas")
            });

            textStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            itemPanel.Children.Add(textStack);
            parent.Children.Add(itemPanel);
        }

        // DIAGNOSTIC VERSION - Use this temporarily to see what's being highlighted
        // COMPLETE REPLACEMENT for HighlightSSML_Click
        // This version navigates the RichTextBox directly instead of using offsets

        private void HighlightSSML_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var document = rtbTextContent.Document;
                var fullRange = new TextRange(document.ContentStart, document.ContentEnd);

                // Clear all formatting first
                fullRange.ClearAllProperties();

                // Define all patterns with colors
                var tagPatterns = new List<(string name, string pattern, Brush bg, Brush fg)>
        {
            // Control tags
            ("split", @"<split>", Brushes.Cyan, Brushes.DarkRed),
            ("voice", @"<voice=\d+>", Brushes.LightGreen, Brushes.DarkGreen),
            ("service", @"<service=\d+>", Brushes.LightBlue, Brushes.DarkBlue),
            ("label", @"<label=\d+>", Brushes.Orange, Brushes.DarkOrange),
            ("vid", @"<vid=[^>]+>", Brushes.Violet, Brushes.Purple),
            ("comment", @"<comment\s*=\s*(?:""[^""]*""|[^>]+)>", Brushes.LightGray, Brushes.Gray),
            
            // SSML tags
            ("emphasis", @"</?emphasis(?:\s+[^>]*)?>", Brushes.Yellow, Brushes.DarkBlue),
            ("prosody", @"</?prosody(?:\s+[^>]*)?>", Brushes.Yellow, Brushes.DarkBlue),
            ("say-as", @"</?say-as(?:\s+[^>]*)?>", Brushes.Yellow, Brushes.DarkBlue),
            ("break", @"<break(?:\s+[^>]*)?/?>", Brushes.Yellow, Brushes.DarkBlue),
            ("sub", @"</?sub(?:\s+[^>]*)?>", Brushes.Yellow, Brushes.DarkBlue),
        };

                int totalCount = 0;
                var tagCounts = new Dictionary<string, int>();

                // Use a different approach: navigate through the document character by character
                TextPointer navigator = document.ContentStart;
                StringBuilder currentText = new StringBuilder();
                List<(int index, string tag, Brush bg, Brush fg)> foundTags = new List<(int, string, Brush, Brush)>();

                // Build up the text and find tag positions
                int charIndex = 0;
                while (navigator.CompareTo(document.ContentEnd) < 0)
                {
                    TextPointerContext context = navigator.GetPointerContext(LogicalDirection.Forward);

                    if (context == TextPointerContext.Text)
                    {
                        string textRun = navigator.GetTextInRun(LogicalDirection.Forward);
                        currentText.Append(textRun);
                        charIndex += textRun.Length;
                    }

                    navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
                    if (navigator == null) break;
                }

                string fullText = currentText.ToString();

                // Find all tags in the text
                foreach (var (name, pattern, bg, fg) in tagPatterns)
                {
                    var matches = Regex.Matches(fullText, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        foundTags.Add((match.Index, match.Value, bg, fg));

                        if (!tagCounts.ContainsKey(name))
                            tagCounts[name] = 0;
                        tagCounts[name]++;
                        totalCount++;
                    }
                }

                // Sort by position
                foundTags = foundTags.OrderBy(t => t.index).ToList();

                // Now colorize each tag by navigating to its position
                foreach (var (index, tag, bg, fg) in foundTags)
                {
                    // Navigate to the start position
                    navigator = document.ContentStart;
                    int currentIndex = 0;
                    TextPointer tagStart = null;
                    TextPointer tagEnd = null;

                    while (navigator != null && navigator.CompareTo(document.ContentEnd) < 0)
                    {
                        TextPointerContext context = navigator.GetPointerContext(LogicalDirection.Forward);

                        if (context == TextPointerContext.Text)
                        {
                            string textRun = navigator.GetTextInRun(LogicalDirection.Forward);
                            int runLength = textRun.Length;

                            // Check if our tag starts in this run
                            if (currentIndex <= index && index < currentIndex + runLength)
                            {
                                int offsetInRun = index - currentIndex;
                                tagStart = navigator.GetPositionAtOffset(offsetInRun);
                                tagEnd = tagStart.GetPositionAtOffset(tag.Length);
                                break;
                            }

                            currentIndex += runLength;
                        }

                        navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
                    }

                    // Apply formatting
                    if (tagStart != null && tagEnd != null)
                    {
                        var range = new TextRange(tagStart, tagEnd);
                        range.ApplyPropertyValue(TextElement.BackgroundProperty, bg);
                        range.ApplyPropertyValue(TextElement.ForegroundProperty, fg);
                        range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                    }
                }

                // Build summary
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"Colorized {totalCount} tags:");
                summary.AppendLine("━━━━━━━━━━━━━━━━━━━━━");

                if (tagCounts.ContainsKey("split")) summary.AppendLine($"🔷 Split: {tagCounts["split"]}");
                if (tagCounts.ContainsKey("voice")) summary.AppendLine($"🟢 Voice: {tagCounts["voice"]}");
                if (tagCounts.ContainsKey("service")) summary.AppendLine($"🔵 Service: {tagCounts["service"]}");
                if (tagCounts.ContainsKey("label")) summary.AppendLine($"🟠 Label: {tagCounts["label"]}");
                if (tagCounts.ContainsKey("vid")) summary.AppendLine($"🟣 VID: {tagCounts["vid"]}");

                var ssmlTags = new[] { "emphasis", "break", "prosody", "say-as", "sub" };
                var ssmlCount = tagCounts.Where(kv => ssmlTags.Contains(kv.Key)).Sum(kv => kv.Value);
                if (ssmlCount > 0) summary.AppendLine($"🟡 SSML: {ssmlCount}");

                if (tagCounts.ContainsKey("comment")) summary.AppendLine($"⚪ Comments: {tagCounts["comment"]}");

                LogMessage(summary.ToString());
            }
            catch (Exception ex)
            {
                LogMessage($"Error colorizing: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Colorization Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void HighlightPattern(string text, string pattern, Brush background, Brush foreground,
            FontWeight fontWeight, FontStyle fontStyle = default)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var start = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, match.Index);
                var end = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, match.Index + match.Length);

                if (start != null && end != null)
                {
                    var range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.BackgroundProperty, background);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, foreground);
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, fontWeight);

                    if (fontStyle != default)
                    {
                        range.ApplyPropertyValue(TextElement.FontStyleProperty, fontStyle);
                    }
                }
            }
        }

        private void ValidateSSML_Click(object sender, RoutedEventArgs e)
        {
            var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
            string text = textRange.Text;

            var validationResult = _ssmlValidationService.ValidateSSML(text);

            textRange.ClearAllProperties();

            if (validationResult.IsValid)
            {
                LogMessage("✓ SSML validation passed! No errors found.");
                MessageBox.Show("SSML validation successful!\n\nAll tags are properly formed and closed.",
                    "Validation Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                foreach (var error in validationResult.Errors)
                {
                    HighlightError(error.Position, error.Length);
                }

                var errorReport = new System.Text.StringBuilder();
                errorReport.AppendLine($"Found {validationResult.Errors.Count} error(s):\n");

                int errorNum = 1;
                foreach (var error in validationResult.Errors)
                {
                    errorReport.AppendLine($"{errorNum}. {error.Message}");
                    if (!string.IsNullOrEmpty(error.Context))
                    {
                        errorReport.AppendLine($"   Context: {error.Context}");
                    }
                    errorReport.AppendLine($"   Position: Character {error.Position}");
                    errorReport.AppendLine();
                    errorNum++;
                }

                LogMessage($"✗ SSML validation failed with {validationResult.Errors.Count} error(s)");

                MessageBox.Show(errorReport.ToString(), "SSML Validation Errors",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (validationResult.Warnings.Count > 0)
            {
                var warningText = new System.Text.StringBuilder();
                warningText.AppendLine("\nWarnings:");
                foreach (var warning in validationResult.Warnings)
                {
                    warningText.AppendLine($"⚠ {warning}");
                }
                LogMessage(warningText.ToString());
            }
        }

        private void HighlightError(int position, int length)
        {
            try
            {
                var start = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, position);
                var end = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, position + length);

                if (start != null && end != null)
                {
                    var range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Red);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.White);
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                }
            }
            catch { }
        }

        #endregion

        // CONTINUED IN NEXT PART...#region Voice Testing

        private async void TestVoice_Click(object sender, RoutedEventArgs e)
        {
            // Ensure current service is synchronized with dropdown selection
            if (cmbTTSEngine.SelectedIndex >= 0 && cmbTTSEngine.SelectedIndex < _ttsServices.Count)
            {
                _currentService = _ttsServices[cmbTTSEngine.SelectedIndex];
            }

            if (_currentService == null)
            {
                MessageBox.Show("Please select a TTS service first.", "No Service Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ensure a voice is selected
            if (cmbVoices.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a voice first.", "No Voice Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string testText = "Hello, this is a test of the selected voice.";
                string tempFile = Path.GetTempFileName();

                var settings = new ConversionSettings
                {
                    RateValue = sliderRate.Value,
                    VolumeValue = sliderVolume.Value,
                    OutputFormatIndex = 0
                };

                LogMessage($"Testing voice with {_currentService.GetType().Name}...");

                bool success = await _currentService.TestVoiceAsync(
                    testText, tempFile, cmbVoices.SelectedIndex, settings);

                if (success && File.Exists(tempFile + ".wav"))
                {
                    _playbackService.LoadPlaylist(new List<string> { tempFile + ".wav" });
                    _playbackService.Play(0);

                    LogMessage("Playing test audio...");
                }
                else
                {
                    MessageBox.Show("Failed to generate test audio.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage("Failed to generate test audio");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing voice: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Error testing voice: {ex.Message}");
            }
        }
        private void StopTest_Click(object sender, RoutedEventArgs e)
        {
            _playbackService.Stop();
        }

        private async void TestSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string testText;
                var selection = rtbTextContent.Selection;

                if (!selection.IsEmpty)
                {
                    testText = selection.Text;
                    LogMessage($"Testing selected text: {testText.Substring(0, Math.Min(50, testText.Length))}...");
                }
                else
                {
                    var caretPos = rtbTextContent.CaretPosition;
                    var start = caretPos.GetPositionAtOffset(-50) ?? rtbTextContent.Document.ContentStart;
                    var end = caretPos.GetPositionAtOffset(50) ?? rtbTextContent.Document.ContentEnd;
                    testText = new TextRange(start, end).Text;
                    LogMessage("Testing text around cursor position");
                }

                if (string.IsNullOrWhiteSpace(testText))
                {
                    MessageBox.Show("No text selected or around cursor to test.");
                    return;
                }

                StopTestSelection_Click(sender, e);

                btnStopTestSelection.IsEnabled = true;
                btnTestSelection.IsEnabled = false;

                // Remove control tags
                string cleanedText = testText;
                cleanedText = Regex.Replace(cleanedText, @"<split>", "", RegexOptions.IgnoreCase);
                cleanedText = Regex.Replace(cleanedText, @"<service=\d+>", "", RegexOptions.IgnoreCase);
                cleanedText = Regex.Replace(cleanedText, @"<voice=\d+>", "", RegexOptions.IgnoreCase);
                cleanedText = Regex.Replace(cleanedText, @"<vid=[^>]+>", "", RegexOptions.IgnoreCase);
                cleanedText = Regex.Replace(cleanedText, @"<label=\d+>", "", RegexOptions.IgnoreCase);
                cleanedText = _ssmlProcessingService.StripComments(cleanedText);
                cleanedText = cleanedText.Trim();

                if (string.IsNullOrWhiteSpace(cleanedText))
                {
                    MessageBox.Show("No content to test after removing control tags.");
                    btnStopTestSelection.IsEnabled = false;
                    btnTestSelection.IsEnabled = true;
                    return;
                }

                string tempFile = Path.GetTempFileName();

                var settings = new ConversionSettings
                {
                    RateValue = sliderRate.Value,
                    VolumeValue = sliderVolume.Value,
                    OutputFormatIndex = 0
                };

                bool success = await _currentService.TestVoiceAsync(
                    cleanedText, tempFile, cmbVoices.SelectedIndex, settings);

                if (success && File.Exists(tempFile + ".wav"))
                {
                    _playbackService.LoadPlaylist(new List<string> { tempFile + ".wav" });
                    _playbackService.Play(0);
                    _playbackService.PlaybackCompleted += (s, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            btnStopTestSelection.IsEnabled = false;
                            btnTestSelection.IsEnabled = true;
                        });
                    };
                }
                else
                {
                    btnStopTestSelection.IsEnabled = false;
                    btnTestSelection.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error testing selection: {ex.Message}");
                btnStopTestSelection.IsEnabled = false;
                btnTestSelection.IsEnabled = true;
            }
        }

        private void StopTestSelection_Click(object sender, RoutedEventArgs e)
        {
            _playbackService.Stop();
            btnStopTestSelection.IsEnabled = false;
            btnTestSelection.IsEnabled = true;
        }


        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                string outputPath = folderDialog.SelectedPath;
                _lastOutputFolder = outputPath;

                string text = new TextRange(rtbTextContent.Document.ContentStart,
                    rtbTextContent.Document.ContentEnd).Text;

                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;
                txtStatus.Text = "Converting...";
                btnConvert.IsEnabled = false;

                var settings = new ConversionSettings
                {
                    EngineIndex = cmbTTSEngine.SelectedIndex,
                    OutputFormatIndex = cmbOutputFormat.SelectedIndex,
                    RateValue = sliderRate.Value,
                    VolumeValue = sliderVolume.Value,
                    OutputPath = outputPath,
                    RetainUnmergedFiles = chkRetainUnmergedFiles.IsChecked ?? false
                };

                await Task.Run(async () => await ProcessConversion(text, settings));

                progressBar.Visibility = Visibility.Collapsed;
                progressBar.IsIndeterminate = false;
                txtStatus.Text = "Conversion complete!";
                btnConvert.IsEnabled = true;

                UpdatePlaybackControls();

                MessageBox.Show($"Conversion completed successfully!\n\nCreated {_createdAudioFiles.Count} audio file(s).",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                progressBar.Visibility = Visibility.Collapsed;
                progressBar.IsIndeterminate = false;
                txtStatus.Text = "Error";
                btnConvert.IsEnabled = true;
                MessageBox.Show($"Error during conversion: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ProcessConversion(string text, ConversionSettings settings)
        {
            _createdAudioFiles.Clear();

            var segments = _ssmlProcessingService.ProcessTextSegments(text);

            Dispatcher.Invoke(() => LogMessage($"Processing {segments.Count} segments"));

            var segmentGroups = segments.GroupBy(s => s.SplitIndex).OrderBy(g => g.Key).ToList();

            for (int groupIndex = 0; groupIndex < segmentGroups.Count; groupIndex++)
            {
                var group = segmentGroups[groupIndex];
                var groupSegments = group.ToList();

                int outputNumber = group.Key;
                string baseFileName = $"output_{outputNumber:D3}";

                bool hasLabel = groupSegments[0].LabelNumber.HasValue;
                if (hasLabel)
                {
                    Dispatcher.Invoke(() => LogMessage($"Using custom label: output_{outputNumber:D3}"));
                }

                var tempFiles = new List<string>();

                for (int subIndex = 0; subIndex < groupSegments.Count; subIndex++)
                {
                    var segment = groupSegments[subIndex];

                    string subFileName = groupSegments.Count > 1
                        ? $"{baseFileName}{(char)('a' + subIndex)}"
                        : baseFileName;

                    string outputFile = Path.Combine(settings.OutputPath, subFileName);

                    int serviceToUse = segment.ServiceIndex >= 0 ? segment.ServiceIndex : settings.EngineIndex;

                    Dispatcher.Invoke(() =>
                        LogMessage($"Segment {outputNumber}.{subIndex + 1}: File={subFileName}, Service={serviceToUse + 1}, Voice={segment.VoiceIndex}, Length={segment.Text.Length} chars"));

                    try
                    {
                        var service = _ttsServices[serviceToUse];
                        bool success = await service.ConvertToAudioAsync(segment, outputFile, settings);

                        if (success)
                        {
                            string fullPath = outputFile + settings.FileExtension;

                            if (settings.OutputFormatIndex == 1 && File.Exists(outputFile + ".wav"))
                            {
                                AudioConverter.ConvertWavToMp3(outputFile + ".wav", outputFile + ".mp3");
                                File.Delete(outputFile + ".wav");
                            }

                            if (File.Exists(fullPath))
                            {
                                var fileInfo = new FileInfo(fullPath);
                                tempFiles.Add(fullPath);
                                Dispatcher.Invoke(() => LogMessage($"✓ Created: {fullPath} ({fileInfo.Length} bytes)"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => LogMessage($"✗ ERROR converting segment {outputNumber}.{subIndex + 1}: {ex.Message}"));
                    }
                }

                string finalFile = Path.Combine(settings.OutputPath, baseFileName + settings.FileExtension);

                if (tempFiles.Count > 1)
                {
                    Dispatcher.Invoke(() => LogMessage($"Merging {tempFiles.Count} sub-files into {baseFileName}..."));

                    await _mergeService.MergeAudioFilesAsync(tempFiles, finalFile);

                    Dispatcher.Invoke(() => LogMessage($"Merged into: {finalFile}"));

                    if (!settings.RetainUnmergedFiles)
                    {
                        foreach (var tempFile in tempFiles)
                        {
                            _fileService.DeleteFile(tempFile);
                            Dispatcher.Invoke(() => LogMessage($"Deleted temp file: {Path.GetFileName(tempFile)}"));
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => LogMessage($"Retained {tempFiles.Count} sub-files"));
                    }
                }
                else if (tempFiles.Count == 1 && tempFiles[0] != finalFile)
                {
                    File.Move(tempFiles[0], finalFile);
                }

                if (File.Exists(finalFile))
                {
                    _createdAudioFiles.Add(finalFile);
                }
            }

            Dispatcher.Invoke(() => LogMessage($"All {segmentGroups.Count} files converted successfully!"));
        }


        private void UpdatePlaybackControls()
        {
            if (_createdAudioFiles.Count > 0)
            {
                cmbCreatedFiles.Items.Clear();
                foreach (var file in _createdAudioFiles)
                {
                    cmbCreatedFiles.Items.Add(Path.GetFileName(file));
                }

                cmbCreatedFiles.SelectedIndex = 0;
                cmbCreatedFiles.IsEnabled = true;
                btnPlayPause.IsEnabled = true;
                btnStopPlayback.IsEnabled = true;
                btnOpenFolder.IsEnabled = true;

                if (_createdAudioFiles.Count > 1)
                {
                    btnPlayNext.IsEnabled = true;
                    btnPlayPrevious.IsEnabled = false;
                }

                _playbackService.LoadPlaylist(_createdAudioFiles);
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.IsPlaying)
            {
                _playbackService.Pause();
                btnPlayPause.Content = new TextBlock { Text = "▶ Play" };
                _playbackTimer.Stop();
            }
            else if (_playbackService.IsPaused)
            {
                _playbackService.Resume();
                btnPlayPause.Content = new TextBlock { Text = "⏸ Pause" };
                _playbackTimer.Start();
            }
            else
            {
                if (cmbCreatedFiles.SelectedIndex >= 0)
                {
                    _playbackService.Play(cmbCreatedFiles.SelectedIndex);
                    btnPlayPause.Content = new TextBlock { Text = "⏸ Pause" };
                    _playbackTimer.Start();
                }
            }
        }

        private void PlayPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.CurrentIndex > 0)
            {
                _playbackService.PlayPrevious();
                cmbCreatedFiles.SelectedIndex = _playbackService.CurrentIndex;
            }
        }

        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.CurrentIndex < _createdAudioFiles.Count - 1)
            {
                _playbackService.PlayNext();
                cmbCreatedFiles.SelectedIndex = _playbackService.CurrentIndex;
            }
        }

        private void StopPlayback_Click(object sender, RoutedEventArgs e)
        {
            _playbackService.Stop();
            _playbackTimer.Stop();
            btnPlayPause.Content = new TextBlock { Text = "▶ Play" };
            playbackProgress.Value = 0;
            txtPlaybackTime.Text = "00:00 / 00:00";
            txtCurrentFile.Text = "Playback stopped";
        }

        private void CreatedFile_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCreatedFiles.SelectedIndex >= 0 && cmbCreatedFiles.SelectedIndex < _createdAudioFiles.Count)
            {
                txtCurrentFile.Text = Path.GetFileName(_createdAudioFiles[cmbCreatedFiles.SelectedIndex]);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastOutputFolder) && Directory.Exists(_lastOutputFolder))
            {
                _fileService.OpenFolderInExplorer(_lastOutputFolder);
            }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_playbackService.IsPlaying)
            {
                var currentTime = _playbackService.CurrentPosition;
                var totalTime = _playbackService.TotalDuration;

                playbackProgress.Maximum = totalTime.TotalSeconds;
                playbackProgress.Value = currentTime.TotalSeconds;

                txtPlaybackTime.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
            }
        }

        private void PlaybackService_PlaybackStateChanged(object sender, PlaybackStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsPlaying)
                {
                    btnPlayPause.Content = new TextBlock { Text = "⏸ Pause" };
                    txtCurrentFile.Text = $"Playing: {Path.GetFileName(_playbackService.CurrentFile)}";
                    _playbackTimer.Start();

                    btnPlayPrevious.IsEnabled = _playbackService.CurrentIndex > 0;
                    btnPlayNext.IsEnabled = _playbackService.CurrentIndex < _createdAudioFiles.Count - 1;
                }
                else if (e.IsPaused)
                {
                    btnPlayPause.Content = new TextBlock { Text = "▶ Play" };
                    _playbackTimer.Stop();
                }
                else
                {
                    btnPlayPause.Content = new TextBlock { Text = "▶ Play" };
                    _playbackTimer.Stop();
                }
            });
        }

        private void PlaybackService_PlaybackCompleted(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_playbackService.CurrentIndex < _createdAudioFiles.Count - 1)
                {
                    _playbackService.PlayNext();
                    cmbCreatedFiles.SelectedIndex = _playbackService.CurrentIndex;
                }
                else
                {
                    StopPlayback_Click(sender, new RoutedEventArgs());
                    txtCurrentFile.Text = "Playback complete";
                }
            });
        }
        // Add these methods to MainWindow.xaml.cs in the existing #region or create #region Help

        #region Help Dialogs

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new Window
            {
                Title = "Quick Start Guide",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer { Padding = new Thickness(20) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "TTS3 Quick Start Guide",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            });

            AddHelpSection(stack, "1. Load Your Text",
                "• Click 'Open Text File' or use Ctrl+O\n" +
                "• Paste directly into the editor\n" +
                "• Start typing your script");

            AddHelpSection(stack, "2. Choose Your TTS Service",
                "• Windows SAPI: Built-in, no setup needed\n" +
                "• Google Cloud TTS: Requires API key (🔑 Set Credentials)\n" +
                "• AWS Polly: Requires credentials (🔑 Set Credentials)\n" +
                "• ElevenLabs: Requires API key (🔑 Set Credentials)");

            AddHelpSection(stack, "3. Add Control Tags",
                "• <split> - Create separate audio files\n" +
                "• <voice=1> - Switch voices (1, 2, 3, 4)\n" +
                "• <service=2> - Switch TTS service mid-text\n" +
                "• <comment=note> - Add notes (ignored in audio)");

            AddHelpSection(stack, "4. Add SSML for Speech Control",
                "• <emphasis level=\"strong\">Important</emphasis>\n" +
                "• <break time=\"1s\"/> - Add pauses\n" +
                "• <prosody rate=\"slow\">Slow speech</prosody>\n" +
                "• Use the SSML dropdown to insert these easily!");

            AddHelpSection(stack, "5. Preview & Validate",
                "• Select text and click '🔊 Preview' to test\n" +
                "• Click '🎨 Colorize' to highlight all tags\n" +
                "• Click '✓ Validate' to check for errors");

            AddHelpSection(stack, "6. Convert to Audio",
                "• Click 'Convert to Audio' or press F5\n" +
                "• Choose output folder\n" +
                "• Wait for conversion to complete\n" +
                "• Use playback controls to listen");

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => helpWindow.Close();
            stack.Children.Add(closeButton);

            scrollViewer.Content = stack;
            helpWindow.Content = scrollViewer;
            helpWindow.ShowDialog();
        }

        private void TagReference_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new Window
            {
                Title = "Tag Reference",
                Width = 750,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer { Padding = new Thickness(20) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "TTS3 Control Tags Reference",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            });

            AddTagReference(stack, "<split>", "Cyan",
                "Splits output into separate audio files",
                "Chapter 1<split>Chapter 2<split>Chapter 3",
                "Creates: output_001.wav, output_002.wav, output_003.wav");

            AddTagReference(stack, "<voice=N>", "Light Green",
                "Switches to voice number N (1-4) within current service",
                "<voice=1>First speaker<voice=2>Second speaker",
                "Each voice uses a different voice from the selected service");

            AddTagReference(stack, "<service=N>", "Light Blue",
                "Switches TTS service: 1=SAPI, 2=Google, 3=AWS, 4=ElevenLabs",
                "Start with SAPI<service=3>Now using AWS Polly",
                "Different services have different voice quality and options");

            AddTagReference(stack, "<label=N>", "Orange",
                "Sets custom output file number (skips sequential numbering)",
                "<label=10>This becomes output_010.wav",
                "Useful for organizing non-sequential content");

            AddTagReference(stack, "<vid=name>", "Violet",
                "Uses custom voice ID (primarily for ElevenLabs)",
                "<vid=rachel>Use Rachel's voice<vid=adam>Use Adam's voice",
                "Must be valid voice ID from your ElevenLabs account");

            AddTagReference(stack, "<comment=text>", "Gray",
                "Adds comment/note that's ignored during conversion",
                "<comment=TODO: Fix this>Regular text continues",
                "Or use quotes: <comment=\"This is a note\">");

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => helpWindow.Close();
            stack.Children.Add(closeButton);

            scrollViewer.Content = stack;
            helpWindow.Content = scrollViewer;
            helpWindow.ShowDialog();
        }

        private void SSMLReference_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new Window
            {
                Title = "SSML Reference",
                Width = 750,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer { Padding = new Thickness(20) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "SSML Tags Reference",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Speech Synthesis Markup Language (SSML) lets you control speech output",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15),
                FontStyle = FontStyles.Italic
            });

            AddSSMLReference(stack, "<emphasis level=\"strong|moderate\">",
                "Emphasizes text with specified strength",
                "<emphasis level=\"strong\">Very important</emphasis>");

            AddSSMLReference(stack, "<break time=\"Xs|Xms\"/>",
                "Inserts pause of specified duration",
                "First sentence<break time=\"1s\"/>Second sentence");

            AddSSMLReference(stack, "<prosody rate=\"slow|medium|fast|X%\">",
                "Controls speech rate",
                "<prosody rate=\"slow\">Speak slowly</prosody>");

            AddSSMLReference(stack, "<prosody pitch=\"+Xst|-Xst\">",
                "Adjusts pitch (st = semitones)",
                "<prosody pitch=\"+5st\">Higher pitch</prosody>");

            AddSSMLReference(stack, "<prosody volume=\"loud|soft|X%\">",
                "Adjusts volume level",
                "<prosody volume=\"loud\">Louder speech</prosody>");

            AddSSMLReference(stack, "<say-as interpret-as=\"type\">",
                "Controls how text is spoken\n" +
                "Types: cardinal, ordinal, date, time, telephone, spell-out",
                "<say-as interpret-as=\"cardinal\">12345</say-as>");

            AddSSMLReference(stack, "<sub alias=\"text\">",
                "Substitutes spoken text",
                "<sub alias=\"World Wide Web\">WWW</sub>");

            stack.Children.Add(new TextBlock
            {
                Text = "⚠ Note: Not all TTS services support all SSML tags. Test with your chosen service.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 15, 0, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.OrangeRed
            });

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => helpWindow.Close();
            stack.Children.Add(closeButton);

            scrollViewer.Content = stack;
            helpWindow.Content = scrollViewer;
            helpWindow.ShowDialog();
        }

        private void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new Window
            {
                Title = "Keyboard Shortcuts",
                Width = 550,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer { Padding = new Thickness(20) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Keyboard Shortcuts",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var shortcuts = new[]
            {
        ("Ctrl+O", "Open text file"),
        ("Ctrl+S", "Save text file"),
        ("Ctrl+F", "Find"),
        ("Ctrl+H", "Find and Replace"),
        ("F5", "Convert to Audio"),
        ("Space", "Play/Pause (when not in editor)"),
        ("Ctrl+Shift+C", "Colorize tags"),
        ("Esc", "Close dialogs")
    };

            foreach (var (key, description) in shortcuts)
            {
                var panel = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };

                var keyBlock = new TextBlock
                {
                    Text = key,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Width = 150
                };
                DockPanel.SetDock(keyBlock, Dock.Left);

                var descBlock = new TextBlock
                {
                    Text = description
                };

                panel.Children.Add(keyBlock);
                panel.Children.Add(descBlock);
                stack.Children.Add(panel);
            }

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => helpWindow.Close();
            stack.Children.Add(closeButton);

            scrollViewer.Content = stack;
            helpWindow.Content = scrollViewer;
            helpWindow.ShowDialog();
        }

        // Helper methods for formatting help content
        private void AddHelpSection(StackPanel parent, string title, string content)
        {
            parent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            });

            parent.Children.Add(new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 0, 0, 0),
                FontFamily = new FontFamily("Segoe UI")
            });
        }

        private void AddTagReference(StackPanel parent, string tag, string color,
            string description, string example, string note)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };

            var stack = new StackPanel();

            var headerPanel = new DockPanel();
            headerPanel.Children.Add(new TextBlock
            {
                Text = tag,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });

            var colorLabel = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color),
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(10, 0, 0, 0)
            };
            DockPanel.SetDock(colorLabel, Dock.Right);
            colorLabel.Child = new TextBlock { Text = color, FontSize = 10 };
            headerPanel.Children.Add(colorLabel);

            stack.Children.Add(headerPanel);

            stack.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Example:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 2)
            });

            stack.Children.Add(new TextBlock
            {
                Text = example,
                FontFamily = new FontFamily("Consolas"),
                Background = Brushes.White,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 5)
            });

            stack.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray
            });

            border.Child = stack;
            parent.Children.Add(border);
        }

        private void AddSSMLReference(StackPanel parent, string tag, string description, string example)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Yellow,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 8, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 240))
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = tag,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkBlue
            });

            stack.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new TextBlock
            {
                Text = example,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = Brushes.DarkGreen,
                Background = Brushes.White,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 5, 0, 0)
            });

            border.Child = stack;
            parent.Children.Add(border);
        }

        #endregion

        // CONTINUED IN PART 3...#region Find and Replace

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            var findWindow = new Window
            {
                Title = "Find",
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var findLabel = new Label { Content = "Find what:", Margin = new Thickness(10, 10, 10, 0) };
            var findTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25,
                FontFamily = new FontFamily("Consolas")
            };

            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 0, 10, 10)
            };

            var matchCaseCheckBox = new CheckBox
            {
                Content = "Match case",
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var highlightAllCheckBox = new CheckBox
            {
                Content = "Highlight all",
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };

            optionsPanel.Children.Add(matchCaseCheckBox);
            optionsPanel.Children.Add(highlightAllCheckBox);

            var statsLabel = new Label
            {
                Content = "",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var findNextButton = new Button
            {
                Content = "Find Next",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            var clearHighlightsButton = new Button
            {
                Content = "Clear Highlights",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            TextPointer currentFindPosition = null;

            Func<TextPointer, string, bool, TextRange> findTextInRange = (startPosition, searchText, matchCase) =>
            {
                var navigator = startPosition;

                while (navigator != null && navigator.CompareTo(rtbTextContent.Document.ContentEnd) < 0)
                {
                    if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                    {
                        string textRun = navigator.GetTextInRun(LogicalDirection.Forward);

                        StringComparison comparison = matchCase ?
                            StringComparison.Ordinal :
                            StringComparison.OrdinalIgnoreCase;

                        int index = textRun.IndexOf(searchText, comparison);

                        if (index >= 0)
                        {
                            var start = navigator.GetPositionAtOffset(index);
                            var end = start.GetPositionAtOffset(searchText.Length);
                            return new TextRange(start, end);
                        }
                    }

                    navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
                }

                return null;
            };

            Action highlightAll = () =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText)) return;

                var fullRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                fullRange.ClearAllProperties();

                bool matchCase = matchCaseCheckBox.IsChecked == true;
                int highlightCount = 0;

                TextPointer position = rtbTextContent.Document.ContentStart;

                while (position != null)
                {
                    TextRange foundRange = findTextInRange(position, searchText, matchCase);

                    if (foundRange != null)
                    {
                        foundRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                        foundRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
                        highlightCount++;
                        position = foundRange.End;
                    }
                    else
                    {
                        break;
                    }
                }

                statsLabel.Content = highlightCount > 0 ?
                    $"Highlighted {highlightCount} occurrence(s)" :
                    "No occurrences found";

                currentFindPosition = null;
            };

            findNextButton.Click += (s, args) =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText)) return;

                bool matchCase = matchCaseCheckBox.IsChecked == true;

                TextPointer startPos = currentFindPosition ?? rtbTextContent.Document.ContentStart;

                TextRange foundRange = findTextInRange(startPos, searchText, matchCase);

                if (foundRange == null && currentFindPosition != null)
                {
                    foundRange = findTextInRange(rtbTextContent.Document.ContentStart, searchText, matchCase);
                }

                if (foundRange != null)
                {
                    var fullRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                    fullRange.ApplyPropertyValue(TextElement.BackgroundProperty, null);

                    if (highlightAllCheckBox.IsChecked == true)
                    {
                        TextPointer position = rtbTextContent.Document.ContentStart;
                        while (position != null)
                        {
                            TextRange yellowRange = findTextInRange(position, searchText, matchCase);
                            if (yellowRange != null)
                            {
                                yellowRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                                yellowRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
                                position = yellowRange.End;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    foundRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Orange);
                    foundRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);

                    rtbTextContent.Selection.Select(foundRange.Start, foundRange.End);
                    rtbTextContent.Focus();

                    var rect = foundRange.Start.GetCharacterRect(LogicalDirection.Forward);
                    rtbTextContent.ScrollToVerticalOffset(rect.Top);

                    currentFindPosition = foundRange.End;
                }
                else
                {
                    MessageBox.Show("No more occurrences found.", "Find", MessageBoxButton.OK, MessageBoxImage.Information);
                    currentFindPosition = null;
                }
            };

            clearHighlightsButton.Click += (s, args) =>
            {
                var fullRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                fullRange.ClearAllProperties();
                statsLabel.Content = "Highlights cleared";
                currentFindPosition = null;
            };

            closeButton.Click += (s, args) => findWindow.Close();

            findTextBox.TextChanged += (s, args) =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText))
                {
                    var fullRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                    fullRange.ClearAllProperties();
                    statsLabel.Content = "";
                    currentFindPosition = null;
                    return;
                }

                if (highlightAllCheckBox.IsChecked == true)
                {
                    highlightAll();
                }
            };

            matchCaseCheckBox.Checked += (s, args) =>
            {
                if (highlightAllCheckBox.IsChecked == true && !string.IsNullOrEmpty(findTextBox.Text))
                {
                    highlightAll();
                }
                currentFindPosition = null;
            };

            matchCaseCheckBox.Unchecked += (s, args) =>
            {
                if (highlightAllCheckBox.IsChecked == true && !string.IsNullOrEmpty(findTextBox.Text))
                {
                    highlightAll();
                }
                currentFindPosition = null;
            };

            highlightAllCheckBox.Checked += (s, args) =>
            {
                if (!string.IsNullOrEmpty(findTextBox.Text))
                {
                    highlightAll();
                }
            };

            highlightAllCheckBox.Unchecked += (s, args) =>
            {
                var fullRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                fullRange.ClearAllProperties();
                currentFindPosition = null;
            };

            Grid.SetRow(findLabel, 0);
            Grid.SetRow(findTextBox, 1);
            Grid.SetRow(optionsPanel, 2);
            Grid.SetRow(statsLabel, 3);
            Grid.SetRow(buttonPanel, 4);

            grid.Children.Add(findLabel);
            grid.Children.Add(findTextBox);
            grid.Children.Add(optionsPanel);
            grid.Children.Add(statsLabel);
            grid.Children.Add(buttonPanel);

            buttonPanel.Children.Add(findNextButton);
            buttonPanel.Children.Add(clearHighlightsButton);
            buttonPanel.Children.Add(closeButton);

            findWindow.Content = grid;

            findWindow.Loaded += (s, args) =>
            {
                findTextBox.Focus();

                if (!rtbTextContent.Selection.IsEmpty)
                {
                    findTextBox.Text = rtbTextContent.Selection.Text;
                    findTextBox.SelectAll();
                }
            };

            findWindow.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    findWindow.Close();
                }
                else if (args.Key == Key.F3 || args.Key == Key.Enter)
                {
                    findNextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    args.Handled = true;
                }
            };

            findWindow.ShowDialog();
        }

        private void FindReplace_Click(object sender, RoutedEventArgs e)
        {
            var findReplaceWindow = new Window
            {
                Title = "Find and Replace",
                Width = 500,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var findLabel = new Label { Content = "Find what:", Margin = new Thickness(10, 10, 10, 0) };
            var findTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25,
                FontFamily = new FontFamily("Consolas")
            };

            var replaceLabel = new Label { Content = "Replace with:", Margin = new Thickness(10, 0, 10, 0) };
            var replaceTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25,
                FontFamily = new FontFamily("Consolas")
            };

            var quickLabel = new Label { Content = "Quick replacements:", Margin = new Thickness(10, 0, 10, 0) };
            var quickReplacementsCombo = new ComboBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25
            };

            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "Select a quick replacement..." });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "Slide → <split>" });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "[pause] → <break time=\"1s\"/>" });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "[emphasis] → <emphasis level=\"strong\">" });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "[/emphasis] → </emphasis>" });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "[voice1] → <voice=1>" });
            quickReplacementsCombo.Items.Add(new ComboBoxItem { Content = "[voice2] → <voice=2>" });

            quickReplacementsCombo.SelectedIndex = 0;
            quickReplacementsCombo.SelectionChanged += (s, args) =>
            {
                if (quickReplacementsCombo.SelectedIndex > 0)
                {
                    var selectedItem = (quickReplacementsCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                    if (!string.IsNullOrEmpty(selectedItem) && selectedItem.Contains(" → "))
                    {
                        var parts = selectedItem.Split(new[] { " → " }, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            findTextBox.Text = parts[0];
                            replaceTextBox.Text = parts[1];
                        }
                    }
                }
            };

            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 0, 10, 10)
            };

            var matchCaseCheckBox = new CheckBox
            {
                Content = "Match case",
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            optionsPanel.Children.Add(matchCaseCheckBox);

            var statsLabel = new Label
            {
                Content = "",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var findNextButton = new Button
            {
                Content = "Find Next",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            var replaceButton = new Button
            {
                Content = "Replace",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            var replaceAllButton = new Button
            {
                Content = "Replace All",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            TextPointer currentFindPosition = rtbTextContent.Document.ContentStart;

            findNextButton.Click += (s, args) =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText)) return;

                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string documentText = textRange.Text;

                var searchStart = TextPointerHelper.GetOffsetFromStart(rtbTextContent.Document.ContentStart, currentFindPosition);

                StringComparison comparison = matchCaseCheckBox.IsChecked == true ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                int index = documentText.IndexOf(searchText, Math.Abs(searchStart), comparison);

                if (index == -1 && searchStart != 0)
                {
                    index = documentText.IndexOf(searchText, 0, comparison);
                }

                if (index >= 0)
                {
                    var start = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, index);
                    var end = TextPointerHelper.GetTextPointerAtOffset(rtbTextContent.Document.ContentStart, index + searchText.Length);

                    if (start != null && end != null)
                    {
                        rtbTextContent.Selection.Select(start, end);
                        rtbTextContent.Focus();
                        currentFindPosition = end;
                    }
                }
                else
                {
                    MessageBox.Show("No more occurrences found.", "Find", MessageBoxButton.OK, MessageBoxImage.Information);
                    currentFindPosition = rtbTextContent.Document.ContentStart;
                }
            };

            replaceButton.Click += (s, args) =>
            {
                if (!rtbTextContent.Selection.IsEmpty && rtbTextContent.Selection.Text == findTextBox.Text)
                {
                    rtbTextContent.Selection.Text = replaceTextBox.Text;
                }

                findNextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            replaceAllButton.Click += (s, args) =>
            {
                string findText = findTextBox.Text;
                string replaceText = replaceTextBox.Text;

                if (string.IsNullOrEmpty(findText))
                {
                    MessageBox.Show("Please enter text to find.", "Replace All", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string content = textRange.Text;

                StringComparison comparison = matchCaseCheckBox.IsChecked == true ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                int count = 0;
                int index = 0;

                while ((index = content.IndexOf(findText, index, comparison)) != -1)
                {
                    count++;
                    index += findText.Length;
                }

                if (count == 0)
                {
                    MessageBox.Show("No occurrences found.", "Replace All", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Replace {count} occurrence(s) of '{findText}' with '{replaceText}'?",
                    "Confirm Replace All",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (matchCaseCheckBox.IsChecked == true)
                    {
                        content = content.Replace(findText, replaceText);
                    }
                    else
                    {
                        content = Regex.Replace(content,
                            Regex.Escape(findText),
                            replaceText.Replace("$", "$$"),
                            RegexOptions.IgnoreCase);
                    }

                    rtbTextContent.Document.Blocks.Clear();
                    rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(content)));

                    statsLabel.Content = $"Replaced {count} occurrence(s)";
                    LogMessage($"Replaced {count} occurrence(s) of '{findText}' with '{replaceText}'");
                }
            };

            closeButton.Click += (s, args) => findReplaceWindow.Close();

            findTextBox.TextChanged += (s, args) =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText))
                {
                    statsLabel.Content = "";
                    return;
                }

                var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
                string content = textRange.Text;

                StringComparison comparison = matchCaseCheckBox.IsChecked == true ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                int count = 0;
                int index = 0;

                while ((index = content.IndexOf(searchText, index, comparison)) != -1)
                {
                    count++;
                    index += searchText.Length;
                }

                statsLabel.Content = count > 0 ? $"Found {count} occurrence(s)" : "No occurrences found";
            };

            Grid.SetRow(findLabel, 0);
            Grid.SetRow(findTextBox, 1);
            Grid.SetRow(replaceLabel, 2);
            Grid.SetRow(replaceTextBox, 3);
            Grid.SetRow(quickLabel, 4);
            Grid.SetRow(quickReplacementsCombo, 5);
            Grid.SetRow(optionsPanel, 6);
            Grid.SetRow(statsLabel, 7);
            Grid.SetRow(buttonPanel, 8);

            grid.Children.Add(findLabel);
            grid.Children.Add(findTextBox);
            grid.Children.Add(replaceLabel);
            grid.Children.Add(replaceTextBox);
            grid.Children.Add(quickLabel);
            grid.Children.Add(quickReplacementsCombo);
            grid.Children.Add(optionsPanel);
            grid.Children.Add(statsLabel);
            grid.Children.Add(buttonPanel);

            buttonPanel.Children.Add(findNextButton);
            buttonPanel.Children.Add(replaceButton);
            buttonPanel.Children.Add(replaceAllButton);
            buttonPanel.Children.Add(closeButton);

            findReplaceWindow.Content = grid;

            findReplaceWindow.Loaded += (s, args) =>
            {
                findTextBox.Focus();

                if (!rtbTextContent.Selection.IsEmpty)
                {
                    findTextBox.Text = rtbTextContent.Selection.Text;
                    findTextBox.SelectAll();
                }
            };

            findReplaceWindow.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    findReplaceWindow.Close();
                }
            };

            findReplaceWindow.ShowDialog();
        }


        private void AudioBrowser_Click(object sender, RoutedEventArgs e)
        {
            var browserWindow = new Window
            {
                Title = "Audio File Browser",
                Width = 700,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top panel - Folder selection
            var topPanel = new DockPanel { Margin = new Thickness(10) };

            var selectFolderButton = new Button
            {
                Content = "Select Folder...",
                Width = 120,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(selectFolderButton, Dock.Left);

            var currentFolderLabel = new TextBlock
            {
                Text = "No folder selected",
                VerticalAlignment = VerticalAlignment.Center,
                FontStyle = FontStyles.Italic,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            topPanel.Children.Add(selectFolderButton);
            topPanel.Children.Add(currentFolderLabel);

            // File list
            var listBox = new ListBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                FontFamily = new FontFamily("Consolas"),
                SelectionMode = SelectionMode.Extended
            };

            // Playback controls
            var playbackPanel = new Grid { Margin = new Thickness(10) };
            playbackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            playbackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            playbackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var controlButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var playButton = new Button
            {
                Content = "▶ Play",
                Width = 80,
                Height = 35,
                Margin = new Thickness(0, 0, 5, 0),
                IsEnabled = false
            };

            var stopButton = new Button
            {
                Content = "⏹ Stop",
                Width = 80,
                Height = 35,
                Margin = new Thickness(0, 0, 5, 0),
                IsEnabled = false
            };

            var autoPlayCheckBox = new CheckBox
            {
                Content = "Auto-play next",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                IsChecked = false
            };

            controlButtonsPanel.Children.Add(playButton);
            controlButtonsPanel.Children.Add(stopButton);
            controlButtonsPanel.Children.Add(autoPlayCheckBox);

            Grid.SetColumn(controlButtonsPanel, 0);
            playbackPanel.Children.Add(controlButtonsPanel);

            // Progress info
            var progressPanel = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };

            var nowPlayingLabel = new TextBlock
            {
                Text = "No file playing",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var progressBar = new ProgressBar
            {
                Height = 8,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var timeLabel = new TextBlock
            {
                Text = "00:00 / 00:00",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };

            progressPanel.Children.Add(nowPlayingLabel);
            progressPanel.Children.Add(progressBar);
            progressPanel.Children.Add(timeLabel);

            Grid.SetColumn(progressPanel, 1);
            playbackPanel.Children.Add(progressPanel);

            // Volume control
            var volumePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };

            var volumeLabel = new TextBlock
            {
                Text = "Volume:",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var volumeSlider = new Slider
            {
                Width = 100,
                Minimum = 0,
                Maximum = 100,
                Value = 100,
                TickFrequency = 10,
                IsSnapToTickEnabled = true
            };

            var volumeValueLabel = new TextBlock
            {
                Text = "100%",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 10
            };

            volumeSlider.ValueChanged += (s, args) =>
            {
                volumeValueLabel.Text = $"{(int)volumeSlider.Value}%";
            };

            volumePanel.Children.Add(volumeLabel);
            volumePanel.Children.Add(volumeSlider);
            volumePanel.Children.Add(volumeValueLabel);

            Grid.SetColumn(volumePanel, 2);
            playbackPanel.Children.Add(volumePanel);

            // Bottom buttons
            var bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var mergeButton = new Button
            {
                Content = "Merge Selected Files...",
                Width = 150,
                Height = 30,
                Margin = new Thickness(5),
                IsEnabled = false,
                Background = new SolidColorBrush(Color.FromRgb(100, 180, 100))
            };

            var loadToMainButton = new Button
            {
                Content = "Load Selected to Main Player",
                Width = 180,
                Height = 30,
                Margin = new Thickness(5),
                IsEnabled = false
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            bottomPanel.Children.Add(mergeButton);
            bottomPanel.Children.Add(loadToMainButton);
            bottomPanel.Children.Add(closeButton);

            // Layout
            Grid.SetRow(topPanel, 0);
            Grid.SetRow(listBox, 1);
            Grid.SetRow(playbackPanel, 2);
            Grid.SetRow(bottomPanel, 3);

            mainGrid.Children.Add(topPanel);
            mainGrid.Children.Add(listBox);
            mainGrid.Children.Add(playbackPanel);
            mainGrid.Children.Add(bottomPanel);

            browserWindow.Content = mainGrid;

            // Audio playback objects
            var browserPlaybackService = new AudioPlaybackService();
            var browserTimer = new System.Windows.Threading.DispatcherTimer();
            browserTimer.Interval = TimeSpan.FromMilliseconds(100);

            browserTimer.Tick += (s, args) =>
            {
                if (browserPlaybackService.IsPlaying)
                {
                    var currentTime = browserPlaybackService.CurrentPosition;
                    var totalTime = browserPlaybackService.TotalDuration;

                    progressBar.Maximum = totalTime.TotalSeconds;
                    progressBar.Value = currentTime.TotalSeconds;

                    timeLabel.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
                }
            };

            // Select folder functionality
            selectFolderButton.Click += (s, args) =>
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select folder containing audio files",
                    SelectedPath = _lastOutputFolder
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedFolder = folderDialog.SelectedPath;
                    currentFolderLabel.Text = selectedFolder;

                    listBox.Items.Clear();

                    var audioFiles = _fileService.GetAudioFilesInDirectory(selectedFolder);

                    foreach (var file in audioFiles)
                    {
                        listBox.Items.Add(file);
                    }

                    if (audioFiles.Count > 0)
                    {
                        playButton.IsEnabled = true;
                        loadToMainButton.IsEnabled = true;
                    }
                }
            };

            // Update merge button state
            listBox.SelectionChanged += (s, args) =>
            {
                mergeButton.IsEnabled = listBox.SelectedItems.Count >= 2;
            };

            // Merge button functionality
            mergeButton.Click += async (s, args) =>
            {
                if (listBox.SelectedItems.Count < 2)
                {
                    MessageBox.Show("Please select at least 2 files to merge.", "Merge Files",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selectedFiles = listBox.SelectedItems
                    .Cast<AudioFileItem>()
                    .OrderBy(item => item.FullPath)
                    .Select(item => item.FullPath)
                    .ToList();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3",
                    FileName = "merged_output.wav",
                    InitialDirectory = Path.GetDirectoryName(selectedFiles[0])
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        mergeButton.IsEnabled = false;
                        mergeButton.Content = "Merging...";

                        await _mergeService.MergeAudioFilesAsync(selectedFiles, saveDialog.FileName);

                        mergeButton.Content = "Merge Selected Files...";
                        mergeButton.IsEnabled = true;

                        var result = MessageBox.Show(
                            $"Successfully merged {selectedFiles.Count} files!\n\n" +
                            $"Output: {Path.GetFileName(saveDialog.FileName)}\n\n" +
                            "Would you like to open the output folder?",
                            "Merge Complete",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            _fileService.SelectFileInExplorer(saveDialog.FileName);
                        }

                        LogMessage($"Merged {selectedFiles.Count} files into {Path.GetFileName(saveDialog.FileName)}");
                    }
                    catch (Exception ex)
                    {
                        mergeButton.Content = "Merge Selected Files...";
                        mergeButton.IsEnabled = true;

                        MessageBox.Show($"Error merging files:\n\n{ex.Message}",
                            "Merge Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        LogMessage($"Error merging files: {ex.Message}");
                    }
                }
            };

            // Play button functionality
            playButton.Click += (s, args) =>
            {
                if (listBox.SelectedItem is AudioFileItem selectedItem)
                {
                    try
                    {
                        browserPlaybackService.Stop();

                        browserPlaybackService.LoadPlaylist(new List<string> { selectedItem.FullPath });
                        browserPlaybackService.SetVolume((float)(volumeSlider.Value / 100.0));
                        browserPlaybackService.Play(0);

                        browserTimer.Start();

                        nowPlayingLabel.Text = $"Playing: {selectedItem.DisplayName}";
                        stopButton.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error playing file:\n{ex.Message}", "Playback Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a file to play.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            // Stop button functionality
            stopButton.Click += (s, args) =>
            {
                browserPlaybackService.Stop();
                browserTimer.Stop();
                nowPlayingLabel.Text = "Playback stopped";
                progressBar.Value = 0;
                timeLabel.Text = "00:00 / 00:00";
                stopButton.IsEnabled = false;
            };

            // Volume control
            volumeSlider.ValueChanged += (s, args) =>
            {
                browserPlaybackService.SetVolume((float)(volumeSlider.Value / 100.0));
            };

            // Double-click to play
            listBox.MouseDoubleClick += (s, args) =>
            {
                if (listBox.SelectedItem != null)
                {
                    playButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            // Load to main player
            loadToMainButton.Click += (s, args) =>
            {
                if (listBox.SelectedItems.Count > 0)
                {
                    _createdAudioFiles.Clear();

                    foreach (AudioFileItem item in listBox.SelectedItems)
                    {
                        _createdAudioFiles.Add(item.FullPath);
                    }

                    UpdatePlaybackControls();

                    MessageBox.Show($"Loaded {listBox.SelectedItems.Count} file(s) to main player.",
                        "Files Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            // Playback completed event
            browserPlaybackService.PlaybackCompleted += (s, args) =>
            {
                browserTimer.Stop();

                if (autoPlayCheckBox.IsChecked == true)
                {
                    int currentIndex = listBox.SelectedIndex;
                    if (currentIndex < listBox.Items.Count - 1)
                    {
                        listBox.SelectedIndex = currentIndex + 1;
                        playButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    else
                    {
                        nowPlayingLabel.Text = "Playback complete";
                        progressBar.Value = 0;
                        timeLabel.Text = "00:00 / 00:00";
                    }
                }
                else
                {
                    nowPlayingLabel.Text = "Playback stopped";
                    progressBar.Value = 0;
                    timeLabel.Text = "00:00 / 00:00";
                }
            };

            closeButton.Click += (s, args) => browserWindow.Close();

            // Cleanup on close
            browserWindow.Closing += (s, args) =>
            {
                browserTimer.Stop();
                browserPlaybackService.Dispose();
            };

            // Keyboard shortcuts
            browserWindow.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.Space && playButton.IsEnabled)
                {
                    playButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    args.Handled = true;
                }
                else if (args.Key == Key.Escape)
                {
                    stopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else if (args.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control && mergeButton.IsEnabled)
                {
                    mergeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    args.Handled = true;
                }
            };

            browserWindow.ShowDialog();
        }

        private void SetCredentials_Click(object sender, RoutedEventArgs e)
        {
            if (_currentService == null) return;

            if (_currentService == _googleService)
            {
                ShowGoogleCredentialsDialog();
            }
            else if (_currentService == _awsService)
            {
                ShowAWSCredentialsDialog();
            }
            else if (_currentService == _elevenLabsService)
            {
                ShowElevenLabsCredentialsDialog();
            }
            else if (_currentService == _lemonfoxService)  // <-- ADD THIS BLOCK
            {
                ShowLemonfoxCredentialsDialog();
            }
            else
            {
                MessageBox.Show("This service does not require credentials.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowGoogleCredentialsDialog()
        {
            var dialog = new Window
            {
                Title = "Google Cloud TTS API Key",
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };

            stack.Children.Add(new TextBlock
            {
                Text = "Enter Google Cloud Text-to-Speech API Key:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Get your API key from the Google Cloud Console",
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            var textBox = new TextBox
            {
                Text = _credentialService.GoogleApiKey,
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new FontFamily("Consolas")
            };
            stack.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };

            okButton.Click += (s, e) =>
            {
                _credentialService.GoogleApiKey = textBox.Text.Trim();
                _credentialService.SaveCredentials();
                dialog.Close();
                LogMessage("Google API key updated");
            };

            cancelButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            dialog.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            dialog.ShowDialog();
        }

        private void ShowAWSCredentialsDialog()
        {
            var dialog = new Window
            {
                Title = "AWS Polly Credentials",
                Width = 500,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            for (int i = 0; i < 7; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Access Key
            var accessKeyLabel = new TextBlock
            {
                Text = "AWS Access Key ID:",
                Margin = new Thickness(10, 10, 10, 5),
                FontWeight = FontWeights.SemiBold
            };

            var accessKeyBox = new TextBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                Text = _credentialService.AWSAccessKey,
                FontFamily = new FontFamily("Consolas"),
                Height = 25
            };

            // Secret Key
            var secretKeyLabel = new TextBlock
            {
                Text = "AWS Secret Access Key:",
                Margin = new Thickness(10, 0, 10, 5),
                FontWeight = FontWeights.SemiBold
            };

            var secretKeyBox = new PasswordBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                FontFamily = new FontFamily("Consolas"),
                Height = 25
            };

            if (!string.IsNullOrEmpty(_credentialService.AWSSecretKey))
            {
                secretKeyBox.Password = _credentialService.AWSSecretKey;
            }

            // Region
            var regionLabel = new TextBlock
            {
                Text = "AWS Region:",
                Margin = new Thickness(10, 0, 10, 5),
                FontWeight = FontWeights.SemiBold
            };

            var regionCombo = new ComboBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                Height = 25
            };

            var regions = new[]
            {
                "us-east-1", "us-west-2", "us-west-1", "eu-west-1",
                "eu-central-1", "ap-southeast-1", "ap-northeast-1",
                "ap-southeast-2", "ap-south-1", "sa-east-1"
            };

            foreach (var region in regions)
            {
                regionCombo.Items.Add(region);
            }

            regionCombo.SelectedItem = _credentialService.AWSRegion;
            if (regionCombo.SelectedItem == null && regionCombo.Items.Count > 0)
            {
                regionCombo.SelectedIndex = 0;
            }

            // Instructions
            var instructionText = new TextBlock
            {
                Text = "Get credentials from AWS IAM Console.\nEnsure your IAM user has AmazonPollyFullAccess policy.",
                Margin = new Thickness(10, 0, 10, 10),
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            };

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };

            okButton.Click += (s, args) =>
            {
                _credentialService.AWSAccessKey = accessKeyBox.Text.Trim();
                _credentialService.AWSSecretKey = secretKeyBox.Password.Trim();
                _credentialService.AWSRegion = regionCombo.SelectedItem?.ToString() ?? "us-east-1";

                _credentialService.SaveCredentials();
                _awsService.InitializeClient();

                LogMessage($"AWS Polly credentials updated for region: {_credentialService.AWSRegion}");
                dialog.Close();
            };

            cancelButton.Click += (s, args) => dialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(accessKeyLabel, 0);
            Grid.SetRow(accessKeyBox, 1);
            Grid.SetRow(secretKeyLabel, 2);
            Grid.SetRow(secretKeyBox, 3);
            Grid.SetRow(regionLabel, 4);
            Grid.SetRow(regionCombo, 5);
            Grid.SetRow(instructionText, 6);
            Grid.SetRow(buttonPanel, 7);

            grid.Children.Add(accessKeyLabel);
            grid.Children.Add(accessKeyBox);
            grid.Children.Add(secretKeyLabel);
            grid.Children.Add(secretKeyBox);
            grid.Children.Add(regionLabel);
            grid.Children.Add(regionCombo);
            grid.Children.Add(instructionText);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            dialog.Loaded += (s, args) =>
            {
                accessKeyBox.Focus();
                accessKeyBox.SelectAll();
            };

            dialog.ShowDialog();
        }

        private void ShowElevenLabsCredentialsDialog()
        {
            var dialog = new Window
            {
                Title = "ElevenLabs API Key",
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };

            stack.Children.Add(new TextBlock
            {
                Text = "Enter ElevenLabs API Key:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Get your API key from https://elevenlabs.io/speech-synthesis",
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            var textBox = new TextBox
            {
                Text = _credentialService.ElevenLabsApiKey,
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new FontFamily("Consolas")
            };
            stack.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };

            okButton.Click += (s, e) =>
            {
                _credentialService.ElevenLabsApiKey = textBox.Text.Trim();
                _credentialService.SaveCredentials();
                dialog.Close();
                LogMessage("ElevenLabs API key updated");
            };

            cancelButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            dialog.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            dialog.ShowDialog();
        }

        private void ShowLemonfoxCredentialsDialog()
        {
            var dialog = new Window
            {
                Title = "Lemonfox AI Credentials",
                Width = 500,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(15) };

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = "Enter Lemonfox AI Credentials:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });

            // Instructions
            stack.Children.Add(new TextBlock
            {
                Text = "Get your API key from https://www.lemonfox.ai/apis/keys",
                Margin = new Thickness(0, 0, 0, 15),
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            // API Key Label
            stack.Children.Add(new TextBlock
            {
                Text = "API Key:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            // API Key TextBox
            var apiKeyBox = new TextBox
            {
                Text = _credentialService.LemonfoxApiKey,
                Margin = new Thickness(0, 0, 0, 15),
                FontFamily = new FontFamily("Consolas"),
                Height = 25
            };
            stack.Children.Add(apiKeyBox);

            // Username Label
            stack.Children.Add(new TextBlock
            {
                Text = "Username (optional):",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            // Username TextBox
            var usernameBox = new TextBox
            {
                Text = _credentialService.LemonfoxUsername,
                Margin = new Thickness(0, 0, 0, 15),
                Height = 25
            };
            stack.Children.Add(usernameBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };

            okButton.Click += (s, e) =>
            {
                _credentialService.LemonfoxApiKey = apiKeyBox.Text.Trim();
                _credentialService.LemonfoxUsername = usernameBox.Text.Trim();
                _credentialService.SaveCredentials();
                dialog.Close();
                LogMessage("Lemonfox credentials updated");
                MessageBox.Show("Lemonfox credentials saved successfully!",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            cancelButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            dialog.Loaded += (s, e) =>
            {
                apiKeyBox.Focus();
                apiKeyBox.SelectAll();
            };

            dialog.ShowDialog();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "TTS3 - Text to Speech Converter\n\n" +
                "Version 3.0 (Refactored)\n\n" +
                "A modular, service-based text-to-speech application\n" +
                "supporting multiple TTS engines:\n" +
                "- Windows SAPI\n" +
                "- Google Cloud TTS\n" +
                "- AWS Polly\n" +
                "- ElevenLabs\n\n" +
                "Features:\n" +
                "- Multiple voice support\n" +
                "- SSML tags\n" +
                "- Audio merging\n" +
                "- Real-time preview\n" +
                "- Plugin-ready architecture\n\n" +
                "© 2024 MinMax Solutions",
                "About TTS3",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _pluginManager?.UnloadAll();
            _playbackService?.Dispose();
            base.OnClosing(e);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+F for Find
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Find_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+H for Find/Replace
            else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
                FindReplace_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+Shift+R for Random Replace
            else if (e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                RandomReplace_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+O for Open
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OpenFile_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+S for Save
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveText_Click(sender, e);
                e.Handled = true;
            }
            // F5 for Convert
            else if (e.Key == Key.F5)
            {
                if (btnConvert.IsEnabled)
                {
                    Convert_Click(sender, e);
                    e.Handled = true;
                }
            }
            // Space for Play/Pause when not in text editor
            else if (e.Key == Key.Space && !rtbTextContent.IsFocused && btnPlayPause.IsEnabled)
            {
                PlayPause_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+Shift+C for Colorize
            else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                HighlightSSML_Click(sender, e);
                e.Handled = true;
            }
        }


        #region Random Replacement Helper Methods

        /// <summary>
        /// Processes replacement text that may contain random selection syntax
        /// Format: #x:value1,value2,value3 or #value1,value2,value2,value3 (weighted)
        /// Examples:
        ///   teststring=#x:2,5,8,9 -> randomly picks 2, 5, 8, or 9
        ///   teststring=#2,2,2,2,3,3,3,5,8,9 -> weighted random (40% 2, 30% 3, 10% each for 5,8,9)
        /// </summary>
        private string ProcessRandomReplacement(string replaceText)
        {
            if (string.IsNullOrEmpty(replaceText))
                return replaceText;

            // Pattern to match #x:values or #values syntax
            var pattern = @"#(x:)?([0-9,\s]+)";
            var regex = new Regex(pattern);

            return regex.Replace(replaceText, match =>
            {
                string values = match.Groups[2].Value;

                // Split by comma and trim whitespace
                var valueList = values.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();

                if (valueList.Count == 0)
                    return match.Value; // Return original if no valid values

                // Select a random value from the list
                var random = new Random();
                int index = random.Next(valueList.Count);
                return valueList[index];
            });
        }

        /// <summary>
        /// Processes all occurrences in the content and replaces them with random selections
        /// </summary>
        private string ProcessRandomReplacementAll(string content, string findText, string replaceText, StringComparison comparison)
        {
            var result = new StringBuilder();
            int lastIndex = 0;
            int index = 0;

            while ((index = content.IndexOf(findText, lastIndex, comparison)) != -1)
            {
                // Add text before the match
                result.Append(content.Substring(lastIndex, index - lastIndex));

                // Add the random replacement
                result.Append(ProcessRandomReplacement(replaceText));

                lastIndex = index + findText.Length;
            }

            // Add remaining text
            result.Append(content.Substring(lastIndex));

            return result.ToString();
        }

        #endregion


        private void RandomReplace_Click(object sender, RoutedEventArgs e)
        {
            var randomReplaceWindow = new Window
            {
                Title = "Random Replace",
                Width = 550,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Find what section
            var findLabel = new Label { Content = "Find what:", Margin = new Thickness(10, 10, 10, 0) };
            var findTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25,
                FontFamily = new FontFamily("Consolas")
            };

            // Replace with section
            var replaceLabel = new Label
            {
                Content = "Replace with (use random syntax):",
                Margin = new Thickness(10, 0, 10, 0)
            };
            var replaceTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Height = 25,
                FontFamily = new FontFamily("Consolas")
            };

            // Help text
            var helpText = new TextBlock
            {
                Margin = new Thickness(10, 0, 10, 10),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkBlue,
                Text = "Random Syntax Examples:\n" +
                       "• Equal probability: #x:1,2,3 (each 33%)\n" +
                       "• Weighted: #1,1,2,3 (50% 1, 25% 2, 25% 3)\n" +
                       "• Voice tags: <voice=#x:1,2,3>\n" +
                       "• Pauses: <break time=\"#x:500ms,1s,1500ms\"/>\n\n" +
                       "Each occurrence gets its own random selection!"
            };

            // Options panel
            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 0, 10, 10)
            };

            var matchCaseCheckBox = new CheckBox
            {
                Content = "Match case",
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            optionsPanel.Children.Add(matchCaseCheckBox);

            // Stats label
            var statsLabel = new Label
            {
                Content = "",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.DarkGreen
            };

            // Buttons panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var previewButton = new Button
            {
                Content = "Preview One",
                Width = 110,
                Height = 30,
                Margin = new Thickness(5),
                ToolTip = "Show one random result as preview"
            };

            var replaceAllButton = new Button
            {
                Content = "Replace All",
                Width = 110,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(100, 180, 100))
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };

            // Preview button functionality
            previewButton.Click += (s, args) =>
            {
                string findText = findTextBox.Text;
                string replaceText = replaceTextBox.Text;

                if (string.IsNullOrEmpty(findText))
                {
                    MessageBox.Show("Please enter text to find.", "Preview",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(replaceText))
                {
                    MessageBox.Show("Please enter replacement text with random syntax.", "Preview",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if random syntax is present
                bool hasRandomSyntax = Regex.IsMatch(replaceText, @"#(x:)?[0-9,\s]+");
                if (!hasRandomSyntax)
                {
                    MessageBox.Show("No random syntax found in replacement text.\n\n" +
                        "Use #x:1,2,3 for equal probability\n" +
                        "or #1,1,2,3 for weighted probability.",
                        "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Generate preview
                string preview = ProcessRandomReplacement(replaceText);
                MessageBox.Show($"Find: {findText}\n\nRandom replacement preview:\n{preview}\n\n" +
                    "Note: Each occurrence will get a different random value!",
                    "Preview Result", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // Replace All button functionality
            replaceAllButton.Click += (s, args) =>
            {
                string findText = findTextBox.Text;
                string replaceText = replaceTextBox.Text;

                if (string.IsNullOrEmpty(findText))
                {
                    MessageBox.Show("Please enter text to find.", "Random Replace All",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(replaceText))
                {
                    MessageBox.Show("Please enter replacement text with random syntax.", "Random Replace All",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if random syntax is present
                bool hasRandomSyntax = Regex.IsMatch(replaceText, @"#(x:)?[0-9,\s]+");
                if (!hasRandomSyntax)
                {
                    MessageBox.Show("No random syntax found in replacement text.\n\n" +
                        "Use #x:1,2,3 for equal probability\n" +
                        "or #1,1,2,3 for weighted probability.",
                        "Random Replace All", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var textRange = new TextRange(rtbTextContent.Document.ContentStart,
                    rtbTextContent.Document.ContentEnd);
                string content = textRange.Text;

                StringComparison comparison = matchCaseCheckBox.IsChecked == true ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                // Count occurrences
                int count = 0;
                int index = 0;
                while ((index = content.IndexOf(findText, index, comparison)) != -1)
                {
                    count++;
                    index += findText.Length;
                }

                if (count == 0)
                {
                    MessageBox.Show("No occurrences found.", "Random Replace All",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Replace {count} occurrence(s) of '{findText}' with random values?\n\n" +
                    $"Each occurrence will get a different random selection from:\n{replaceText}",
                    "Confirm Random Replace All",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Use random replacement for all occurrences
                    content = ProcessRandomReplacementAll(content, findText, replaceText, comparison);

                    rtbTextContent.Document.Blocks.Clear();
                    rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(content)));

                    statsLabel.Content = $"✓ Replaced {count} occurrence(s) with random values";
                    LogMessage($"Random Replace: Replaced {count} occurrence(s) of '{findText}'");

                    MessageBox.Show($"Successfully replaced {count} occurrences with random values!",
                        "Random Replace Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            closeButton.Click += (s, args) => randomReplaceWindow.Close();

            // Update stats when find text changes
            findTextBox.TextChanged += (s, args) =>
            {
                string searchText = findTextBox.Text;
                if (string.IsNullOrEmpty(searchText))
                {
                    statsLabel.Content = "";
                    return;
                }

                var textRange = new TextRange(rtbTextContent.Document.ContentStart,
                    rtbTextContent.Document.ContentEnd);
                string content = textRange.Text;

                StringComparison comparison = matchCaseCheckBox.IsChecked == true ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                int count = 0;
                int index = 0;
                while ((index = content.IndexOf(searchText, index, comparison)) != -1)
                {
                    count++;
                    index += searchText.Length;
                }

                statsLabel.Content = count > 0 ?
                    $"Found {count} occurrence(s)" : "No occurrences found";
            };

            // Layout
            Grid.SetRow(findLabel, 0);
            Grid.SetRow(findTextBox, 1);
            Grid.SetRow(replaceLabel, 2);
            Grid.SetRow(replaceTextBox, 3);
            Grid.SetRow(helpText, 4);
            Grid.SetRow(optionsPanel, 5);
            Grid.SetRow(statsLabel, 6);
            Grid.SetRow(buttonPanel, 7);

            grid.Children.Add(findLabel);
            grid.Children.Add(findTextBox);
            grid.Children.Add(replaceLabel);
            grid.Children.Add(replaceTextBox);
            grid.Children.Add(helpText);
            grid.Children.Add(optionsPanel);
            grid.Children.Add(statsLabel);
            grid.Children.Add(buttonPanel);

            buttonPanel.Children.Add(previewButton);
            buttonPanel.Children.Add(replaceAllButton);
            buttonPanel.Children.Add(closeButton);

            randomReplaceWindow.Content = grid;

            randomReplaceWindow.Loaded += (s, args) =>
            {
                findTextBox.Focus();

                if (!rtbTextContent.Selection.IsEmpty)
                {
                    findTextBox.Text = rtbTextContent.Selection.Text;
                    findTextBox.SelectAll();
                }
            };

            randomReplaceWindow.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    randomReplaceWindow.Close();
                }
            };

            randomReplaceWindow.ShowDialog();
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                txtLog.ScrollToEnd();
            });
        }

    }
}