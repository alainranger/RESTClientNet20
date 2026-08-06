using System.Net;

namespace RESTClientNet20.Tests.Models
{
	internal class TestHttpRequest
	{
		private string _method;
		private string _path;
		private WebHeaderCollection _headers;
		private string _body;

		public string Method
		{
			get { return _method; }
			set { _method = value; }
		}

		public string Path
		{
			get { return _path; }
			set { _path = value; }
		}

		public WebHeaderCollection Headers
		{
			get { return _headers; }
			set { _headers = value; }
		}

		public string Body
		{
			get { return _body; }
			set { _body = value; }
		}
	}
}
