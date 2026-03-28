<p align="left">

<a href="https://github.com/dees743-cloud/SortMyMedia/releases/latest">
<img src="https://img.shields.io/github/v/release/dees743-cloud/SortMyMedia?color=blue&label=Latest%20Version&style=for-the-badge" alt="Latest Version">
</a>

<a href="https://github.com/dees743-cloud/SortMyMedia/releases">
<img src="https://img.shields.io/github/downloads/dees743-cloud/SortMyMedia/total?color=brightgreen&label=Downloads&style=for-the-badge" alt="Downloads">
</a>

<img src="https://img.shields.io/badge/.NET-10%20Required-purple?style=for-the-badge" alt=".NET 10">

<a href="https://github.com/dees743-cloud/SortMyMedia/blob/master/LICENSE.txt">
<img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="MIT License">
</a>

<a href="https://github.com/dees743-cloud/SortMyMedia/blob/master/changelog.md">
<img src="https://img.shields.io/badge/Changelog-View-blue?style=for-the-badge" alt="Changelog">
</a>

<img src="https://img.shields.io/badge/Made%20in-Belgium 🇧🇪-red?style=for-the-badge" alt="Made in Belgium">

<img src="https://img.shields.io/badge/HEIC-Supported-blueviolet?style=for-the-badge" alt="HEIC Supported">

<img src="https://img.shields.io/badge/Engine-Multithreaded-orange?style=for-the-badge" alt="Multithreaded Engine">

<img src="https://img.shields.io/badge/UI-Windows%2010%2F11%20Modern-lightgrey?style=for-the-badge" alt="Modern Windows UI">

</p>

📦 SortMyMedia
SortMyMedia is a fast, multithreaded Windows application that automatically organizes your photos and videos into clean, structured folders.
Starting with version 2.0, SortMyMedia now includes three powerful modes:

- Sort Mode — automatic date‑based media organization

- OCR Mode — extract text from images using PaddleOCR

- Face Mode — detect, group, and organize photos by person using AdaFace + SCRFD

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
- 🆕 Version 2.0 — Major Upgrade
- SortMyMedia 2.0 introduces two completely new AI-powered modes and a redesigned start screen.

🤖 Face Mode (New in 2.0)
- Face Mode detects faces, generates embeddings, groups people, and exports photos into per‑person folders.
    🔍 Pipeline
    - SCRFD face detection
    - 5‑point alignment
    - Quality filtering (sharpness, pose, detection score, skin detection)
    - AdaFace IR‑101 embeddings
    - Cosine‑distance clustering
    - Interactive drag‑and‑drop UI
    - Auto‑naming and per‑person folder export

    🧠 Models Included
    - det_500m_fixed.onnx (SCRFD detection)
    - adaface_ir101_webface12m.onnx (AdaFace embeddings)
    
    ⚡ GPU Acceleration
    - ONNX Runtime CUDA for detection & embedding
    - CPU fallback automatically

🔤 OCR Mode (New in 2.0)
-OCR Mode extracts text from images using a local PaddleOCR server.

    📄 Output
    - ocr/<filename>.txt
    - ocr/<filename>.html (interactive bounding boxes)
    
    🧠 Powered by
    - PaddleOCR (FastAPI server)
    - Automatic language detection
    - GPU acceleration when available

🖥️ New Installer (New in 2.0)
- SortMyMedia now includes a full Windows installer:
- Installs .NET Desktop Runtime 10 automatically
- Includes all ONNX models
- Includes ExifTool
- Creates Start Menu shortcuts
- Includes an uninstaller
- Portable ZIP version is still available.

🧭 Version 1.2 — Still Available
- Version 1.2 remains available for users who only need the classic sorting functionality.
✔ What’s fixed in 1.2
- ExifTool now runs correctly for all supported formats
- HEIC metadata extraction fully reliable
- Faster file discovery (EnumerateFiles)
- Reduced memory usage
- More stable multithreaded processing
- Fewer NO_DATE cases
- Only truly problematic files end up in NO_DATE

⚠ Important
To use SortMyMedia 1.2 correctly, the following must be placed next to SortMyMedia.exe:

Code
exiftool.exe
exiftool_files\

🏎️ Performance
- SortMyMedia outperforms similar tools thanks to its fully parallelized engine.
- 10,000 faces → ~45 seconds embedding on RTX 4060
- Clustering < 1 second

📦 Download
Download the latest version here:
👉 Releases → Latest

Available formats:
✔ Installer (recommended)
SortMyMedia_v2.0_Setup.exe
✔ Portable ZIP
SortMyMedia-2.0.zip
✔ Legacy version
SortMyMedia-1.2.zip

📁 Output Structure
Code
photos/
   2024/
      2024-08-15/
videos/
   2023/
      2023-11/
NO_DATE/
Face Mode output:

Code
People/
   John/
   Sarah/
   Unknown Faces/
OCR Mode output:

Code
ocr/
   image1.txt
   image1.html
📦 Requirements
- Version 2.0
  - Windows 10/11
  - .NET Desktop Runtime 10 (installed automatically by the installer)
  - PaddleOCR server (for OCR Mode only)

- Version 1.2
  - Windows 10/11
  - .NET 6+
  - Exiftool 

📜 License
MIT License
