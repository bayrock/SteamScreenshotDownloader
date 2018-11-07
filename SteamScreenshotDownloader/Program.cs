using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SteamScreenshotDownloader
{
    public class SteamScreenshot
    {
        public Int32 FileId { get; set; }

        public String ScreenshotFilename { get; set; }

        public String ScreenshotUrl { get; set; }

        public String ThumbnailFilename { get; set; }

        public String ThumbnailUrl { get; set; }
    }

    class Program
    {
        private static String BaseDirectory { get; set; }

        static void Main(string[] args)
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Steam Screenshot Downloader, version: {assemblyVersion}");
            Console.ResetColor();

            BaseDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FromSteam");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("To start, enter the steam account profile id");
            Console.WriteLine("For example: If your profile url is https://steamcommunity.com/id/gabelogannewell, the profile id is 'gabelogannewell'");
            Console.ResetColor();
            Console.Write("Enter Steam ID: ");
            var steamId = Console.ReadLine();

            var screenshots = GetFileIdAndThumbnails(new List<SteamScreenshot>(), steamId, 1);

            foreach (var screenshot in screenshots)
            {
                var fullscreenUrl = GetFileActualUrl(screenshot.FileId);

                if (!String.IsNullOrWhiteSpace(fullscreenUrl))
                {
                    screenshot.ScreenshotUrl = fullscreenUrl;
                }
            }

            SaveScreenshots(screenshots);

            Console.WriteLine("Done! Processed {0} screenshots, press any key to exit.", screenshots.Count);
            Console.Read();
        }

        private static WebResponse TryGetResponse(HttpWebRequest request, Int32 maxRetries = 10, Int32 retryWaitSeconds = 3)
        {
            WebResponse result = null;
            Int32 tries = 0;

            do
            {
                try
                {
                    tries++;
                    result = request.GetResponse();
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Web request error, attempt {0}", tries);
                    Console.ResetColor();

                    System.Threading.Thread.Sleep(retryWaitSeconds * 1000);
                }
            } while (result == null && tries <= maxRetries);

            return result;
        }

        private static void SaveScreenshots(List<SteamScreenshot> screenshots)
        {
            const String DispositionPattern = @"inline; filename(?:\*\=UTF-8'')(?<Filename>.*?);|inline; filename=""(?<Filename>.*?)"";";

            foreach (var screenshot in screenshots)
            {
                if (String.IsNullOrWhiteSpace(screenshot.ScreenshotUrl))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Screenshot Url is not valid for File Id: {0}", screenshot.FileId);
                    Console.ResetColor();
                    continue;
                }

                var screenshotWebRequest = WebRequest.Create(screenshot.ScreenshotUrl) as HttpWebRequest;

                using (var response = TryGetResponse(screenshotWebRequest))
                {
                    // TODO: Handle null responses...
                    if (response != null)
                    {
                        using (var stream = response.GetResponseStream())
                        {
                            if (response.Headers.AllKeys.Any(x => x.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)))
                            {
                                var fullDisposition = response.Headers["Content-Disposition"];
                                var regexMatch = Regex.Match(fullDisposition, DispositionPattern);

                                if (regexMatch.Success)
                                {
                                    var fullFilename = regexMatch.Groups["Filename"].Value;

                                    // Underscores are used as folder separators, except the last one, which splits the date & time values
                                    var lastUnderscore = fullFilename.LastIndexOf('_');

                                    fullFilename = fullFilename.Substring(0, lastUnderscore).Replace('_', '\\') + fullFilename.Substring(lastUnderscore, fullFilename.Length - lastUnderscore);

                                    screenshot.ScreenshotFilename = fullFilename;
                                }
                            }
                            else
                            {
                                screenshot.ScreenshotFilename = String.Format("ss_{0}.jpg", screenshot.FileId);
                            }

                            var fullScreenshotFilePath = Path.Combine(BaseDirectory, screenshot.ScreenshotFilename);
                            var fullScreenshotDirectoryPath = Path.GetDirectoryName(fullScreenshotFilePath);

                            if (!Directory.Exists(fullScreenshotDirectoryPath))
                            {
                                Directory.CreateDirectory(fullScreenshotDirectoryPath);
                            }

                            using (var fileStream = new FileStream(fullScreenshotFilePath, FileMode.Create))
                            {
                                Console.WriteLine("Saving screenshot to {0}", fullScreenshotFilePath);
                                stream.CopyTo(fileStream);

                            }
                        }
                    }
                }

                if (String.IsNullOrWhiteSpace(screenshot.ThumbnailUrl))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Thumbnail Url is not valid for File Id: {0}", screenshot.FileId);
                    Console.ResetColor();
                    continue;
                }

                var thumbnailWebRequest = WebRequest.Create(screenshot.ThumbnailUrl) as HttpWebRequest;

                using (var response = TryGetResponse(thumbnailWebRequest))
                {
                    // TODO: Handle null responses...
                    if (response != null)
                    {
                        using (var stream = response.GetResponseStream())
                        {
                            if (response.Headers.AllKeys.Any(x => x.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)))
                            {
                                var fullDisposition = response.Headers["Content-Disposition"];
                                var regexMatch = Regex.Match(fullDisposition, DispositionPattern);

                                if (regexMatch.Success)
                                {
                                    var fullFilename = regexMatch.Groups["Filename"].Value;

                                    var lastUnderscore = fullFilename.LastIndexOf('_');

                                    fullFilename = fullFilename.Substring(0, lastUnderscore).Replace('_', '\\') + fullFilename.Substring(lastUnderscore, fullFilename.Length - lastUnderscore);

                                    screenshot.ThumbnailFilename = fullFilename;
                                }
                            }
                            else
                            {
                                screenshot.ThumbnailFilename = String.Format("t_{0}.jpg", screenshot.FileId);
                            }

                            var fullScreenshotFilePath = Path.Combine(BaseDirectory, screenshot.ThumbnailFilename);
                            var fullScreenshotDirectoryPath = Path.GetDirectoryName(fullScreenshotFilePath);

                            if (!Directory.Exists(fullScreenshotDirectoryPath))
                            {
                                Directory.CreateDirectory(fullScreenshotDirectoryPath);
                            }

                            using (var fileStream = new FileStream(fullScreenshotFilePath, FileMode.Create))
                            {
                                Console.WriteLine("Saving thumbnail to {0}", fullScreenshotFilePath);
                                stream.CopyTo(fileStream);
                            }
                        }
                    }
                }
            }
        }

        private static List<SteamScreenshot> GetFileIdAndThumbnails(List<SteamScreenshot> screenshots, String steamId, Int32 pageNo)
        {
            Console.WriteLine("Getting file ids and thumbnails for {0}, page {1}", steamId, pageNo);
            var requestScreenshots = new List<SteamScreenshot>();

            const String fileDetailPattern = @"<div style=""background-image: url\('(?<ThumbnailUrl>.*?)'\);"" class=""imgWallItem.*?id=""imgWallItem_(?<FileId>\d{1,})";

            var url = "";
            var urlFormatString = GetAppSetting("ScreenshotGridUrlFormatString");

            if (String.IsNullOrWhiteSpace(urlFormatString))
            {
                url = String.Format("https://steamcommunity.com/id/{0}/screenshots/?p={1}&sort=newestfirst&view=grid", steamId, pageNo);
            }
            else
            {
                url = String.Format(urlFormatString, steamId, pageNo);
            }

            var webRequest = WebRequest.Create(url) as HttpWebRequest;

            using (var response = TryGetResponse(webRequest))
            {
                // TODO: Handle null responses...
                if (response != null)
                {
                    using (var stream = response.GetResponseStream())
                    using (var streamReader = new StreamReader(stream))
                    {
                        var html = streamReader.ReadToEnd();

                        var fileIdMatches = Regex.Matches(html, fileDetailPattern, RegexOptions.IgnoreCase);

                        for (int i = 0; i < fileIdMatches.Count; i++)
                        {
                            var match = fileIdMatches[i];
                            var fileIdValue = Int32.Parse(match.Groups["FileId"].Value.Trim());
                            var thumbnailUrlValue = match.Groups["ThumbnailUrl"].Value.Trim();
                            var newScreenshot = new SteamScreenshot { FileId = fileIdValue, ThumbnailUrl = thumbnailUrlValue };

                            Console.WriteLine("Added File Id: {0}, with Thumbnail Url: {1}", newScreenshot.FileId, newScreenshot.ThumbnailUrl);

                            requestScreenshots.Add(newScreenshot);
                        }
                    }
                }
            }

            if (requestScreenshots.Any())
            {
                screenshots.AddRange(requestScreenshots);
                GetFileIdAndThumbnails(screenshots, steamId, pageNo + 1);
            }

            return screenshots;
        }

        private static String GetAppSetting(String key)
        {
            var value = System.Configuration.ConfigurationManager.AppSettings[key];

            return value;
        }

        private static String GetFileActualUrl(Int32 fileId)
        {
            // Default Steam File Detail format string: https://steamcommunity.com/sharedfiles/filedetails/?id={0}
            var fileDetailUrlFormatString = GetAppSetting("SteamFileDetailUrlFormatString");

            if (String.IsNullOrWhiteSpace(fileDetailUrlFormatString))
            {
                fileDetailUrlFormatString = "https://steamcommunity.com/sharedfiles/filedetails/?id={0}";
            }

            // Default Screenshot Url Base: https://steamuserimages-a.akamaihd.net/ugc/
            var screenshotUrlBase = GetAppSetting("DefaultScreenshotUrlBase");

            if (String.IsNullOrWhiteSpace(screenshotUrlBase))
            {
                screenshotUrlBase = @"href=""(?<Url>https://steamuserimages-a.akamaihd.net/ugc/.*?)""";
            }

            var fileDetailUrl = String.Format(fileDetailUrlFormatString, fileId);

            var webRequest = WebRequest.Create(fileDetailUrl) as HttpWebRequest;

            using (var response = TryGetResponse(webRequest))
            {
                // TODO: Handle null responses...
                if (response != null)
                {
                    using (var stream = response.GetResponseStream())
                    using (var streamReader = new StreamReader(stream))
                    {
                        var html = streamReader.ReadToEnd();

                        var actualFileUrlMatches = Regex.Matches(html, screenshotUrlBase, RegexOptions.IgnoreCase);

                        if (actualFileUrlMatches.Count > 0)
                        {
                            var match = actualFileUrlMatches[0];
                            var value = match.Groups["Url"].Value.Trim();

                            Console.WriteLine("Found screenshot url {0} for File Id: {1}", value, fileId);

                            return value;
                        }
                    }
                }
            }

            return null;
        }
    }
}
