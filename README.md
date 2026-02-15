📦 SortMyMedia
SortMyMedia is a fast, multithreaded Windows application that automatically organizes your photos and videos into clean, structured folders.
It reads EXIF and QuickTime metadata, detects creation dates, and sorts your media per day or per month — all while keeping photos and videos in separate folders for maximum clarity.


✨ Features
- 🚀 High‑performance sorting using parallel processing
- 🗂️ Automatic folder structure (photos/videos → year → month/day)
- 📸 Accurate date detection via EXIF, QuickTime metadata & ExifTool for HEIC
- 🧩 Google Takeout JSON support (*.supplemental-metadata.json)
- 🔍 Smart fallback system
- JSON fallback when EXIF/QuickTime dates are missing or invalid
- Fixes for 1904/0000 QuickTime epoch issues
- 🎥 Separate photo/video output for clean organization
- 🪟 Simple, clean Windows UI
- 🔧 Supports JPG, PNG, TIFF, WEBP, MP4, MOV, M4V, HEIC, HEIF

🚀 Version 1.1 Improvements
- JSON fallback for invalid EXIF/QuickTime dates
- Correct handling of Google Takeout .supplemental-metadata.json
- Improved prefix matching for long filenames
- Eliminated false NO_DATE cases
- Faster and more reliable processing
- More robust HEIC date extraction

🏎️ Performance
SortMyMedia outperforms similar tools thanks to its fully parallelized file processing.
Example benchmark on 31GB of mixed media:
- Competing tool: 15m 20s
- SortMyMedia v1.0: 12m 36s
- SortMyMedia V1.1 (with JSON fallback): ~14 minutes, but 100% accurate

📦 Download
Download the latest version here:
👉 Releases → Latest (ZIP included

📁 Output Structure
photos/
   2024/
      2024-08-15/
videos/
   2023/
      2023-11/
NO_DATE/

📁 📦 Requirements
- Windows 10/11  
- .NET 6+  
- ExifTool

📜 License
MIT License
