# 📦 MultiLingo - Installation & Dependencies

Thank you for choosing MultiLingo! This guide ensures your Unity environment is correctly configured to use all the advanced features of this package.

## 🚀 Automatic Setup
When you first import MultiLingo, a **Setup Wizard** will automatically appear if any required Unity packages are missing. Simply click **"Install All Dependencies"** to have Unity download and configure everything automatically.

## 🛠️ Manual Dependency Management
If the Setup Wizard does not appear, or if you want to verify your installation later:

1. Go to `Tools > Multilingo > Welcome Window`.
2. Click the **🔍 Manage Dependencies** button.
3. The system will check the Unity Package Manager and prompt you if anything is missing.

### Core Required Packages:
*   **Unity Localization (1.4.5+)**: The foundation for string tables and locale management.
*   **TextMeshPro**: Used for advanced font rendering and RTL support.
*   **Addressables**: Required for dynamic loading of localized assets.

## 📁 Package contents
If you are moving files around, ensure the `Editor/Installer` folder remains intact. It contains the following critical files:
*   `MultilingoDependencyInstaller.cs`: The core logic for detecting missing packages.
*   `Multilingo.Installer.asmdef`: A dedicated assembly that allows the installer to run even when the rest of the project has errors.

## ❓ Troubleshooting
**Q: My project is full of red errors after import!**
A: This usually means `Unity Localization` is missing. Use the `Tools > Multilingo > Check Dependencies` menu or wait for the Setup Wizard popup to resolve this.

**Q: The Progress Bar is stuck during installation.**
A: Unity is likely downloading packages in the background. Check the Unity Console for any specific Package Manager errors.
