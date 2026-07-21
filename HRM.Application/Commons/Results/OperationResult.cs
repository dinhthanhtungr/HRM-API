using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Models
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public static OperationResult Ok(string? message = null) => new() { Success = true, Message = message };
        public static OperationResult Fail(string message) => new() { Success = false, Message = message };

    }

    // Generic version
    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; set; }

        public static OperationResult<T> Ok(T data, string? message = null)
            => new() { Success = true, Message = message, Data = data };

        public static new OperationResult<T> Ok(string message)
            => new() { Success = true, Message = message };

        public static new OperationResult<T> Fail(string message)
            => new() { Success = false, Message = message };

        public static OperationResult<T> Fail(T? data, string message)
            => new() { Success = false, Message = message, Data = data };
    }
}
