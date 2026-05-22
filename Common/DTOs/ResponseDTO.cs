using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ResponseDTO
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public object? Result { get; set; } = null;
    }

    public class ResponseDTO<T> : ResponseDTO
    {
        public T? Data { get; set; }
    }
}
