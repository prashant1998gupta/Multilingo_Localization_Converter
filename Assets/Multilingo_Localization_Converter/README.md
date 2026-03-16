# MultiLingo - Localization Converter & Tools for Unity

**MultiLingo** is your ultimate, all-in-one Unity Editor extension that makes localizing your game incredibly fast and easy. Whether you need to translate your game text into 100+ languages, convert file formats, generate voice-overs, or optimize your fonts, MultiLingo does it all straight from the Unity Editor!

---

## 🌟 What Can MultiLingo Do?

MultiLingo is divided into two main parts:
1. **The Converter & Translator Window**: For working with files.
2. **Unity Localization Tools**: For working directly with your Unity project.

### 1. 🌐 The Converter & Translator
- **Translate Game Text**: Simply drag and drop your spreadsheet (CSV or Excel) and translate it into over 100 languages.
- **Top-Tier AI Integrations**: Choose between **Google**, **OpenAI (ChatGPT)**, or **DeepL** for high-quality translations.
- **Batch Processing**: Translate dozens of files at the same time automatically.
- **Smart Memory (Caching)**: It remembers what you've translated before, saving you time and money.
- **Format Converter**: Need to change an Excel file to JSON? Or CSV to YAML? Convert freely between CSV, XLSX, JSON, XML, and YAML.
- **Quality Checks**: Automatically warns you if translations are missing or if text might be too long to fit in your UI!

### 2. 🛠️ Unity Localization Tools
- **Auto Voice-Over (TTS)**: Let AI generate voice/audio files for all your character dialogues in every language in one click!
- **Auto-Translate Missing Keys**: Directly scan your Unity `String Tables` and translate only the missing entries automatically.
- **Two-Way Sync**: Push your translation files directly into Unity's official Localization system, or pull them out.
- **C# Keys Generator**: Automatically generates code from your file so you can type `LocalizationKeys.PLAY_BUTTON` instead of easily misspelled strings.
- **Scene Text Scanner**: Automatically finds all text in your game scene and exports it to a spreadsheet.
- **Google Sheets Sync**: Update your game text instantly via a public Google Sheets link.
- **Optimize Asian Fonts (CJK)**: Reduces huge font files sizes by keeping only the characters your game actually uses.

---

## 🛠 Installation

Installing MultiLingo is simple! No extra plugins or DLLs are required.

1. Unzip the downloaded folder.
2. Drag and drop the `Multilingo_Localization_Converter` folder anywhere inside your Unity project's `Assets` folder.
3. You're done! MultiLingo is now ready to use.

---

## 📖 Quick Start Guide

For full detailed instructions on every feature, please see the [**DOCUMENTATION.md**](DOCUMENTATION.md) file included in the folder.

### Translating a Spreadsheet
1. Open Unity and click `Tools > Multilingo > Localization Converter` at the top menu.
2. Select **Translator** mode.
3. Drag & drop your `.csv` or `.xlsx` file into the window.
4. Select the languages you want to translate the text into.
5. Choose your AI Translation provider (Google, DeepL, or OpenAI). *(Note: DeepL and OpenAI require your personal API Keys).*
6. Click **Start Processing** and save the newly translated file!

---

## 📋 File Formats Supported
* **Inputs**: `.csv`, `.xlsx`, `.json`, `.xml`, `.yaml`
* **Outputs**: `.csv`, `.xlsx`, `.json`, `.xml`, `.yaml`

## 💡 Notes
- Requires Unity 2020.3 or higher.

Enjoy a much smoother, faster localization experience! 🚀
