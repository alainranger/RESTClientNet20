namespace RESTClientNet20.Tests.Models
{
	internal class TestHttpResponse
	{
		private readonly int _statusCode;
		private readonly string _contentType;
		private readonly string _body;

		public TestHttpResponse(int statusCode, string contentType, string body)
		{
			_statusCode = statusCode;
			_contentType = contentType;
			_body = body;
		}

		public int StatusCode
		{
			get { return _statusCode; }
		}

		public string ContentType
		{
			get { return _contentType; }
		}

		public string Body
		{
			get { return _body; }
		}
	}
}
