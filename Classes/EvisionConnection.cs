using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using flowgear.Sdk;

namespace flowgear.Nodes.Evision
{
    public class EvisionConnection
    {
        public string URL { get; set; }

        public string Username { get; set; }

        [Property(ExtendedType.Password)]
        public string Password { get; set; }

        public string BaseUrl()
        {
            string url = "";

            if (string.IsNullOrEmpty(URL))
                url = "https://www.olg.co.za.evisionapi/api";
            else
            {
                url = URL;
                if (!url.Contains("://")) url = "https://" + url;
                if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);
            }

            return url;
        }
    }
}