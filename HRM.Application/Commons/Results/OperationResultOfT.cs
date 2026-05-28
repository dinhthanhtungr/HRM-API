using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Models
{
    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; protected set; }

        public static OperationResult<T> Ok(T data, string? message = null)
            => new() { Success = true, Message = message, Data = data };

        public static new OperationResult<T> Fail(string message, string? errorCode = null)
            => new() { Success = false, Message = message, ErrorCode = errorCode };

        public static OperationResult<T> Fail(T? data, string message, string? errorCode = null)
            => new() { Success = false, Message = message, ErrorCode = errorCode, Data = data };
    }
}
