# SSML Template Library & Math Detection - User Guide

## 🎯 Overview

Two powerful new features to make SSML tagging faster and more accurate, especially for mathematical content:

1. **SSML Template Library** - Pre-built SSML snippets for common patterns
2. **Smart Math Detection** - Auto-detect math expressions and suggest SSML markup

---

## 📚 SSML Template Library

### How to Access:
- **Menu:** Tools → SSML Template Library...
- **Or add toolbar button:** 📚 Templates

### What It Does:
Browse and insert pre-built SSML snippets organized by category:
- **Math** (13 templates)
- **Emphasis** (2 templates)
- **Pauses** (3 templates)
- **Speed** (3 templates)
- **Volume** (2 templates)
- **Format** (5 templates)
- **Special** (1 template)

### How to Use:

#### **Method 1: Insert at Cursor**
1. Position cursor where you want the tag
2. Open SSML Template Library
3. Find template (e.g., "Strong Emphasis")
4. Click "📝 Insert at Cursor"
5. Template inserted with {text} placeholder

#### **Method 2: Wrap Selected Text**
1. Select text in editor: `This is important`
2. Open SSML Template Library
3. Click "Strong Emphasis"
4. Result: `<emphasis level="strong">This is important</emphasis>`

### Math Templates:

#### **1. Fraction**
```
Original: 1/2
Template: <say-as interpret-as="fraction">1/2</say-as>
Spoken as: "one half"
```

#### **2. Exponent**
```
Original: x^2
Template: x to the <say-as interpret-as="ordinal">2</say-as> power
Spoken as: "x to the second power"
```

#### **3. Square Root**
```
Original: √25
Template: the square root of 25
Spoken as: "the square root of 25"
```

#### **4. Subscript**
```
Original: H_2O
Template: H sub 2 O
Spoken as: "H sub two O"
```

#### **5. Equation (Complex)**
```
Original: a = b + c
Template: <prosody rate="slow">a = b + c</prosody>
Effect: Spoken slowly for clarity
```

#### **6. Variable Emphasis**
```
Original: x
Template: <emphasis level="moderate">x</emphasis>
Effect: Slight emphasis on variable
```

---

## 🔢 Smart Math Detection

### How to Access:
- **Menu:** Tools → Analyze Math & Suggest SSML...
- **Or toolbar button:** 🔢 Analyze Math

### What It Detects:

#### **1. Fractions** (1/2, 3/4, 5/8)
```
Detects: 1/2
Suggests: <say-as interpret-as="fraction">1/2</say-as>
Reads as: "one half"
```

#### **2. Exponents** (x^2, 10^3, e^x)
```
Detects: x^2
Suggests: x to the <say-as interpret-as="ordinal">2</say-as> power
Reads as: "x to the second power"
```

#### **3. Square Roots** (√25, sqrt(16))
```
Detects: √25
Suggests: the square root of 25
Reads as: "the square root of 25"
```

#### **4. Subscripts** (x_1, H_2O, a_n)
```
Detects: x_1
Suggests: x sub 1
Reads as: "x sub one"
```

#### **5. Equations** (a = b + c)
```
Detects: a = b + c
Suggests: <prosody rate="slow">a = b + c</prosody>
Effect: Slows down equation reading
```

#### **6. Percentages** (25%, 0.5%)
```
Detects: 25%
Suggests: <say-as interpret-as="cardinal">25</say-as> percent
Reads as: "twenty-five percent"
```

#### **7. Decimals** (3.14, 0.5)
```
Detects: 3.14
Suggests: 3 point 1 4
Reads as: "three point one four"
```

#### **8. Scientific Notation** (1.5e10, 3E-5)
```
Detects: 1.5e10
Suggests: 1.5 times ten to the <say-as interpret-as="ordinal">10</say-as> power
Reads as: "one point five times ten to the tenth power"
```

### How to Use:

#### **Step 1: Analyze**
1. Type or paste your text with math expressions
2. Click Tools → Analyze Math & Suggest SSML
3. Wait for analysis (instant for most documents)

#### **Step 2: Review Suggestions**
- Color-coded cards for each detected expression
- Shows:
  - **Original:** What was detected
  - **Suggested SSML:** How to mark it up
  - **Explanation:** How it will be spoken

#### **Step 3: Apply**
- **Apply Single:** Click "Apply" on individual suggestion
- **Apply All:** Click "✓ Apply All" to apply all suggestions at once

### Example Workflow:

**Original Text:**
```
The formula for area is A = πr^2. For a circle with radius 5, 
the area is approximately 78.54. That's about 25% larger than 
a square with the same radius. The ratio is 1/4 compared to π.
```

**After Analysis - 6 Suggestions:**
1. **Exponent** `r^2` → `r to the 2nd power`
2. **Decimal** `78.54` → `78 point 5 4`
3. **Percentage** `25%` → `25 percent`
4. **Fraction** `1/4` → `one quarter`
5. **Equation** `A = πr^2` → (slowed down)
6. **Variable** `π` → (emphasized)

**After Applying All:**
```
The formula for area is <prosody rate="slow">A = πr to the <say-as interpret-as="ordinal">2</say-as> power</prosody>. 
For a circle with radius 5, the area is approximately 78 point 5 4. 
That's about <say-as interpret-as="cardinal">25</say-as> percent larger than a square with the same radius. 
The ratio is <say-as interpret-as="fraction">1/4</say-as> compared to π.
```

---

## 🎓 Real-World Examples

### **Example 1: Physics Equation**

**Original:**
```
Einstein's famous equation E = mc^2 states that energy equals 
mass times the speed of light squared.
```

**After Auto-Detection:**
```
Einstein's famous equation <prosody rate="slow">E = mc to the <say-as interpret-as="ordinal">2</say-as> power</prosody> 
states that energy equals mass times the speed of light squared.
```

### **Example 2: Statistics Problem**

**Original:**
```
The probability is 0.75, or 75%, with a margin of error of ±3%.
The sample size was n = 1000, and the confidence interval was 95%.
```

**After Auto-Detection:**
```
The probability is 0 point 7 5, or <say-as interpret-as="cardinal">75</say-as> percent, 
with a margin of error of plus or minus <say-as interpret-as="cardinal">3</say-as> percent.
The sample size was n equals 1000, and the confidence interval was 
<say-as interpret-as="cardinal">95</say-as> percent.
```

### **Example 3: Chemistry Formula**

**Original:**
```
Water (H_2O) consists of 2 hydrogen atoms and 1 oxygen atom.
The chemical equation is 2H_2 + O_2 → 2H_2O.
```

**After Auto-Detection:**
```
Water (H sub 2 O) consists of 2 hydrogen atoms and 1 oxygen atom.
The chemical equation is 2 H sub 2 plus O sub 2 yields 2 H sub 2 O.
```

### **Example 4: Calculus**

**Original:**
```
The derivative of x^3 is 3x^2. At x = 2, the slope is 12.
```

**After Auto-Detection:**
```
The derivative of x to the <say-as interpret-as="ordinal">3</say-as> power is 
3 x to the <say-as interpret-as="ordinal">2</say-as> power. 
At x equals 2, the slope is 12.
```

---

## 💡 Tips & Best Practices

### **1. Run Analysis After Editing**
- Math detection works on the current text
- Re-run after making major edits
- Suggestions won't duplicate if already applied

### **2. Review Before Applying All**
- Some auto-detections might be false positives
- Review the "Explanation" for each suggestion
- Apply individually if unsure

### **3. Combine with Manual Tags**
- Auto-detection finds common patterns
- You can still manually add tags for special cases
- Use Template Library for uncommon patterns

### **4. Test with Preview**
- After applying suggestions, use 🔊 Preview button
- Listen to how it sounds
- Adjust if needed

### **5. Save Custom Templates**
- If you use a pattern repeatedly
- Consider adding it to the template library
- (Future feature: user-defined templates)

---

## 🔧 Keyboard Shortcuts

Add these to your workflow:

```
Ctrl+Shift+T  → Open SSML Template Library (if you add shortcut)
Ctrl+Shift+M  → Analyze Math (if you add shortcut)
Ctrl+Shift+C  → Colorize tags
```

---

## 🐛 Troubleshooting

### **Problem: Math not detected**
**Solution:** 
- Make sure you're using standard notation (x^2, not x²)
- Subscripts need underscore: x_1 not x₁
- Try different notation (√ or sqrt())

### **Problem: Too many false positives**
**Solution:**
- Apply suggestions individually
- Skip suggestions that don't make sense
- Some patterns (like dates) might be detected as fractions

### **Problem: SSML doesn't work during playback**
**Solution:**
- Not all TTS engines support all SSML tags
- Windows SAPI: Limited SSML support
- Google/AWS/ElevenLabs: Better SSML support
- Test with 🔊 Preview to verify

---

## 📊 Supported Patterns Summary

| Pattern | Example | Detection | SSML Output |
|---------|---------|-----------|-------------|
| Fractions | 1/2, 3/4 | ✅ Auto | `<say-as interpret-as="fraction">` |
| Exponents | x^2, 10^3 | ✅ Auto | `to the Nth power` |
| Square Roots | √25, sqrt(16) | ✅ Auto | `square root of N` |
| Subscripts | x_1, H_2O | ✅ Auto | `X sub N` |
| Equations | a = b + c | ✅ Auto | `<prosody rate="slow">` |
| Percentages | 25%, 0.5% | ✅ Auto | `N percent` |
| Decimals | 3.14, 0.5 | ✅ Auto | `N point N` |
| Scientific | 1.5e10 | ✅ Auto | `times ten to the Nth` |
| Complex equations | ∫, ∑, √ | ❌ Manual | Use Template Library |
| Greek letters | α, β, π | ❌ Manual | Use substitution template |
| Matrices | [a b; c d] | ❌ Manual | Custom approach |

---

## 🎬 Quick Start Guide

### **For Math Teachers/Tutors:**
1. Paste your lesson text
2. Click "🔢 Analyze Math"
3. Click "✓ Apply All"
4. Preview and adjust
5. Convert to audio

### **For Technical Writers:**
1. Use Template Library for common patterns
2. Run Math Analysis for formulas
3. Manually add emphasis for key terms
4. Test with target TTS engine

### **For Audiobook Creators:**
1. Use templates for dramatic pauses
2. Combine with voice tags for characters
3. Run analysis for any technical content
4. Preview frequently

---

## 📝 Future Enhancements

Coming soon:
- User-defined custom templates
- More math patterns (integrals, summations)
- LaTeX to SSML conversion
- Save/load template sets
- Batch apply templates to multiple files

---

**Need Help?** Check the Help menu for more guides!
