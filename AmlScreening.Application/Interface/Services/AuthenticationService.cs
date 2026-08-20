using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Security;
using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordService _passwordService;
        public AuthenticationService(IUserRepository userRepository, IPasswordService passwordService)
        {
            _passwordService = passwordService;
            _userRepository = userRepository;
        }
        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            var user = await _userRepository.GetUserAsync(username);
            if (user == null)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Invalid username or password."
                };
            }
            if (!user.IsActive)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "User is inactive."
                };
            }
            var passwordValid = _passwordService.Verify(user.PasswordHash, password);
            if (!passwordValid)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Invalid username or password."
                };
            }

            return new LoginResult
            {
                Success = true,
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role
            };
        }

    }
}
