# Changelog

All notable changes to SortMyMedia will be documented in this file.

## [1.1] – 2026-02-15
### Added
- Support for Google Takeout `.supplemental-metadata.json` files
- JSON fallback when EXIF/QuickTime dates are missing or invalid
- Prefix‑based matching for long filenames
- Full HEIC/HEIF date extraction support via ExifTool

### Fixed
- Issue where some videos incorrectly ended up in **NO_DATE** despite valid JSON metadata
- QuickTime epoch problems (1904/0000 dates) are now properly handled
- More robust error handling for missing or corrupted metadata

### Improved
- Faster and more reliable processing of large datasets
- More accurate matching between media files and JSON metadata
- Cleaner and more consistent logging

---

## [1.0] – 2026-02-10
### Initial Release
- EXIF and QuickTime metadata extraction
- Multithreaded processing engine
- Automatic folder structure (photos/videos → year → month/day)
- Support for JPG, PNG, TIFF, WEBP, MP4, MOV, M4V, HEIC, HEIF
- Basic NO_DATE fallback
