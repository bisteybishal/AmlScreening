using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Security
{
    public class PasswordService:IPasswordService
    {
        private readonly PasswordHasher<object> _passwordHasher=new();
        public bool Verify(string hashedPassword, string enteredPassword)
        {
            var result = _passwordHasher.VerifyHashedPassword(new object(), hashedPassword, enteredPassword);
            return result == PasswordVerificationResult.Success;
        }
    }
}
