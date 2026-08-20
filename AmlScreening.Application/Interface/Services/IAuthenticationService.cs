using AmlScreening.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Services
{
    public  interface IAuthenticationService
    {
        Task<LoginResult>LoginAsync(string username, string password);
    }
}
