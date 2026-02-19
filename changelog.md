📜 Changelog
All notable changes to SortMyMedia will be documented in this file.

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
