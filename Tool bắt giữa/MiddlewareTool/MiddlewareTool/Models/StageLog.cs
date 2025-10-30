// MiddlewareTool/Models/StageLog.cs
using System;

namespace MiddlewareTool.Models
{
    public class StageLog
    {
        public int Stage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ClientOutput { get; set; } = "";  // Delta thay đổi hoặc full snapshot
        public string ServerOutput { get; set; } = "";
        public string Description { get; set; } = "";  // Mô tả như "Initial", "User Input", "Response"
    }
}