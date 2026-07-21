using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Auth.Contracts
{
    public sealed class RefreshTokenRequestDto
    {
        public string RefreshToken { get; init; } = string.Empty;

        public bool UseCookie { get; set; }
    }

}
