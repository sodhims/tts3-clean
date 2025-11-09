using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Service for validating SSML syntax
    /// </summary>
    public class SSMLValidationService
    {
        private readonly HashSet<string> _selfClosingTags = new()
        {
            "break", "phoneme", "audio", "mark", "meta"
        };

        private readonly HashSet<string> _validTags = new()
        {
            "speak", "emphasis", "break", "prosody", "say-as", "phoneme", "sub",
            "audio", "p", "s", "voice", "mark", "desc", "lexicon", "metadata", "meta",
            "split", "service", "label", "vid", "comment"
        };

        /// <summary>
        /// Validate SSML syntax in text
        /// </summary>
/// <summary>
/// Validate SSML syntax in text
/// </summary>
public SSMLValidationResult ValidateSSML(string text)
{
    var result = new SSMLValidationResult();
    var tagStack = new Stack<SSMLTag>();

    // Updated regex to handle control tags like <voice=1>, <service=2>, etc.
    string tagPattern = @"<(/?)([a-zA-Z]+)(?:=([^>]*?))?(\s+[^>]*)?\s*(/?)>";
    var matches = Regex.Matches(text, tagPattern);

    foreach (Match match in matches)
    {
        string fullTag = match.Value;
        bool isClosing = match.Groups[1].Value == "/";
        string tagName = match.Groups[2].Value.ToLower();
        string tagValue = match.Groups[3].Value; // For tags like voice=1
        string attributes = match.Groups[4].Value;
        bool isSelfClosing = match.Groups[5].Value == "/";
        int position = match.Index;

        // Control tags with values are always self-closing
        if (!string.IsNullOrEmpty(tagValue) && 
            (tagName == "voice" || tagName == "service" || tagName == "label" || 
             tagName == "vid" || tagName == "comment"))
        {
            isSelfClosing = true;
        }

        // Split tag is always self-closing
        if (tagName == "split")
        {
            isSelfClosing = true;
        }

        // Check if tag is valid
        if (!_validTags.Contains(tagName))
        {
            result.Errors.Add(new SSMLError
            {
                Message = $"Unknown tag: <{tagName}>",
                Position = position,
                Length = fullTag.Length,
                Context = fullTag
            });
            continue;
        }

        // Comment tags are always self-closing
        if (tagName == "comment")
        {
            continue;
        }

        if (isSelfClosing)
        {
            // These tags can be self-closing, no validation needed
            if (!_selfClosingTags.Contains(tagName) && 
                tagName != "voice" && tagName != "split" && 
                tagName != "service" && tagName != "label" && 
                tagName != "vid")
            {
                result.Warnings.Add($"Tag <{tagName}> is not typically self-closing");
            }
        }
        else if (isClosing)
        {
            if (tagStack.Count == 0)
            {
                result.Errors.Add(new SSMLError
                {
                    Message = $"Closing tag </{tagName}> has no matching opening tag",
                    Position = position,
                    Length = fullTag.Length,
                    Context = fullTag
                });
            }
            else
            {
                var expectedTag = tagStack.Pop();
                if (expectedTag.Name != tagName)
                {
                    result.Errors.Add(new SSMLError
                    {
                        Message = $"Mismatched closing tag: expected </{expectedTag.Name}>, found </{tagName}>",
                        Position = position,
                        Length = fullTag.Length,
                        Context = $"Opened at position {expectedTag.Position}"
                    });

                    tagStack.Push(expectedTag);
                }
            }
        }
        else
        {
            if (_selfClosingTags.Contains(tagName))
            {
                result.Warnings.Add($"Tag <{tagName}> should be self-closing (use <{tagName} />)");
            }
            else
            {
                tagStack.Push(new SSMLTag { Name = tagName, Position = position });
                ValidateTagAttributes(tagName, attributes, position, result);
            }
        }
    }

    // Check for unclosed tags
    while (tagStack.Count > 0)
    {
        var unclosedTag = tagStack.Pop();
        result.Errors.Add(new SSMLError
        {
            Message = $"Unclosed tag: <{unclosedTag.Name}> at position {unclosedTag.Position}",
            Position = unclosedTag.Position,
            Length = unclosedTag.Name.Length + 2,
            Context = "Tag was never closed"
        });
    }

    return result;
}

        private void ValidateTagAttributes(string tagName, string attributes, int position, SSMLValidationResult result)
        {
            switch (tagName)
            {
                case "say-as":
                    if (!attributes.Contains("interpret-as"))
                    {
                        result.Errors.Add(new SSMLError
                        {
                            Message = "<say-as> tag missing required 'interpret-as' attribute",
                            Position = position,
                            Length = tagName.Length + 2,
                            Context = "Required: interpret-as=\"type\""
                        });
                    }
                    break;

                case "sub":
                    if (!attributes.Contains("alias"))
                    {
                        result.Errors.Add(new SSMLError
                        {
                            Message = "<sub> tag missing required 'alias' attribute",
                            Position = position,
                            Length = tagName.Length + 2,
                            Context = "Required: alias=\"replacement text\""
                        });
                    }
                    break;
            }

            // Check for unpaired quotes
            if (!string.IsNullOrWhiteSpace(attributes))
            {
                int singleQuotes = attributes.Count(c => c == '\'');
                int doubleQuotes = attributes.Count(c => c == '"');

                if (singleQuotes % 2 != 0 || doubleQuotes % 2 != 0)
                {
                    result.Errors.Add(new SSMLError
                    {
                        Message = $"Unpaired quotes in attributes for <{tagName}>",
                        Position = position,
                        Length = tagName.Length + attributes.Length + 2,
                        Context = attributes
                    });
                }
            }
        }
    }
}