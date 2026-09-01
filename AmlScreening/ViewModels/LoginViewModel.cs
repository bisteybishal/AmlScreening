using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "User Name is required")]
        public string UserName { get; set; }= string.Empty;
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }= string.Empty;
        public bool RememberMe { get; set; }
    }

}
