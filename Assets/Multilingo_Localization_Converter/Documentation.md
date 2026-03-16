# 📚 The Complete Beginner's Guide to MultiLingo

Welcome to **MultiLingo**! 

If you have never translated a video game before, or if the idea of "programming" and "API Keys" sounds confusing, you are in the right place. **This guide is written for non-programmers.** We will explain every single button, every feature, and exactly how everything works using simple, everyday language.

Think of MultiLingo as your personal, automated translation factory that lives directly inside Unity. It takes the English text you've written for your game (like "Play" or "Settings") and magically turns it into Spanish, French, Japanese, or over 100 other languages.

---

## 🚪 How to Open MultiLingo

To get started, look at the very top menu bar in your Unity window.
1. Click on **Tools**.
2. Go down and hover over **Multilingo**.
3. You will see two options click on either one:
   - **Localization Converter**: This is your main translation factory where you drag and drop files.
   - **Unity Localization Utilities**: These are extra tools to help you manage your game text (like automatically generating voice-overs or finding missing text).

---

## 🏭 Part 1: The Localization Converter Window

*(Found at: `Tools > Multilingo > Localization Converter`)*

When you open this window, you will see a setting at the top called **Mode**. You can choose between **Translator** and **Converter**.

### 🔀 The "Converter" Mode
Sometimes, you already have a translated file, but it's in the wrong "shape" (format). For example, your friend sends you an Excel spreadsheet (`.xlsx`), but your game needs a special web file called JSON (`.json`) or a text file called CSV (`.csv`).

This mode simply changes the *shape* of the file. It does **not** translate anything.

**Step-by-Step Instructions:**
1. At the top of the window, switch the Mode to **Converter**.
2. **Input File**: Find your original file on your computer. Drag the file with your mouse and drop it into the little box that says "Input File".
3. **Output Format**: Click this dropdown menu. Choose the shape you want the file to become. (If you aren't sure, **CSV** or **JSON** are usually the best choices for Unity).
4. Click the big **Start Processing** button.
5. Unity will ask you where you want to save your shiny new file. Choose a folder on your computer and click Save!

### 🌎 The "Translator" Mode
This is the main event! This mode uses powerful AI (like ChatGPT or Google Translate) to automatically read your game's English text, translate it into as many languages as you want, and give you a brand new spreadsheet.

**Step-by-Step Instructions:**

**Step 1. Prepare your Spreadsheet**
Before using this tool, you need a spreadsheet. You can make this in Microsoft Excel, Google Sheets, or Apple Numbers.
It should be very simple:
- Make the top row your "Headers" (e.g., in column A write "ID", in column B write "English").
- Fill out the rows below it (e.g., Column A = "btn_play", Column B = "Play Game").
- Save it to your computer as a `.csv` or `.xlsx` file.

**Step 2. Load it into MultiLingo**
1. At the top of the MultiLingo window, switch the Mode to **Translator**.
2. Drag and drop your spreadsheet into the **Input File** box.
3. MultiLingo will scan your file! A new box will appear called **Source Column**. Click the dropdown and tell MultiLingo which column holds your English text.

**Step 3. Choose your Languages**
You will see a massive list of flags representing different countries.
- Click the little checkbox next to every language you want your game to support (for example, Spanish, French, German, Japanese).

**Step 4. Choose your AI Engine**
MultiLingo needs a brain to do the translating. In the settings, you can pick which AI brain you want to hire.
- **Google Free**: This is totally free and requires no setup. However, it is meant for small bits of text. If your game has a massive story with thousands of words, Google Free might get "tired" and block you temporarily.
- **OpenAI (ChatGPT)**: This is highly recommended! It is incredibly smart and understands the context of your game, making the translations very natural.
- **DeepL**: Highly recommended for European languages. It is famous for being extremely accurate.
- **Google Cloud**: A professional paid version of Google Translate.

**Step 5. Entering your API Key (The VIP Password)**
If you chose OpenAI, DeepL, or Google Cloud, you must give MultiLingo an **API Key**. 
- *What is an API Key?* Think of it as a VIP password. The AI company gives you a special password (a long string of random letters and numbers) to prove that it is actually *you* asking for translations.
- *How do I get one?* Go to the OpenAI or DeepL website, create a free account, put in your billing info (translations cost pennies, usually less than $1 per game!), and click a button that says "Generate API Key". 
- Paste that long password into the box in MultiLingo. MultiLingo will save it securely so you never have to type it again.

**Step 6. Add Context (Optional but helpful!)**
The AI doesn't know if your word "Bat" means a baseball bat or a scary flying animal!
- Next to the languages, you can type in your **Project Name** and a basic description of your game. This tells the AI what to expect!

**Step 7. Start!**
- Click the big **Start Processing** button.
- A loading bar will show you the progress. Sit back and relax while the AI translates thousands of words in seconds.
- When it's finished, it will ask you where to save the multi-language spreadsheet. Choose a folder and click Save!

*(⭐ Smart Memory Feature: MultiLingo has a built-in brain. If you translate the English word "Apple" into Spanish ("Manzana"), MultiLingo saves it in a hidden fast-memory file. If you ever try to translate "Apple" into Spanish again in the future, MultiLingo will instantly remember "Manzana" without having to ask the AI. This saves you tons of time and money!)*

---

## 🚀 Part 2: Unity Localization Utilities

*(Found at: `Tools > Multilingo > Unity Localization Utilities`)*

This window is like a Swiss Army Knife for game text. Across the top of the window, you will see different tabs. Here is exactly what each tab does.

### 🎙️ 1. Auto Voice-Over (Character Speech)
Imagine your character says "Hello!" in English, and you want them to actually speak it out loud. Now imagine doing that for Spanish, French, and Japanese. Hiring voice actors for all those languages would cost thousands of dollars!

This tool uses OpenAI's amazing Text-to-Speech robot to generate highly realistic voice files automatically.
1. Make sure your OpenAI API Key is entered.
2. Select your text file (or Unity String Table) containing your translated text.
3. Choose a Voice Actor style (e.g., "Alloy" is generic, "Fable" is a British accent, "Onyx" is a deep male voice).
4. Click **Generate Audio Clips**.
MultiLingo will read every line of text, generate an Audio file (`.mp3`), and neatly organize them in your project folder!

### 🔍 2. Missing Keys Auto-Translator
Let's say you spent hours fully translating your game. But then, right before release, you decide to add a new "Settings" button. Instead of having to translate an entire spreadsheet all over again just for one little button, use this tool!
1. It scans your existing Unity setup.
2. It finds any blank spots (for example, it finds the French translation for "Settings" is totally empty).
3. It asks the AI to translate *only* those missing words and plugs them in automatically.

### 🔗 3. Two-Way Sync (Importing & Exporting)
Unity has a built-in system to display text on screen called the "Localization Package". But typing words directly into Unity's system is very slow and frustrating. Most people prefer working in Excel or Google Sheets. This tab connects the two!

- **Import to Unity**: If you have a fully translated Excel spreadsheet, use this option to smoothly copy all the text from the spreadsheet directly into Unity's internal system.
- **Export to CSV**: If you did type all your text inside Unity, use this option to "spit it out" into a clean Excel spreadsheet. You can then email this spreadsheet to human translators to check your work!

### 💻 4. Code Generator (For Scripters)
*(If you do not write programming code, you can ignore this tab!)*
Programmers use "IDs" to find text. For example, to make a button say "Play", they might tell the code to find the ID `"Menu_Play"`. But if they accidentally type `"Menu_Playy"`, the game breaks!
1. Load your spreadsheet here and click Generate.
2. It writes a C# script containing every ID in your game.
3. Now, the programmer can just type `LocalizationKeys.MENU_PLAY` and their code editor will auto-complete it, guaranteeing they never misspell an ID again.

### 🔎 5. Scene Text Scanner
Did you build an entire level with text on signs, buttons, and walls, but you forgot to write them down in a spreadsheet? This feature is a lifesaver.
1. Open up your level or menu scene in Unity.
2. Go to this tab and click **Scan Scene**.
3. MultiLingo acts like a detective, searches your entire scene, and builds a list of every single word it finds on screen.
4. Click **Export to CSV** to save this list as a spreadsheet. Now you have the perfect starting point to translate your game!

### ☁️ 6. Google Sheets Sync
Would you like to fix typos in your game without even opening Unity?
1. Upload your main translation spreadsheet to Google Sheets (on your internet browser) and make it "Public".
2. Copy the URL link of that Google Sheet and paste it into this MultiLingo tab.
3. Now, whenever you or your friends fix a typo online in Google Sheets, you just click the **Sync** button in Unity! MultiLingo will instantly download the newest version and update your game text immediately. 

### 🔠 7. Font Optimizer (Saving File Size)
English is easy—it only has 26 letters! But languages like Japanese, Chinese, and Korean have *thousands* of complex symbols. If you try to put a full Chinese font (like NotoSans) into your game, the font file alone could be 30 Megabytes, bloating your game's file size!

This tool is the ultimate optimization trick.
1. Give the tool your translated file.
2. It reads every single Chinese word in your game, and makes a list of *only the specific symbols you actually used*.
3. Let's say your 5-hour game only uses 800 unique Chinese symbols. MultiLingo groups those 800 symbols tightly together.
4. You can take that small group of symbols to the Unity Font Creator, and tell it to throw away the thousands of symbols you aren't using.
5. Your font file shrinks from 30 Megabytes down to just tiny Kilobytes!

---

## 🛠 Help & Troubleshooting for Beginners

**"The Tool is saying API Key Invalid"**
Don't panic! 99% of the time, this means you accidentally highlighted an invisible "Space" when copy-pasting your API password from the OpenAI/DeepL website. Go back to the settings box in MultiLingo, click inside the box, and make sure there are no spaces or gaps at the beginning or end of your API Key. 

**"My AI Translations stopped working and are failing!"**
- **If you use Google Free**: Google has a hidden security system. If you try translating 5,000 words in 10 seconds, Google's Free service will think you are a spam virus and block you. Don't worry, the block goes away after an hour or two. Next time, try translating smaller chunks, or switch to OpenAI.
- **If you use OpenAI or DeepL**: Go to their website and check your account balance. You might have run out of pre-paid credits! Remember, you must set up a payment method on their website for the API key to activate.

**"The translation takes a very long time"**
Translating 10,000 words into 15 languages requires a massive amount of processing power from the AI. Just grab a cup of coffee and let it run! It is still infinitely faster than a human.

Enjoy using MultiLingo! Building a game that the whole world can understand is now easier than ever.
