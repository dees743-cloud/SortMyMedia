# Roadmap

This document outlines the planned features and improvements for future versions of SortMyMedia.  
The roadmap is not final and may evolve based on testing, feedback, and new ideas.

---

## 🚀 Version 1.2 — Planned
### Features
- **FAST mode** (EXIF/QuickTime only) for maximum performance  
- **DEEP mode** (EXIF + JSON + fallback) for 100% accuracy  
- **Integrated ExifTool** (embedded inside the application)  
- **Single-file executable** (optional): bundle all dependencies into one .exe  
- Improved UI with clearer mode selection  
- Optional summary report after sorting (files processed, skipped, errors)

### Improvements
- More efficient JSON lookup for large Google Takeout exports  
- Better detection of corrupted media files  
- Minor UI polish and layout refinements

---

## 🧭 Version 1.3 — Planned
### Features
- Preview mode: show how files *would* be sorted without moving them  
- Configurable output structure (year/month/day or year/month only)  
- Option to merge photos and videos into a single timeline folder

### Improvements
- Faster HEIC/HEIF handling  
- More detailed logging options

---

## 🌟 Future Ideas (no version assigned yet)
- Dark mode UI  
- Drag‑and‑drop support  
- Multi‑folder batch processing  
- Duplicate detection (hash‑based)  
- Optional database for tracking processed files  
- Plugin system for custom metadata extractors

---

## ✔ Completed
### Version 1.1
- JSON fallback system  
- Google Takeout support  
- Prefix matching for long filenames  
- Eliminated false NO_DATE cases  
- Improved HEIC handling  
- Faster and more reliable processing
