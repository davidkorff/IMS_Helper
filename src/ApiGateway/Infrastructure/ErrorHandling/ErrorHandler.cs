using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

public class ErrorHandler
{
    public class IMSException : Exception
    {
        public string ErrorCode { get; }
        public string BusinessMessage { get; }
    }
    
    public async Task<IActionResult> HandleException(Exception ex)
    {
        // Map IMS SOAP errors to REST responses
    }
} 