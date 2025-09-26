﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddlewareTool
{
    public class LoggedRequest
    {
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss.fff");
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public int StatusCode { get; set; }
        public string RequestBody { get; set; } = "";
        public string ResponseBody { get; set; } = "";
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    }
}
