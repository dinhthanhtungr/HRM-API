using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Models
{
    public class OperationResult
    {
        public bool Success { get; protected set; }
        public string? Message { get; protected set; }
        public string? ErrorCode { get; protected set; }

        public static OperationResult Ok(string? message = null)
            => new() { Success = true, Message = message };

        public static OperationResult Fail(string message, string? errorCode = null)
            => new() { Success = false, Message = message, ErrorCode = errorCode };
    }
}
