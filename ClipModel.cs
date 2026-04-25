using System;

namespace ClipVault
{
    public class ClipModel
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public string Type { get; set; }
        public string Preview { get; set; }
        public string CopiedAt { get; set; }
        public int CopyCount { get; set; }
        public bool IsPinned { get; set; }

        // UI Helpers
        public string TypeIcon => Type switch
        {
            "LINK" => "Link",
            "CODE" => "Code",
            "EMAIL" => "Mail",
            "IMAGE" => "Image",
            _ => "FileText"
        };
        
        public bool IsImage => Type == "IMAGE";
    }
}
