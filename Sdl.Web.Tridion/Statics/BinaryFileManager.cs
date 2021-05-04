using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Tridion.Providers.Binary;
using Image = System.Drawing.Image; // TODO: Shouldn't use System.Drawing namespace in a web application.

namespace Sdl.Web.Tridion.Statics
{
    /// <summary>
    /// Ensures a Binary file is cached on the file-system from the Tridion Broker DB
    /// </summary>
    public class BinaryFileManager
    {
        #region Inner classes
        internal class Dimensions
        {
            internal int Width;
            internal int Height;
            internal bool NoStretch;

            /// <summary>
            /// Returns a string that represents the current object.
            /// </summary>
            /// <returns>
            /// A string that represents the current object.
            /// </returns>
            public override string ToString()
            {
                return $"(W={Width}, H={Height}, NoStretch={NoStretch})";
            }
        }
        #endregion

        /// <summary>
        /// Gets the singleton BinaryFileManager instance.
        /// </summary>
        internal static BinaryFileManager Instance { get; } = new BinaryFileManager();

        internal static IBinaryProvider Provider
            // Default to CIL binary provider if no implementation specified
            => SiteConfiguration.BinaryProvider ?? new CILBinaryProvider();

        private static bool IsCached(Func<DateTime> getLastPublishedDate, string localFilePath,
            Localization localization)
        {
            DateTime lastPublishedDate = SiteConfiguration.CacheProvider.GetOrAdd(
                localFilePath,
                CacheRegions.BinaryPublishDate,
                getLastPublishedDate
                );

            if (localization.LastRefresh != DateTime.MinValue && localization.LastRefresh.CompareTo(lastPublishedDate) < 0)
            {
                //File has been modified since last application start
                Log.Debug(
                    "Binary at path '{0}' is modified",
                    localFilePath);
                return false;
            }

            FileInfo fi = new FileInfo(localFilePath);
            if (fi.Length > 0)
            {
                DateTime fileModifiedDate = File.GetLastWriteTime(localFilePath);
                if (fileModifiedDate.CompareTo(lastPublishedDate) >= 0)
                {
                    Log.Debug("Binary at path '{0}' is still up to date, no action required", localFilePath);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the cached local file for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path.</param>
        /// <param name="localization">The Localization.</param>
        /// <returns>The path to the local file.</returns>
        internal string GetCachedFile(string urlPath, Localization localization, out MemoryStream memoryStream)
        {
            memoryStream = null;
            IBinaryProvider provider = Provider;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFilePath = $"{baseDir}/{urlPath}";
            if (File.Exists(localFilePath))
            {
                // If our resource exists on the filesystem we can assume static content that is
                // manually added to web application.
                return localFilePath;
            }
            // Attempt cache location with fallback to retrieval from content service.
            localFilePath = $"{baseDir}/{localization.BinaryCacheFolder}/{urlPath}";
            using (new Tracer(urlPath, localization, localFilePath))
            {
                Dimensions dimensions;
                urlPath = StripDimensions(urlPath, out dimensions);
                if (File.Exists(localFilePath))
                {
                    if (IsCached(() => provider.GetBinaryLastPublishedDate(localization, urlPath), localFilePath, localization))
                    {
                        return localFilePath;
                    }
                }

                var binary = provider.GetBinary(localization, urlPath);
                WriteBinaryToFile(binary.Item1, localFilePath, dimensions, out memoryStream);
                return localFilePath;
            }
        }

        /// <summary>
        /// Gets the cached local file for a given binary Id.
        /// </summary>
        /// <param name="binaryId">The binary Id.</param>
        /// <param name="localization">The Localization.</param>
        /// <returns>The path to the local file.</returns>
        internal string GetCachedFile(int binaryId, Localization localization, out MemoryStream memoryStream)
        {
            memoryStream = null;
            IBinaryProvider provider = Provider;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFilePath = $"{baseDir}/{localization.BinaryCacheFolder}";
            using (new Tracer(binaryId, localization, localFilePath))
            {
                try
                {
                    if (Directory.Exists(localFilePath))
                    {
                        string[] files = Directory.GetFiles(localFilePath, $"{binaryId}*",
                            SearchOption.TopDirectoryOnly);
                        if (files.Length > 0)
                        {
                            localFilePath = files[0];
                            if (IsCached(() => provider.GetBinaryLastPublishedDate(localization, binaryId),
                                localFilePath,
                                localization))
                            {
                                return localFilePath;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Our binary cache folder probably doesn't exist. 
                    Log.Warn($"Failed to cache binary at {localFilePath}");
                    Log.Warn(ex.Message);
                }

                var data = provider.GetBinary(localization, binaryId);
                if (string.IsNullOrEmpty(Path.GetExtension(localFilePath)))
                {
                    var ext = Path.GetExtension(data.Item2) ?? "";
                    localFilePath = $"{localFilePath}/{binaryId}{ext}";
                }

                WriteBinaryToFile(data.Item1, localFilePath, null, out memoryStream);
                return localFilePath;
            }
        }

        /// <summary>
        /// Perform actual write of binary content to file
        /// </summary>
        /// <param name="binary">The binary to store</param>
        /// <param name="physicalPath">String the file path to write to</param>
        /// <param name="dimensions">Dimensions of file</param>
        /// <returns>True is binary was written to disk, false otherwise</returns>
        private static void WriteBinaryToFile(byte[] binary, string physicalPath, Dimensions dimensions, out MemoryStream memoryStream)
        {
            memoryStream = null;
            if (binary == null) return;
            byte[] buffer = binary;
            using (new Tracer(binary, physicalPath, dimensions))
            {
                try
                {
                    if (!File.Exists(physicalPath))
                    {
                        FileInfo fileInfo = new FileInfo(physicalPath);
                        if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                        {
                            fileInfo.Directory.Create();
                        }
                    }
                   
                    if (dimensions != null && (dimensions.Width > 0 || dimensions.Height > 0))
                    {
                        ImageFormat imgFormat = GetImageFormat(physicalPath);
                        if (imgFormat != null) buffer = ResizeImage(buffer, dimensions, imgFormat);
                    }

                    lock (NamedLocker.GetLock(physicalPath))
                    {
                        using (FileStream fileStream = new FileStream(physicalPath, FileMode.Create,
                            FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            fileStream.Write(buffer, 0, buffer.Length);
                        }
                    }

                    NamedLocker.RemoveLock(physicalPath);
                }
                catch (IOException)
                {
                    // file possibly accessed by a different thread in a different process, locking failed
                    Log.Warn("Cannot write to {0}. This can happen sporadically, let the next thread handle this.", physicalPath);
                    Thread.Sleep(1000);
                    memoryStream = new MemoryStream(buffer);
                }
            }
        }

        private static void CleanupLocalFile(string physicalPath)
        {
            using (new Tracer(physicalPath))
            {
                try
                {
                    // file got unpublished
                    File.Delete(physicalPath);
                    NamedLocker.RemoveLock(physicalPath);
                }
                catch (IOException)
                {
                    // file probabaly accessed by a different thread in a different process
                    Log.Warn("Cannot delete '{0}'. This can happen sporadically, let the next thread handle this.", physicalPath);
                }
            }
        }

        internal static byte[] ResizeImage(byte[] imageData, Dimensions dimensions, ImageFormat imageFormat)
        {
            using (new Tracer(imageData.Length, dimensions, imageFormat))
            {
                Image original = Image.FromStream(new MemoryStream(imageData));

                //Defaults for crop position, width and target size
                int cropX = 0, cropY = 0;
                int sourceW = original.Width, sourceH = original.Height;
                int targetW = original.Width, targetH = original.Height;

                //Most complex case is if a height AND width is specified
                if (dimensions.Width > 0 && dimensions.Height > 0)
                {
                    if (dimensions.NoStretch)
                    {
                        //If we don't want to stretch, then we crop
                        float originalAspect = (float)original.Width / (float)original.Height;
                        float targetAspect = (float)dimensions.Width / (float)dimensions.Height;
                        if (targetAspect < originalAspect)
                        {
                            //Crop the width - ensuring that we do not stretch if the requested height is bigger than the original
                            targetH = dimensions.Height > original.Height ? original.Height : dimensions.Height;
                            targetW = (int)Math.Ceiling(targetH * targetAspect);
                            cropX = (int)Math.Ceiling((original.Width - (original.Height * targetAspect)) / 2);
                            sourceW = sourceW - 2 * cropX;
                        }
                        else
                        {
                            //Crop the height - ensuring that we do not stretch if the requested width is bigger than the original
                            targetW = dimensions.Width > original.Width ? original.Width : dimensions.Width;
                            targetH = (int)Math.Ceiling(targetW / targetAspect);
                            cropY = (int)Math.Ceiling((original.Height - (original.Width / targetAspect)) / 2);
                            sourceH = sourceH - 2 * cropY;
                        }
                    }
                    else
                    {
                        //We stretch to fit the dimensions
                        targetH = dimensions.Height;
                        targetW = dimensions.Width;
                    }
                }
                //If we simply have a certain width or height, its simple: We just use that and derive the other
                //dimension from the original image aspect ratio. We also check if the target size is bigger than
                //the original, and if we allow stretching.
                else if (dimensions.Width > 0)
                {
                    targetW = (dimensions.NoStretch && dimensions.Width > original.Width) ? original.Width : dimensions.Width;
                    targetH = (int)(original.Height * ((float)targetW / (float)original.Width));
                }
                else
                {
                    targetH = (dimensions.NoStretch && dimensions.Height > original.Height) ? original.Height : dimensions.Height;
                    targetW = (int)(original.Width * ((float)targetH / (float)original.Height));
                }
                if (targetW == original.Width && targetH == original.Height)
                {
                    //No resize required
                    return imageData;
                }
                Image imgPhoto;
                using (MemoryStream memoryStream = new MemoryStream(imageData))
                {
                    imgPhoto = Image.FromStream(memoryStream);
                }
                // Create a new blank canvas.  The resized image will be drawn on this canvas.
                Bitmap bmPhoto = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb);
                Bitmap bmOriginal = new Bitmap(original);
                bmPhoto.SetResolution(72, 72);
                bmPhoto.MakeTransparent(bmOriginal.GetPixel(0, 0));
                Graphics grPhoto = Graphics.FromImage(bmPhoto);
                grPhoto.SmoothingMode = SmoothingMode.AntiAlias;
                grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
                grPhoto.PixelOffsetMode = PixelOffsetMode.HighQuality;
                grPhoto.DrawImage(imgPhoto, new Rectangle(0, 0, targetW, targetH), cropX, cropY, sourceW, sourceH, GraphicsUnit.Pixel);
                // Save out to memory and then to a file.  We dispose of all objects to make sure the files don't stay locked.
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    bmPhoto.Save(memoryStream, imageFormat);
                    original.Dispose();
                    imgPhoto.Dispose();
                    bmPhoto.Dispose();
                    grPhoto.Dispose();
                    return memoryStream.GetBuffer();
                }
            }
        }

        internal static ImageFormat GetImageFormat(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            switch (Path.GetExtension(path).ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    return ImageFormat.Jpeg;
                case ".gif":
                    return ImageFormat.Gif;
                case ".png":
                    return ImageFormat.Png;
                case ".bmp":
                    return ImageFormat.Bmp;
                default:
                    return null;
            }
        }

        internal static string StripDimensions(string path, out Dimensions dimensions)
        {
            dimensions = new Dimensions();
            Regex re = new Regex(@"_(w(\d+))?(_h(\d+))?(_n)?\.");
            if (re.IsMatch(path))
            {
                Match match = re.Match(path);
                string dim = match.Groups[2].ToString();
                if (!string.IsNullOrEmpty(dim))
                {
                    dimensions.Width = Convert.ToInt32(dim);
                }
                dim = match.Groups[4].ToString();
                if (!string.IsNullOrEmpty(dim))
                {
                    dimensions.Height = Convert.ToInt32(dim);
                }
                if (!string.IsNullOrEmpty(match.Groups[5].ToString()))
                {
                    dimensions.NoStretch = true;
                }
                
                //Git Hub Issue #84
                path = re.Replace(path, ".");
            }

            // TSI-417: unescape and only escape spaces
            path = WebUtility.UrlDecode(path);
            path = path.Replace(" ", "%20");
            return path;
        }
    }
}
