using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;

namespace AmlScreening.Application.Services
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

            var roles = await _userRepository.GetUserRolesAsync(user.Id);
            if (roles.Count == 0)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "No active role is assigned to this account."
                };
            }

            var permissions = await _userRepository.GetPermissionsAsync(user.Id);

            return new LoginResult
            {
                Success = true,
                UserId = user.Id,
                ClientId = user.ClientId,
                Username = user.Username,
                FullName = user.FullName,
                Permissions = permissions,
                Roles=roles.Select(x=>x.RoleName).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

    }
}
