using System;


namespace T2G
{
    [Serializable]
    public class Response
    {
        public bool Succeeded = true;
        public string Message = string.Empty;
        public string ObjectId = string.Empty;

        public Response()
        {
            Succeeded = true;
            Message = string.Empty;
            ObjectId = string.Empty;
        }

        public Response(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
            ObjectId = string.Empty;
        }
    }
}