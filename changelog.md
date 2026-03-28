📜 Changelog
All notable changes to SortMyMedia are documented in this file.

🟦 [2.0.0] – 2026‑03‑XX
✨ Added
New Start Screen with three modes: Sort Mode, OCR Mode, Face Mode
Face Mode (brand‑new):
SCRFD face detection (det_500m_fixed.onnx)
AdaFace IR‑101 embeddings (adaface_ir101_webface12m.onnx)
5‑point face alignment
Multi‑crop embedding for robustness
Quality filtering (sharpness, pose, detection score, skin detection)
GPU acceleration via ONNX Runtime CUDA
Fully interactive clustering UI (drag & drop, merge, split, undo/redo)
Automatic per‑person folder export
OCR Mode (brand‑new):
PaddleOCR FastAPI server integration
.txt and interactive .html output with bounding boxes
Automatic language detection
GPU acceleration when available
Full Windows Installer (Inno Setup):
Automatic installation of .NET Desktop Runtime 10
All ONNX models included
ExifTool included
Start Menu shortcuts
Uninstaller
Improved HEIC support via Magick.NET fallback
New UI elements and modernized layout
🛠️ Fixed
More stable multithreaded processing across all modes
Improved error handling for corrupted or unreadable images
More robust handling of large datasets in Face Mode and Sort Mode

⚡ Improved
Faster file discovery
More accurate metadata extraction
Better fallback logic for missing EXIF/QuickTime metadata
Higher accuracy in face clustering due to quality‑weighted embeddings

🟦 [1.2.0] – 2026‑02‑18 ✨ Added / 🛠️ Fixed
✨ Added
-	Full and reliable HEIC metadata extraction now works as intended
- ExifTool dependency clarified:  and the  directory are now included in the ZIP
- Improved documentation to prevent incorrect NO_DATE results in the future
- Added new TestEngine for safe performance experimentation without affecting ClassicEngine.
- Replaced `Directory.GetFiles` with `Directory.EnumerateFiles` for faster file discovery and reduced memory usage.
- Implemented `HashSet<string>` for efficient extension filtering.
- Improved multithreaded processing stability and throughput.

🛠️ Fixed
- Critical issue where ExifTool could not run because the required  directory was missing
- HEIC files incorrectly ending up in 
- Fallback system now only triggers when ExifTool truly cannot extract metadata
- Only genuinely problematic files (e.g., corrupted or 0 KB) end up in 

🟦 [1.1.1] – 2026‑02‑15
🛠️ Fixed
- Updated UI language:
- “Sorteren op” → “Sort by”
- “per dag / per maand” → “by day / by month”
- No functional changes
- Stability‑only update

🟦 [1.1] – 2026‑02‑15
✨ Added
- Support for Google Takeout .supplemental-metadata.json files
- JSON fallback when EXIF/QuickTime dates are missing or invalid
- Prefix‑based matching for long filenames
- Full HEIC/HEIF date extraction support via ExifTool
- ExifTool is now included directly in the ZIP (no installation required)

🛠️ Fixed
- Videos incorrectly ending up in NO_DATE despite valid JSON metadata
- QuickTime epoch issues (1904/0000 dates) are now properly handled
- More robust error handling for missing or corrupted metadata


⚡ Improved
- Faster and more reliable processing of large datasets
- More accurate matching between media files and JSON metadata
- Cleaner and more consistent logging


🟩 [1.0] – 2026‑02‑10
🚀 Initial Release
- EXIF and QuickTime metadata extraction
- Multithreaded processing engine
- Automatic folder structure (photos/videos → year → month/day)
- Support for JPG, PNG, TIFF, WEBP, MP4, MOV, M4V, HEIC, HEIF
- Basic NO_DATE fallback
