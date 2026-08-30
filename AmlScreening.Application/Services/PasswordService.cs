using AmlScreening.Application.Interface;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Services
{
    public class PasswordService:IPasswordService
    {
        private readonly PasswordHasher<object> _passwordHasher=new();
        public string Hash(string password)
        {
            return _passwordHasher.HashPassword(new object(), password);
        }

        public bool Verify(string hashedPassword, string enteredPassword)
        {
            var result = _passwordHasher.VerifyHashedPassword(new object(), hashedPassword, enteredPassword);
            return result == PasswordVerificationResult.Success;
        }
    }
}
