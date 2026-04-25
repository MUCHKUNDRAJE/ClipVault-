# 📋 ClipVault

**ClipVault** is a modern, high-performance clipboard manager for Windows. It automatically tracks your clipboard history, categorizes content (text, links, code, emails), and stores it securely in a local SQLite database for quick retrieval.

![ClipVault Header](https://via.placeholder.com/800x200?text=ClipVault+Clipboard+Manager)

![ClipVault Preview](clipvaultfull.png)

## ✨ Features

- **Automatic Monitoring**: Real-time clipboard tracking that works silently in the background.
- **Smart Categorization**: Automatically detects and tags content:
  - 📝 **Plain Text**
  - 🔗 **Links/URLs**
  - 💻 **Code Snippets**
  - 📧 **Emails**
  - 🖼️ **Images** (Coming Soon)
- **Local Storage**: All data is stored locally in a SQLite database (`clips.db`) for privacy and speed.
- **Modern UI**: Built with WPF, featuring a clean, responsive design with smooth animations.
- **Search & Filter**: Quickly find previous clippings with powerful filtering options.
- **System Tray Integration**: Minimize to tray for a clutter-free workspace.

## 🛠️ Technology Stack

- **Framework**: .NET 10 (WPF)
- **Language**: C#
- **Database**: SQLite (Microsoft.Data.Sqlite)
- **UI Components**: MahApps.Metro.IconPacks (Lucide)

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows OS

### Installation & Running
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ClipVault.git
   ```
2. Navigate to the project directory:
   ```bash
   cd ClipVault
   ```
3. Run the application:
   ```bash
   dotnet run
   ```

## 📁 Project Structure

- `MainWindow.xaml`: Main interface and clipboard list view.
- `DatabaseHelper.cs`: Logic for SQLite database interactions.
- `ClipModel.cs`: Data model for clipboard entries.
- `App.xaml`: Application-wide resources and startup logic.

## 📄 License

This project is licensed under the MIT License.
