// ==========================================
// SSML TEMPLATE LIBRARY & MATH DETECTION
// Add these to MainWindow.xaml.cs
// ==========================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TTS3
{
    public partial class MainWindow
    {
        #region SSML Template Library

        // Template class to store SSML snippets
        public class SSMLTemplate
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public string Template { get; set; }
            public string Description { get; set; }
            public string Example { get; set; }
        }

        // Built-in template library
        private List<SSMLTemplate> _ssmlTemplates = new List<SSMLTemplate>
        {
            // ===== MATH TEMPLATES =====
            new SSMLTemplate
            {
                Name = "Fraction",
                Category = "Math",
                Template = "<say-as interpret-as=\"fraction\">{text}</say-as>",
                Description = "Speaks fractions naturally (e.g., 1/2 as 'one half')",
                Example = "1/2 → one half"
            },
            new SSMLTemplate
            {
                Name = "Exponent (Superscript)",
                Category = "Math",
                Template = "<say-as interpret-as=\"ordinal\">{base}</say-as> to the <say-as interpret-as=\"ordinal\">{exponent}</say-as> power",
                Description = "Speaks exponents (e.g., x^2 as 'x to the second power')",
                Example = "x^2 → x to the second power"
            },
            new SSMLTemplate
            {
                Name = "Square Root",
                Category = "Math",
                Template = "the square root of {number}",
                Description = "Speaks square roots clearly",
                Example = "√25 → the square root of 25"
            },
            new SSMLTemplate
            {
                Name = "Equation",
                Category = "Math",
                Template = "<prosody rate=\"slow\">{equation}</prosody>",
                Description = "Slows down for complex equations",
                Example = "Speaks equation more slowly"
            },
            new SSMLTemplate
            {
                Name = "Variable",
                Category = "Math",
                Template = "<emphasis level=\"moderate\">{variable}</emphasis>",
                Description = "Emphasizes mathematical variables",
                Example = "x, y, z with slight emphasis"
            },
            new SSMLTemplate
            {
                Name = "Subscript",
                Category = "Math",
                Template = "{base} sub {subscript}",
                Description = "Speaks subscripts (e.g., x₁ as 'x sub 1')",
                Example = "x₁ → x sub one"
            },

            // ===== EMPHASIS & PAUSES =====
            new SSMLTemplate
            {
                Name = "Strong Emphasis",
                Category = "Emphasis",
                Template = "<emphasis level=\"strong\">{text}</emphasis>",
                Description = "Strong emphasis on text",
                Example = "VERY important"
            },
            new SSMLTemplate
            {
                Name = "Moderate Emphasis",
                Category = "Emphasis",
                Template = "<emphasis level=\"moderate\">{text}</emphasis>",
                Description = "Moderate emphasis on text",
                Example = "Somewhat important"
            },
            new SSMLTemplate
            {
                Name = "Short Pause",
                Category = "Pauses",
                Template = "<break time=\"500ms\"/>",
                Description = "Half-second pause",
                Example = "Brief pause"
            },
            new SSMLTemplate
            {
                Name = "Medium Pause",
                Category = "Pauses",
                Template = "<break time=\"1s\"/>",
                Description = "One-second pause",
                Example = "Standard pause"
            },
            new SSMLTemplate
            {
                Name = "Long Pause",
                Category = "Pauses",
                Template = "<break time=\"2s\"/>",
                Description = "Two-second dramatic pause",
                Example = "Dramatic pause"
            },

            // ===== PROSODY =====
            new SSMLTemplate
            {
                Name = "Slow Speech",
                Category = "Speed",
                Template = "<prosody rate=\"slow\">{text}</prosody>",
                Description = "Slows down speech for clarity",
                Example = "Speak more slowly"
            },
            new SSMLTemplate
            {
                Name = "Fast Speech",
                Category = "Speed",
                Template = "<prosody rate=\"fast\">{text}</prosody>",
                Description = "Speeds up speech",
                Example = "Speak more quickly"
            },
            new SSMLTemplate
            {
                Name = "Very Slow (Explanation)",
                Category = "Speed",
                Template = "<prosody rate=\"x-slow\">{text}</prosody>",
                Description = "Extra slow for complex explanations",
                Example = "Very deliberate speech"
            },
            new SSMLTemplate
            {
                Name = "Whisper",
                Category = "Volume",
                Template = "<prosody volume=\"soft\" rate=\"slow\">{text}</prosody>",
                Description = "Quiet, slow speech like a whisper",
                Example = "Hushed tones"
            },
            new SSMLTemplate
            {
                Name = "Loud/Excited",
                Category = "Volume",
                Template = "<prosody volume=\"loud\" rate=\"fast\"><emphasis>{text}</emphasis></prosody>",
                Description = "Loud and energetic speech",
                Example = "Excited announcement!"
            },

            // ===== SPECIAL FORMATS =====
            new SSMLTemplate
            {
                Name = "Spell Out",
                Category = "Format",
                Template = "<say-as interpret-as=\"spell-out\">{text}</say-as>",
                Description = "Spells out text letter by letter",
                Example = "NASA → N-A-S-A"
            },
            new SSMLTemplate
            {
                Name = "Phone Number",
                Category = "Format",
                Template = "<say-as interpret-as=\"telephone\">{number}</say-as>",
                Description = "Speaks phone numbers naturally",
                Example = "555-1234"
            },
            new SSMLTemplate
            {
                Name = "Date",
                Category = "Format",
                Template = "<say-as interpret-as=\"date\" format=\"mdy\">{date}</say-as>",
                Description = "Speaks dates (Month-Day-Year)",
                Example = "12/25/2024 → December 25th, 2024"
            },
            new SSMLTemplate
            {
                Name = "Ordinal Number",
                Category = "Format",
                Template = "<say-as interpret-as=\"ordinal\">{number}</say-as>",
                Description = "Speaks ordinal numbers",
                Example = "1 → first, 2 → second"
            },
            new SSMLTemplate
            {
                Name = "Cardinal Number",
                Category = "Format",
                Template = "<say-as interpret-as=\"cardinal\">{number}</say-as>",
                Description = "Speaks numbers as quantities",
                Example = "100 → one hundred"
            },

            // ===== SPECIAL CHARACTERS =====
            new SSMLTemplate
            {
                Name = "Substitute Text",
                Category = "Special",
                Template = "<sub alias=\"{spoken}\">{written}</sub>",
                Description = "Speaks different text than written",
                Example = "WWW → World Wide Web"
            }
        };

        private void ShowSSMLTemplateLibrary_Click(object sender, RoutedEventArgs e)
        {
            var libraryWindow = new Window
            {
                Title = "SSML Template Library",
                Width = 900,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Category list (left side)
            var categoryList = new ListBox
            {
                Margin = new Thickness(5),
                FontSize = 14
            };

            var categories = _ssmlTemplates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();
            categoryList.Items.Add("All Templates");
            foreach (var category in categories)
            {
                categoryList.Items.Add(category);
            }
            categoryList.SelectedIndex = 0;

            Grid.SetColumn(categoryList, 0);
            mainGrid.Children.Add(categoryList);

            // Template display (right side)
            var rightPanel = new Grid();
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var templateList = new StackPanel
            {
                Margin = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = templateList
            };

            Grid.SetRow(scrollViewer, 0);
            rightPanel.Children.Add(scrollViewer);

            // Bottom buttons
            var bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };
            closeButton.Click += (s, args) => libraryWindow.Close();

            bottomPanel.Children.Add(closeButton);
            Grid.SetRow(bottomPanel, 1);
            rightPanel.Children.Add(bottomPanel);

            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            libraryWindow.Content = mainGrid;

            // Populate templates based on category selection
            Action<string> populateTemplates = (category) =>
            {
                templateList.Children.Clear();

                var templates = category == "All Templates"
                    ? _ssmlTemplates
                    : _ssmlTemplates.Where(t => t.Category == category).ToList();

                foreach (var template in templates)
                {
                    var templateCard = new Border
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 5, 5, 5),
                        Padding = new Thickness(10),
                        Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                        CornerRadius = new CornerRadius(5)
                    };

                    var cardContent = new StackPanel();

                    // Template name
                    var namePanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                    var nameText = new TextBlock
                    {
                        Text = template.Name,
                        FontSize = 16,
                        FontWeight = FontWeights.Bold
                    };
                    DockPanel.SetDock(nameText, Dock.Left);

                    var categoryBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                        Padding = new Thickness(5, 2, 5, 2),
                        CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(10, 0, 0, 0)
                    };
                    DockPanel.SetDock(categoryBadge, Dock.Left);
                    categoryBadge.Child = new TextBlock
                    {
                        Text = template.Category,
                        Foreground = Brushes.White,
                        FontSize = 10
                    };

                    namePanel.Children.Add(nameText);
                    namePanel.Children.Add(categoryBadge);
                    cardContent.Children.Add(namePanel);

                    // Description
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = template.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 5)
                    });

                    // Template code
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = "Template:",
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 5, 0, 2)
                    });

                    var templateCode = new TextBox
                    {
                        Text = template.Template,
                        IsReadOnly = true,
                        FontFamily = new FontFamily("Consolas"),
                        Background = Brushes.White,
                        Padding = new Thickness(5),
                        Margin = new Thickness(0, 0, 0, 5),
                        TextWrapping = TextWrapping.Wrap
                    };
                    cardContent.Children.Add(templateCode);

                    // Example
                    if (!string.IsNullOrEmpty(template.Example))
                    {
                        cardContent.Children.Add(new TextBlock
                        {
                            Text = $"Example: {template.Example}",
                            FontStyle = FontStyles.Italic,
                            FontSize = 11,
                            Foreground = Brushes.Gray,
                            Margin = new Thickness(0, 0, 0, 5)
                        });
                    }

                    // Insert button
                    var insertButton = new Button
                    {
                        Content = "📝 Insert at Cursor",
                        Width = 150,
                        Height = 30,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Tag = template
                    };

                    insertButton.Click += (s, args) =>
                    {
                        InsertTemplate(template);
                        libraryWindow.Close();
                    };

                    cardContent.Children.Add(insertButton);

                    templateCard.Child = cardContent;
                    templateList.Children.Add(templateCard);
                }
            };

            // Category selection changed
            categoryList.SelectionChanged += (s, args) =>
            {
                if (categoryList.SelectedItem != null)
                {
                    populateTemplates(categoryList.SelectedItem.ToString());
                }
            };

            // Initial population
            populateTemplates("All Templates");

            libraryWindow.ShowDialog();
        }

        private void InsertTemplate(SSMLTemplate template)
        {
            var selection = rtbTextContent.Selection;
            string templateText = template.Template;

            if (!selection.IsEmpty)
            {
                // Replace {text} placeholder with selected text
                string selectedText = selection.Text;
                
                if (templateText.Contains("{text}"))
                {
                    templateText = templateText.Replace("{text}", selectedText);
                    selection.Text = templateText;
                    LogMessage($"Applied template: {template.Name}");
                }
                else
                {
                    // No {text} placeholder - just insert at cursor
                    rtbTextContent.CaretPosition.InsertTextInRun(templateText);
                    LogMessage($"Inserted template: {template.Name}");
                }
            }
            else
            {
                // No selection - insert at cursor with placeholder
                rtbTextContent.CaretPosition.InsertTextInRun(templateText);
                LogMessage($"Inserted template: {template.Name}");
            }

            rtbTextContent.Focus();
        }

        #endregion

        #region Smart Math Detection

        private void AnalyzeMathAndSuggest_Click(object sender, RoutedEventArgs e)
        {
            var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
            string text = textRange.Text;

            var suggestions = DetectMathPatterns(text);

            if (suggestions.Count == 0)
            {
                MessageBox.Show("No math expressions detected that need SSML markup.",
                    "Math Analysis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowMathSuggestions(suggestions);
        }

        private List<MathSuggestion> DetectMathPatterns(string text)
        {
            var suggestions = new List<MathSuggestion>();

            // Pattern 1: Fractions (1/2, 3/4, etc.)
            var fractionMatches = Regex.Matches(text, @"\b(\d+)/(\d+)\b");
            foreach (Match match in fractionMatches)
            {
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Fraction",
                    Suggested = $"<say-as interpret-as=\"fraction\">{match.Value}</say-as>",
                    Explanation = $"Will be spoken as '{NumberToWords(int.Parse(match.Groups[1].Value))} {GetFractionName(int.Parse(match.Groups[2].Value))}'"
                });
            }

            // Pattern 2: Exponents (x^2, 10^3, etc.)
            var exponentMatches = Regex.Matches(text, @"(\w+)\^(\d+)");
            foreach (Match match in exponentMatches)
            {
                string baseValue = match.Groups[1].Value;
                string exponent = match.Groups[2].Value;
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Exponent",
                    Suggested = $"{baseValue} to the <say-as interpret-as=\"ordinal\">{exponent}</say-as> power",
                    Explanation = $"Will be spoken as '{baseValue} to the {GetOrdinalName(int.Parse(exponent))} power'"
                });
            }

            // Pattern 3: Square roots (√25, sqrt(16))
            var sqrtMatches = Regex.Matches(text, @"(√|sqrt\()(\d+)\)?");
            foreach (Match match in sqrtMatches)
            {
                string number = match.Groups[2].Value;
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Square Root",
                    Suggested = $"the square root of {number}",
                    Explanation = $"Will be spoken as 'the square root of {number}'"
                });
            }

            // Pattern 4: Subscripts (x_1, H_2O)
            var subscriptMatches = Regex.Matches(text, @"(\w+)_(\w+)");
            foreach (Match match in subscriptMatches)
            {
                string baseValue = match.Groups[1].Value;
                string subscript = match.Groups[2].Value;
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Subscript",
                    Suggested = $"{baseValue} sub {subscript}",
                    Explanation = $"Will be spoken as '{baseValue} sub {subscript}'"
                });
            }

            // Pattern 5: Equations with = (a = b + c)
            var equationMatches = Regex.Matches(text, @"[a-zA-Z]\s*=\s*[^,\.\n]{3,30}");
            foreach (Match match in equationMatches)
            {
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Equation",
                    Suggested = $"<prosody rate=\"slow\">{match.Value.Trim()}</prosody>",
                    Explanation = "Will be spoken more slowly for clarity"
                });
            }

            // Pattern 6: Percentages (25%, 0.5%)
            var percentMatches = Regex.Matches(text, @"\b(\d+\.?\d*)\s*%");
            foreach (Match match in percentMatches)
            {
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Percentage",
                    Suggested = $"<say-as interpret-as=\"cardinal\">{match.Groups[1].Value}</say-as> percent",
                    Explanation = $"Will be spoken clearly as '{match.Groups[1].Value} percent'"
                });
            }

            // Pattern 7: Decimals (3.14, 0.5)
            var decimalMatches = Regex.Matches(text, @"\b(\d+)\.(\d+)\b");
            foreach (Match match in decimalMatches)
            {
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Decimal",
                    Suggested = $"{match.Groups[1].Value} point {string.Join(" ", match.Groups[2].Value.ToCharArray())}",
                    Explanation = $"Will be spoken as '{match.Groups[1].Value} point {string.Join(" ", match.Groups[2].Value.ToCharArray())}'"
                });
            }

            // Pattern 8: Scientific notation (1.5e10, 3E-5)
            var scientificMatches = Regex.Matches(text, @"(\d+\.?\d*)[eE]([-+]?\d+)");
            foreach (Match match in scientificMatches)
            {
                suggestions.Add(new MathSuggestion
                {
                    Original = match.Value,
                    Position = match.Index,
                    Type = "Scientific Notation",
                    Suggested = $"{match.Groups[1].Value} times ten to the <say-as interpret-as=\"ordinal\">{match.Groups[2].Value}</say-as> power",
                    Explanation = "Will be spoken in scientific notation format"
                });
            }

            return suggestions.OrderBy(s => s.Position).ToList();
        }

        private void ShowMathSuggestions(List<MathSuggestion> suggestions)
        {
            var suggestionsWindow = new Window
            {
                Title = "Math SSML Suggestions",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerPanel = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Padding = new Thickness(15)
            };

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Math Expression Suggestions",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = $"Found {suggestions.Count} math expression(s) that could benefit from SSML markup",
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 5, 0, 0)
            });

            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Suggestions list
            var suggestionsPanel = new StackPanel();
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(15),
                Content = suggestionsPanel
            };

            foreach (var suggestion in suggestions)
            {
                var card = CreateSuggestionCard(suggestion);
                suggestionsPanel.Children.Add(card);
            }

            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Bottom buttons
            var bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15)
            };

            var applyAllButton = new Button
            {
                Content = "✓ Apply All",
                Width = 120,
                Height = 35,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(100, 180, 100))
            };

            applyAllButton.Click += (s, args) =>
            {
                ApplyAllSuggestions(suggestions);
                suggestionsWindow.Close();
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                Height = 35,
                Margin = new Thickness(5)
            };
            closeButton.Click += (s, args) => suggestionsWindow.Close();

            bottomPanel.Children.Add(applyAllButton);
            bottomPanel.Children.Add(closeButton);

            Grid.SetRow(bottomPanel, 2);
            mainGrid.Children.Add(bottomPanel);

            suggestionsWindow.Content = mainGrid;
            suggestionsWindow.ShowDialog();
        }

        private Border CreateSuggestionCard(MathSuggestion suggestion)
        {
            var card = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(5)
            };

            var cardContent = new Grid();
            cardContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftPanel = new StackPanel();

            // Type badge
            var typeBadge = new Border
            {
                Background = GetColorForType(suggestion.Type),
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };
            typeBadge.Child = new TextBlock
            {
                Text = suggestion.Type,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            leftPanel.Children.Add(typeBadge);

            // Original text
            leftPanel.Children.Add(new TextBlock
            {
                Text = "Original:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            leftPanel.Children.Add(new TextBlock
            {
                Text = suggestion.Original,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(255, 240, 240)),
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Suggested SSML
            leftPanel.Children.Add(new TextBlock
            {
                Text = "Suggested SSML:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            leftPanel.Children.Add(new TextBlock
            {
                Text = suggestion.Suggested,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(240, 255, 240)),
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            // Explanation
            leftPanel.Children.Add(new TextBlock
            {
                Text = suggestion.Explanation,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });

            Grid.SetColumn(leftPanel, 0);
            cardContent.Children.Add(leftPanel);

            // Apply button
            var applyButton = new Button
            {
                Content = "Apply",
                Width = 80,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = suggestion
            };

            applyButton.Click += (s, args) =>
            {
                ApplySingleSuggestion(suggestion);
                ((Border)((Button)s).Parent).Background = new SolidColorBrush(Color.FromRgb(230, 255, 230));
                ((Button)s).Content = "✓ Applied";
                ((Button)s).IsEnabled = false;
            };

            Grid.SetColumn(applyButton, 1);
            cardContent.Children.Add(applyButton);

            card.Child = cardContent;
            return card;
        }

        private void ApplySingleSuggestion(MathSuggestion suggestion)
        {
            var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
            string text = textRange.Text;

            // Replace the first occurrence at the specified position
            string newText = text.Substring(0, suggestion.Position) + 
                           suggestion.Suggested + 
                           text.Substring(suggestion.Position + suggestion.Original.Length);

            rtbTextContent.Document.Blocks.Clear();
            rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(newText)));

            LogMessage($"Applied SSML suggestion: {suggestion.Type} - {suggestion.Original}");
        }

        private void ApplyAllSuggestions(List<MathSuggestion> suggestions)
        {
            var textRange = new TextRange(rtbTextContent.Document.ContentStart, rtbTextContent.Document.ContentEnd);
            string text = textRange.Text;

            // Apply suggestions in reverse order to maintain position integrity
            foreach (var suggestion in suggestions.OrderByDescending(s => s.Position))
            {
                text = text.Substring(0, suggestion.Position) +
                       suggestion.Suggested +
                       text.Substring(suggestion.Position + suggestion.Original.Length);
            }

            rtbTextContent.Document.Blocks.Clear();
            rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(text)));

            LogMessage($"Applied {suggestions.Count} SSML suggestions for math expressions");
            MessageBox.Show($"Successfully applied {suggestions.Count} SSML suggestions!",
                "Suggestions Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Brush GetColorForType(string type)
        {
            return type switch
            {
                "Fraction" => new SolidColorBrush(Color.FromRgb(100, 150, 255)),
                "Exponent" => new SolidColorBrush(Color.FromRgb(255, 100, 150)),
                "Square Root" => new SolidColorBrush(Color.FromRgb(150, 100, 255)),
                "Subscript" => new SolidColorBrush(Color.FromRgb(100, 200, 150)),
                "Equation" => new SolidColorBrush(Color.FromRgb(255, 150, 100)),
                "Percentage" => new SolidColorBrush(Color.FromRgb(200, 150, 100)),
                "Decimal" => new SolidColorBrush(Color.FromRgb(100, 180, 200)),
                "Scientific Notation" => new SolidColorBrush(Color.FromRgb(180, 100, 180)),
                _ => new SolidColorBrush(Color.FromRgb(150, 150, 150))
            };
        }

        // Helper methods for number conversion
        private string NumberToWords(int number)
        {
            if (number == 0) return "zero";
            var words = new[] { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            return number <= 10 ? words[number] : number.ToString();
        }

        private string GetFractionName(int denominator)
        {
            return denominator switch
            {
                2 => "half",
                3 => "third",
                4 => "quarter",
                5 => "fifth",
                8 => "eighth",
                _ => $"{NumberToWords(denominator)}th"
            };
        }

        private string GetOrdinalName(int number)
        {
            return number switch
            {
                1 => "first",
                2 => "second",
                3 => "third",
                4 => "fourth",
                5 => "fifth",
                _ => $"{number}th"
            };
        }

        #endregion

        // Supporting class
        public class MathSuggestion
        {
            public string Original { get; set; }
            public int Position { get; set; }
            public string Type { get; set; }
            public string Suggested { get; set; }
            public string Explanation { get; set; }
        }
    }
}
