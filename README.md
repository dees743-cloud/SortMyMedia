<p align="left">

  <!-- Version badge -->
  <a href="https://github.com/dees743-cloud/SortMyMedia/releases/latest">
    <img src="https://img.shields.io/github/v/release/dees743-cloud/SortMyMedia?color=blue&label=Version&style=for-the-badge" alt="Latest Version">
  </a>

  <!-- Downloads badge -->
  <a href="https://github.com/dees743-cloud/SortMyMedia/releases">
    <img src="https://img.shields.io/github/downloads/dees743-cloud/SortMyMedia/total?color=brightgreen&label=Downloads&style=for-the-badge" alt="Downloads">
  </a>

  <!-- .NET badge -->
  <img src="https://img.shields.io/badge/.NET-6%2B-purple?style=for-the-badge" alt=".NET 6+">

  <!-- License badge -->
  <a href="https://github.com/dees743-cloud/SortMyMedia/blob/main/LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="MIT License">
  </a>

  <!-- Changelog badge -->
  <a href="https://github.com/dees743-cloud/SortMyMedia/blob/main/CHANGELOG.md">
    <img src="https://img.shields.io/badge/Changelog-View-blue?style=for-the-badge" alt="Changelog">
  </a>

</p>

📦 SortMyMedia
SortMyMedia is a fast, multithreaded Windows application that automatically organizes your photos and videos into clean, structured folders.
It reads EXIF and QuickTime metadata, detects creation dates, and sorts your media by day or by month, while keeping photos and videos in separate folders for maximum clarity.


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
- ExifTool is now included directly in the ZIP (no installation required) ← new

🛠️ Version 1.1.1 (UI Update)
- Updated UI language:
- “Sorteren op” → “Sort by”
- “per dag / per maand” → “by day / by month”
- No functional changes
- Stability‑only update

🆕 Version 1.2.0 – HEIC Metadata Fix
This update resolves a critical issue where ExifTool could not run correctly because the required exiftool_files directory was missing.
✔ What’s fixed
- ExifTool now runs correctly for all supported formats
- HEIC metadata is fully and reliably extracted
- Files that previously ended up incorrectly in the NO_DATE folder (especially HEIC) are now processed correctly
- The fallback system is only used when ExifTool truly cannot extract metadata
- Only genuinely problematic files (e.g., corrupted or 0 KB files) end up in NO_DATE
✔ Important note
To ensure proper functionality, both of the following must be placed next to SortMyMedia.exe:
exiftool.exe
exiftool_files\   (the entire folder)
Without the exiftool_files directory, ExifTool cannot start.

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
