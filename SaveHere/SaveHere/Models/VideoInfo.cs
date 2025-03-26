namespace SaveHere.Models
{
    public class VideoInfo
    {
        public string Title { get; set; }

        public string Filename { get; set; }
        public string Ext { get; set; }
        public long? Filesize { get; set; } // Actual file size
        public bool RequiresLogin { get; set; }

        public string Errors { get; set; } = string.Empty;


        public string ConvertBytesToFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            if (bytes >= TB)
                return $"{bytes / (double)TB:F2} TB";
            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";

            return $"{bytes} bytes";  // If the file size is less than 1 KB
        }

        public override string ToString()
        {
            if (Errors == string.Empty)
            {
                return $"Title: {Title}<br />" +  // Title on a new line
                    $"Filename: {Filename}<br />" +  // Title on a new line
                       $"Extension: {Ext}<br />" +  // Extension on a new line
                       $"Filesize: {(Filesize.HasValue ? ConvertBytesToFileSize(Filesize.Value) : "Unknown")}<br />" +  // Filesize on a new line
                       $"Requires Login: {(RequiresLogin ? "Yes" : "No")}";  // Requires Login on a new line
            }
            else
            {
                return $"Errors when getting file info: {Errors}";
            }

        }
    }
}
