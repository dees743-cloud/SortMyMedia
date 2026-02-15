# SortMyMedia

SortMyMedia is a fast, multithreaded Windows application that automatically organizes your photos and videos into clean, structured folders.  
It reads EXIF and QuickTime metadata, detects creation dates, and sorts your media per day or per month — all while keeping photos and videos in separate folders for maximum clarity.

## ✨ Features
- 🚀 **High‑performance sorting** using parallel processing  
- 🗂️ **Automatic folder structure** (photos/videos → year → month/day)  
- 📸 **Accurate date detection** via EXIF, QuickTime metadata & ExifTool for HEIC  
- 🎥 **Separate photo/video output** for clean organization  
- 🧠 **Fallback handling** for files without metadata  
- 🪟 **Simple, clean Windows UI**  
- 🔧 **Supports JPG, PNG, TIFF, WEBP, MP4, MOV, M4V, HEIC, HEIF**

## 🏎️ Performance
SortMyMedia outperforms similar tools thanks to its fully parallelized file processing.  
Example benchmark on 31GB of mixed media:

- Competing tool: **15m 20s**  
- SortMyMedia: **12m 36s**

## 📦 Requirements
- Windows 10/11  
- .NET 6+  
- ExifTool

## 📁 Output Structure# MediaSorter
photos/ 2024/ 2024-08-15/ videos/ 2023/ 2023-11/ NO_DATE/


## 📜 License
MIT License
