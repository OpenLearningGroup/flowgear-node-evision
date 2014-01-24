using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using flowgear.Sdk;
using System.Xml;
using System.Net;
using System.IO;
using System.Collections;

namespace flowgear.Nodes.Evision
{
    [Node("Evision", "eVision", "1.0.0.13", NodeType.Connector)]
    public partial class Evision : INode
    {
        private const char splitDelimiter = '~';

        #region Private Properties/Variables
        PropertyDictionary _configuration = null;
        Dictionary<string, string> _requestMappings = null;

        #endregion

        #region Constructor
        public Evision()
        {

        }
        #endregion

        #region Public Properties
        [Property(FlowDirection.Input, ExtendedType.ConnectionProfile)]
        public EvisionConnection Connection { get; set; }

        [Property(FlowDirection.Input)]
        public Modules Module { get; set; }

        [Property(FlowDirection.Input)]
        public Actions Action { get; set; }

        [Property(FlowDirection.Input, ExtendedType.Xml)]
        public string ParamXml { get; set; }

        [Property(FlowDirection.Input, ExtendedType.Xml)]
        public string RequestXml { get; set; }

        [Property(FlowDirection.Output, ExtendedType.Xml)]
        public string ResponseXml { get; set; }
        #endregion

        #region INode interface
        public PropertyDictionary Configuration
        {
            set
            {
                _configuration = value;
                try
                {
                    string configXml = (string)_configuration[flowgear.Sdk.Configuration.NodeConfiguration];
                    RequestMappings mappings = new RequestMappings();

                    mappings.ReadXml(new StringReader(configXml));

                    _requestMappings = new Dictionary<string, string>();

                    foreach (RequestMappings.RequestMappingRow mapping in mappings.RequestMapping)
                        _requestMappings.Add(new MappingKey(mapping.Module, mapping.Action).ToString(), mapping.UrlTemplate);

                    if (_requestMappings.Count == 0)
                        throw new Exception("No Request Mappings were loaded!");
                }
                catch
                {
                    throw new Exception("The configuration for this node has not been set up correctly!");
                }
            }
        }

        public List<InvokeResult> ExtendedInvokeResults
        {
            get { return null; }
        }

        public InvokeResult Invoke()
        {
            ResponseXml = handleInvoke(Module, Action, RequestXml);
            return new InvokeResult();
        }

        public bool ShouldExit { get; set; }

        public void Terminate()
        {

        }
        #endregion

        #region Test Interface
        public void Test()
        {
            handleInvoke(Modules.AllProgrammes, Actions.Get, null);
        }

        private string getHttpMethod(Actions action)
        {
            switch (action)
            {
                case Actions.List: return "GET";
                case Actions.Get: return "GET";
                case Actions.Create: return "POST";
                case Actions.Update: return "PUT";
                case Actions.Delete: return "DELETE";
                default:
                    throw new Exception("Unrecognised Action!");
            }
        }
        #endregion

        #region Internal
        private string getRequestUrl(Modules module, Actions action)
        {
            string mappingKey = new MappingKey(module, action).ToString();

            if (!_requestMappings.ContainsKey(mappingKey))
                throw new Exception("This module/action combination is not supported!");

            string url;
            ArrayList parameters = new ArrayList();
            parameters.Add(Connection.BaseUrl());
            if (!string.IsNullOrEmpty(ParamXml))
                url = string.Format(queryStringParamter(_requestMappings[mappingKey]), parameters.ToArray());
            else
                url = string.Format(_requestMappings[mappingKey], parameters.ToArray());
            return url;
        }

        private string queryStringParamter(string addUrlParam)
        {
            string paramUrl = null;
            if (!addUrlParam.Contains("?")) addUrlParam += "?";
            bool oDataSearch = false;

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(ParamXml);
            foreach (XmlElement node in doc.FirstChild.ChildNodes)
            {
                if (!string.IsNullOrEmpty(node.InnerText))
                {
                    if (node.Name.Contains("filter") || node.Name.Contains("orderby")
                        || node.Name.Contains("skip") || node.Name.Contains("top"))
                        oDataSearch = true;

                    if (string.IsNullOrEmpty(paramUrl))
                        paramUrl += oDataSearch ?
                            string.Format("${0}={1}", node.Name, node.InnerText) :
                            string.Format("{0}={1}", node.Name, node.InnerText);
                    else
                        paramUrl += oDataSearch ?
                          string.Format("&${0}={1}", node.Name, node.InnerText) :
                          string.Format("&{0}={1}", node.Name, node.InnerText);
                    oDataSearch = false;
                }
            }

            return addUrlParam.Substring(0, addUrlParam.IndexOf("?") + 1) + paramUrl;
        }

        private string textFromXml(string xml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                string text = "";
                getNodeText(doc.LastChild, ref text);
                return text;
            }
            catch
            {
                return xml;
            }
        }

        private void getNodeText(XmlNode node, ref string text)
        {
            XmlElement element = node as XmlElement;
            if (element != null)
            {
                if ((element.ChildNodes.Count == 1) && (element.ChildNodes[0].NodeType == XmlNodeType.Text))
                {
                    string newText = element.InnerText.Trim();
                    if (!newText.EndsWith(".")) newText += ". ";
                    text += newText;
                }
                else
                {
                    foreach (XmlNode childNode in element.ChildNodes)
                        getNodeText(childNode, ref text);
                }
            }
        }

        private string handleInvoke(Modules module, Actions action, string requestXml)
        {
            if (Connection == null)
                throw new Exception("No Connection was specified!");

            if (_requestMappings == null)
                throw new Exception("Request Mappings could not be loaded!");

            string requestUrl = getRequestUrl(module, action);

            string httpMethod = getHttpMethod(action);

            if (requestUrl == null)
                throw new Exception(string.Format("Request URL is not set! {0} {1} {2}", module, action));

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(Uri.EscapeUriString(requestUrl));
            request.Method = httpMethod;

            if ((httpMethod == "POST") || (httpMethod == "PUT"))
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(requestXml);

                request.GetRequestStream().Write(buffer, 0, buffer.Length);
            }

            if ((_configuration != null) && _configuration.Contains(flowgear.Sdk.Configuration.InvokeTimeoutSecs))
                request.Timeout = (int)_configuration[flowgear.Sdk.Configuration.InvokeTimeoutSecs] * 1000;

            request.Accept = "application/xml";

            request.ContentType = "application/xml; charset=utf-8";
            var encoding = Encoding.GetEncoding("iso-8859-1");
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(encoding.GetBytes(string.Format("{0}:{1}",
                Connection.Username, Connection.Password))));

            request.Credentials = new NetworkCredential(Connection.Username, Connection.Password);

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response == null)
                    throw new Exception("No response was returned!");

                Stream responseStream = response.GetResponseStream();

                if (responseStream == null)
                    throw new Exception("Response was empty!");
                string responseXml = new StreamReader(responseStream).ReadToEnd();

                // DJJ - 2013-10-08 OLG - UpdatePickingListStatus Support for empty response
                if (response.StatusCode.ToString() == "Created")
                {
                    return responseXml.ToString();
                }
                else
                {
                    XmlDocument docout = new XmlDocument();
                    docout.LoadXml(responseXml);

                    // this will check if there is a cursor, and will return it.
                    if (response.Headers.AllKeys.Contains("Link"))
                    {
                        string link = response.Headers["Link"].ToString();
                        link = link.Substring(link.IndexOf("rel=\"prev\"") + 1);
                        link = link.Substring(link.IndexOf("cursor=") + 7);

                        string cursorText = "";
                        int num;
                        while (int.TryParse(link[0].ToString(), out num))
                        {
                            cursorText += link[0].ToString();
                            link = link.Substring(1);
                        }

                        XmlElement cursor = docout.CreateElement("cursor");
                        cursor.InnerText = cursorText;
                        docout.LastChild.AppendChild(cursor);
                    }
                    return docout.InnerXml;
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse response = (HttpWebResponse)wex.Response;
                if (response == null)
                    throw new Exception("Response was empty, original error was '" + wex.Message + "'");

                Stream responseStream = response.GetResponseStream();
                if (responseStream == null)
                    throw new Exception("Response stream was empty, original error was '" + wex.Message + "'");

                string responseContent = new StreamReader(responseStream).ReadToEnd();

                string responseMessage = string.Format(
                    "The server returned {0}{1}: {2}",
                    response.StatusCode,
                    response.StatusDescription == "" ? "" : " (" + response.StatusDescription + ")",
                    textFromXml(responseContent));

                throw new Exception(responseMessage);
            }
        }
        #endregion
    }
}